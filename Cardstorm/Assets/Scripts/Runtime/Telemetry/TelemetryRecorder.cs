using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cardstorm.Cards;
using Cardstorm.Core;
using Cardstorm.Enemies;
using Cardstorm.Player;
using Cardstorm.Progression;
using UnityEngine;

namespace Cardstorm.Telemetry
{
    // Graba cada partida en Application.persistentDataPath/telemetria: eventos, muestras cada 15 s, cartas y resumen.
    // Eventos y muestras se escriben sobre la marcha; cartas y resumen, al morir, ganar o salir.
    [DefaultExecutionOrder(100)]  // Después del Start de RunManager, que crea el estado.
    public class TelemetryRecorder : MonoBehaviour
    {
        const string CodeKey = "telemetry.code";
        const string RunsKey = "telemetry.runs";

        [SerializeField] RunManager run;
        [SerializeField] GameClock clock;
        [SerializeField] PlayerCards cards;
        [SerializeField] PlayerHealth health;
        [SerializeField] EnemyManager enemies;

        [Tooltip("Segundos de partida entre muestras.")]
        [SerializeField, Min(1f)] float sampleInterval = 15f;

        readonly RunTelemetry telemetry = new();
        readonly List<CardInstance> deckBuffer = new();
        readonly List<string> exhausted = new();

        RunInfo info;
        StreamWriter eventsWriter;
        StreamWriter samplesWriter;
        string prefix;
        double realStart;
        double nextSample;
        SceneSample lastSample;
        double lastTime;
        bool started;
        bool finished;

        public string PlayerCode { get; private set; }

        public string Folder => Path.Combine(Application.persistentDataPath, "telemetria");

        void Awake()
        {
            if (run == null) run = FindFirstObjectByType<RunManager>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();

            PlayerCode = PlayerPrefs.GetString(CodeKey, "");
            if (PlayerCode.Length != Telemetry.PlayerCode.Length)
            {
                PlayerCode = Telemetry.PlayerCode.Generate(new System.Random(Environment.TickCount));
                PlayerPrefs.SetString(CodeKey, PlayerCode);
                PlayerPrefs.Save();
            }
        }

        void OnEnable()
        {
            RunEvents.XpGained += OnXp;
            RunEvents.GoldGained += OnGold;
            RunEvents.GoldSpent += OnGoldSpent;
            RunEvents.LevelUp += OnLevelUp;
            RunEvents.LevelUpChoice += OnLevelUpChoice;
            RunEvents.CardsRolled += OnCardsRolled;
            RunEvents.CardBought += OnCardBought;
            RunEvents.ShopRefreshed += OnShopRefreshed;
            RunEvents.CardRemoved += OnCardRemoved;
            RunEvents.GemsRolled += OnGemsRolled;
            RunEvents.GemApplied += OnGemApplied;
            RunEvents.GemsRefreshed += OnGemsRefreshed;
            RunEvents.CardRepeated += OnCardRepeated;
            RunEvents.BossSpawned += OnBossSpawned;
            RunEvents.BossKilled += OnBossKilled;
            RunEvents.BossRetreated += OnBossRetreated;
            RunEvents.RareCardTaken += OnRareCardTaken;
            RunEvents.CheatUsed += OnCheatUsed;
            RunEvents.RunWon += OnRunWon;
            DamageEvents.EnemyDamaged += OnEnemyDamaged;
            if (enemies != null) enemies.EnemySpawned += OnEnemySpawned;
            if (health != null)
            {
                health.Damaged += OnPlayerDamaged;
                health.Died += OnDied;
            }
            if (cards != null)
            {
                cards.CardPlayed += OnCardPlayed;
                cards.PlayFailed += OnPlayFailed;
            }
        }

        void OnDisable()
        {
            // Salir o reiniciar a mitad de partida.
            Finish(RunResult.Abandoned, null);

            RunEvents.XpGained -= OnXp;
            RunEvents.GoldGained -= OnGold;
            RunEvents.GoldSpent -= OnGoldSpent;
            RunEvents.LevelUp -= OnLevelUp;
            RunEvents.LevelUpChoice -= OnLevelUpChoice;
            RunEvents.CardsRolled -= OnCardsRolled;
            RunEvents.CardBought -= OnCardBought;
            RunEvents.ShopRefreshed -= OnShopRefreshed;
            RunEvents.CardRemoved -= OnCardRemoved;
            RunEvents.GemsRolled -= OnGemsRolled;
            RunEvents.GemApplied -= OnGemApplied;
            RunEvents.GemsRefreshed -= OnGemsRefreshed;
            RunEvents.CardRepeated -= OnCardRepeated;
            RunEvents.BossSpawned -= OnBossSpawned;
            RunEvents.BossKilled -= OnBossKilled;
            RunEvents.BossRetreated -= OnBossRetreated;
            RunEvents.RareCardTaken -= OnRareCardTaken;
            RunEvents.CheatUsed -= OnCheatUsed;
            RunEvents.RunWon -= OnRunWon;
            DamageEvents.EnemyDamaged -= OnEnemyDamaged;
            if (enemies != null) enemies.EnemySpawned -= OnEnemySpawned;
            if (health != null)
            {
                health.Damaged -= OnPlayerDamaged;
                health.Died -= OnDied;
            }
            if (cards != null)
            {
                cards.CardPlayed -= OnCardPlayed;
                cards.PlayFailed -= OnPlayFailed;
            }
        }

        void OnApplicationQuit() => Finish(RunResult.Abandoned, null);

        void Start()
        {
            int runNumber = PlayerPrefs.GetInt(RunsKey, 0) + 1;
            PlayerPrefs.SetInt(RunsKey, runNumber);
            PlayerPrefs.Save();

            var now = DateTime.Now;
            info = new RunInfo(Application.version, PlayerCode, runNumber, now.ToString("yyyy-MM-dd HH:mm:ss"),
                run != null ? run.Seed : 0);
            realStart = Time.realtimeSinceStartupAsDouble;
            prefix = Path.Combine(Folder, $"{PlayerCode}_{now:yyyyMMdd_HHmmss}_");

            try
            {
                Directory.CreateDirectory(Folder);
                eventsWriter = Open("eventos", RunTelemetry.EventsHeader);
                samplesWriter = Open("muestras", RunTelemetry.SamplesHeader);
            }
            catch (Exception e)
            {
                Fail(e);
                return;
            }

            started = true;
            lastSample = ReadScene();
            lastTime = Now;
            WriteSample(lastTime);
            nextSample = sampleInterval;
        }

        void Update()
        {
            if (!started || finished) return;

            lastSample = ReadScene();
            lastTime = Now;
            if (clock != null && cards != null) telemetry.TickEnergy(clock.DeltaTime, cards.Energy.IsFull);

            // Tras un salto de tiempo, una sola muestra con el tiempo nuevo.
            if (lastTime >= nextSample)
            {
                WriteSample(lastTime);
                nextSample = (Math.Floor(lastTime / sampleInterval) + 1.0) * sampleInterval;
            }
        }

        double Now => clock != null ? clock.Elapsed : Time.timeSinceLevelLoad;

        // Valores de la escena. Se guardan en cada frame, con el tiempo: al salir, los componentes pueden estar ya destruidos.
        SceneSample ReadScene()
        {
            var state = run != null ? run.State : null;
            var energy = cards != null ? cards.Energy : null;
            return new SceneSample(
                state != null ? state.Level : 1,
                state != null ? state.Gold : 0,
                health != null ? health.Current : 0f,
                health != null ? health.MaxHealth : 0f,
                energy != null ? energy.Current : 0f,
                energy != null ? energy.Wasted : 0f,
                enemies != null ? enemies.AliveCount : 0);
        }

        void OnDied() => Finish(RunResult.Death, health != null ? health.LastDamageCause : null);

        void OnRunWon(double time, int level)
        {
            Event(time, "victoria", null, null, level);
            Finish(RunResult.Victory, null);
        }

        void Finish(RunResult result, string deathCause)
        {
            if (!started || finished) return;
            if (result != RunResult.Abandoned)
            {
                lastSample = ReadScene();
                lastTime = Now;
            }
            double end = lastTime;
            if (result == RunResult.Death) Event(end, "muerte", deathCause, null, null);
            WriteSample(end);
            finished = true;

            try
            {
                using (var writer = Open("cartas", RunTelemetry.CardsHeader))
                    foreach (var line in telemetry.CardLines()) writer.WriteLine(line);

                using (var writer = Open("resumen", RunTelemetry.SummaryHeader))
                    writer.WriteLine(telemetry.SummaryLine(in info, result, end, Time.realtimeSinceStartupAsDouble - realStart,
                        in lastSample, deathCause, FinalDeck()));
            }
            catch (Exception e)
            {
                Fail(e);
            }

            Close();
        }

        // Las cartas que siguen en el ciclo y las de poder ya jugadas, que salen de él.
        List<string> FinalDeck()
        {
            var deck = new List<string>(exhausted);
            var cycle = cards != null ? cards.Cycle : null;
            if (cycle != null)
            {
                cycle.CopyCards(deckBuffer);
                foreach (var card in deckBuffer)
                    deck.Add(RunTelemetry.DeckEntry(card.Definition.id, card.Gem != null ? card.Gem.id : null));
            }
            return deck;
        }

        StreamWriter Open(string name, string header)
        {
            // Con BOM, para que Excel lea bien los acentos.
            var writer = new StreamWriter(prefix + name + ".csv", false, new UTF8Encoding(true));
            writer.WriteLine(header);
            return writer;
        }

        void WriteSample(double time)
        {
            if (samplesWriter == null) return;
            try
            {
                samplesWriter.WriteLine(telemetry.SampleLine(time, in lastSample));
                samplesWriter.Flush();
                eventsWriter?.Flush();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        void Event(double time, string evento, string id, string id2, double? value)
        {
            if (eventsWriter == null) return;
            try
            {
                eventsWriter.WriteLine(RunTelemetry.EventLine(time, evento, id, id2, value));
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        void Fail(Exception e)
        {
            Debug.LogError($"[TelemetryRecorder] No se pudo escribir en {Folder}: {e.Message}");
            Close();
        }

        void Close()
        {
            try
            {
                eventsWriter?.Dispose();
                samplesWriter?.Dispose();
            }
            catch (Exception) { }
            eventsWriter = null;
            samplesWriter = null;
        }

        void OnXp(double time, float xp) => telemetry.OnXp(xp);

        void OnGold(double time, int gold, GoldSource source) => telemetry.OnGold(gold, source);

        void OnGoldSpent(double time, int amount, SpendReason reason) => telemetry.OnGoldSpent(amount);

        void OnLevelUp(double time, int level)
        {
            telemetry.OnLevelUp(time, level);
            Event(time, "subida", null, null, level);
        }

        void OnLevelUpChoice(double time, int level, LevelUpOption option) =>
            Event(time, "eleccion", option == LevelUpOption.Gem ? "gema" : "tienda", null, level);

        void OnCardsRolled(double time, IReadOnlyList<string> ids)
        {
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] != null) Event(time, "oferta_carta", ids[i], null, i);
        }

        void OnCardBought(double time, string cardId, int price)
        {
            telemetry.OnCardBought(cardId);
            Event(time, "compra", cardId, null, price);
        }

        void OnShopRefreshed(double time, int price) => Event(time, "refresco", null, null, price);

        void OnCardRemoved(double time, string cardId, int price) => Event(time, "eliminacion", cardId, null, price);

        void OnGemsRolled(double time, IReadOnlyList<string> ids)
        {
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] != null) Event(time, "oferta_gema", ids[i], null, i);
        }

        void OnGemApplied(double time, string gemId, string cardId, string previousGemId) => Event(time, "gema", gemId, cardId, null);

        void OnGemsRefreshed(double time, int price) => Event(time, "refresco_gemas", null, null, price);

        void OnCardRepeated(double time, string cardId, string gemId) => telemetry.OnRepeat(cardId, gemId);

        void OnBossSpawned(double time, string bossId)
        {
            telemetry.OnBossSpawned(time, bossId);
            Event(time, "jefe_aparece", bossId, null, null);
        }

        void OnBossKilled(double time, string bossId)
        {
            telemetry.OnBossKilled(time, bossId);
            Event(time, "jefe_derrotado", bossId, null, null);
        }

        void OnBossRetreated(double time, string bossId)
        {
            telemetry.OnBossRetreated(time, bossId);
            Event(time, "jefe_huye", bossId, null, null);
        }

        void OnRareCardTaken(double time, string cardId) => Event(time, "rara", cardId, null, null);

        void OnCheatUsed(double time, string key)
        {
            telemetry.OnCheat();
            Event(time, "truco", key, null, null);
        }

        void OnEnemyDamaged(DamageSource source, float dealt, float overkill, bool killed) =>
            telemetry.OnDamage(in source, dealt, overkill, killed);

        void OnEnemySpawned(Enemy enemy) => telemetry.OnEnemySpawned(enemy.MaxHealth);

        void OnPlayerDamaged(float amount) => telemetry.OnPlayerDamaged(amount);

        void OnCardPlayed(int slot, CardInstance card)
        {
            if (card?.Definition == null) return;
            string gemId = card.Gem != null ? card.Gem.id : null;
            telemetry.OnCardPlayed(card.Definition.id, gemId, card.Cost);
            if (card.ExhaustOnPlay) exhausted.Add(RunTelemetry.DeckEntry(card.Definition.id, gemId));
        }

        void OnPlayFailed(int slot, PlayResult result) => telemetry.OnPlayFailed(result);
    }
}
