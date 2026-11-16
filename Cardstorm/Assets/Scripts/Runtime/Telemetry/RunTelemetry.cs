using System;
using System.Collections.Generic;
using System.Linq;
using Cardstorm.Cards;
using Cardstorm.Core;
using Cardstorm.Progression;

namespace Cardstorm.Telemetry
{
    public enum RunResult { Death, Victory, Abandoned }

    // Lo que se lee de la escena en cada muestra.
    public readonly struct SceneSample
    {
        public SceneSample(int level, int gold, float health, float maxHealth, float energy, float energyWasted, int aliveEnemies)
        {
            Level = level;
            Gold = gold;
            Health = health;
            MaxHealth = maxHealth;
            Energy = energy;
            EnergyWasted = energyWasted;
            AliveEnemies = aliveEnemies;
        }

        public int Level { get; }
        public int Gold { get; }
        public float Health { get; }
        public float MaxHealth { get; }
        public float Energy { get; }
        public float EnergyWasted { get; }
        public int AliveEnemies { get; }
    }

    // Datos de la partida que no cambian.
    public readonly struct RunInfo
    {
        public RunInfo(string version, string playerCode, int runNumber, string start, int seed)
        {
            Version = version;
            PlayerCode = playerCode;
            RunNumber = runNumber;
            Start = start;
            Seed = seed;
        }

        public string Version { get; }
        public string PlayerCode { get; }
        public int RunNumber { get; }
        public string Start { get; }
        public int Seed { get; }
    }

    // Contadores de una partida y las filas de los CSV. Sin Unity: lo alimenta TelemetryRecorder.
    public class RunTelemetry
    {
        public const string MinibossId = "heraldo";
        public const string FinalBossId = "ojo_tormenta";
        public const string GreedId = "codicia";
        public const string ScholarId = "erudicion";

        public const string SummaryHeader =
            "version;codigo;partida;inicio;semilla;trucos;resultado;t_fin_s;t_real_s;nivel;xp_total;" +
            "oro_obtenido;oro_enemigos;oro_cofres;oro_jefes;oro_gastado;oro_final;t_primera_subida_s;" +
            "energia_desperdiciada;seg_energia_llena;frac_energia_llena;cartas_jugadas;jugadas_fallidas;causa_muerte;" +
            "heraldo_aparece_s;heraldo_fin_s;heraldo_resultado;jefe_final_aparece_s;jefe_final_fin_s;jefe_final_resultado;" +
            "codicia;erudicion;mazo_final";

        public const string EventsHeader = "t_s;evento;id;id2;valor";

        public const string SamplesHeader =
            "t_s;nivel;xp_total;oro_obtenido;oro_gastado;oro;vida;vida_max;energia;energia_desperdiciada;seg_energia_llena;" +
            "enemigos_vivos;vida_entrante;dano_infligido;dano_sobrante;dano_recibido;cartas_jugadas";

        public const string CardsHeader = "carta;gema;jugadas;energia;repeticiones;dano;dano_sobrante;muertes";

        class CardStats
        {
            public int Plays;
            public double Energy;
            public int Repeats;
            public double Damage;
            public double Overkill;
            public int Kills;
        }

        class BossRecord
        {
            public double Spawned;
            public double? Ended;
            public string Outcome;
        }

        readonly Dictionary<DamageSource, CardStats> cards = new();
        readonly Dictionary<string, BossRecord> bosses = new(StringComparer.Ordinal);
        readonly Dictionary<string, int> bought = new(StringComparer.Ordinal);

        double xpTotal;
        int goldEnemies, goldChests, goldBosses, goldSpent;
        double? firstLevelUp;
        int cardsPlayed, playsFailed;
        double incomingHealth, damageDealt, overkill, damageTaken;

        public bool CheatsUsed { get; private set; }
        public double EnergyFullSeconds { get; private set; }
        public int GoldEarned => goldEnemies + goldChests + goldBosses;

        public void OnXp(float xp) => xpTotal += xp;

        // El oro de depuración no cuenta como obtenido.
        public void OnGold(int amount, GoldSource source)
        {
            switch (source)
            {
                case GoldSource.Enemy: goldEnemies += amount; break;
                case GoldSource.Chest: goldChests += amount; break;
                case GoldSource.Boss: goldBosses += amount; break;
            }
        }

        public void OnGoldSpent(int amount) => goldSpent += amount;

        public void OnLevelUp(double time, int level) => firstLevelUp ??= time;

        public void OnCardPlayed(string cardId, string gemId, float cost)
        {
            var stats = Stats(new DamageSource(cardId, gemId));
            stats.Plays++;
            stats.Energy += cost;
            cardsPlayed++;
        }

        // Sin carta en la ranura no cuenta: no es un intento fallido.
        public void OnPlayFailed(PlayResult result)
        {
            if (result == PlayResult.NotEnoughEnergy || result == PlayResult.Drawing) playsFailed++;
        }

        public void OnRepeat(string cardId, string gemId) => Stats(new DamageSource(cardId, gemId)).Repeats++;

        public void OnDamage(in DamageSource source, float dealt, float overkillAmount, bool killed)
        {
            if (source.CardId == null) return;

            var stats = Stats(source);
            stats.Damage += dealt;
            stats.Overkill += overkillAmount;
            if (killed) stats.Kills++;
            damageDealt += dealt;
            overkill += overkillAmount;
        }

        public void OnEnemySpawned(float maxHealth) => incomingHealth += maxHealth;

        public void OnPlayerDamaged(float amount) => damageTaken += amount;

        public void OnBossSpawned(double time, string bossId) => bosses[bossId ?? ""] = new BossRecord { Spawned = time, Outcome = "vivo" };

        public void OnBossKilled(double time, string bossId) => EndBoss(time, bossId, "derrotado");

        public void OnBossRetreated(double time, string bossId) => EndBoss(time, bossId, "huye");

        public void OnCardBought(string cardId)
        {
            if (cardId == null) return;
            bought.TryGetValue(cardId, out int copies);
            bought[cardId] = copies + 1;
        }

        public void OnCheat() => CheatsUsed = true;

        // dt de partida: 0 en pausa.
        public void TickEnergy(float dt, bool isFull)
        {
            if (dt > 0f && isFull) EnergyFullSeconds += dt;
        }

        public static string DeckEntry(string cardId, string gemId) => gemId != null ? cardId + "+" + gemId : cardId;

        public static string EventLine(double time, string evento, string id, string id2, double? value) =>
            Csv.Line(Csv.Num(time), evento, id, id2, value.HasValue ? Csv.Num(value.Value) : null);

        public string SampleLine(double time, in SceneSample s) => Csv.Line(
            Csv.Num(time), Csv.Num(s.Level), Csv.Num(xpTotal), Csv.Num(GoldEarned), Csv.Num(goldSpent), Csv.Num(s.Gold),
            Csv.Num(s.Health), Csv.Num(s.MaxHealth), Csv.Num(s.Energy), Csv.Num(s.EnergyWasted), Csv.Num(EnergyFullSeconds),
            Csv.Num(s.AliveEnemies), Csv.Num(incomingHealth), Csv.Num(damageDealt), Csv.Num(overkill), Csv.Num(damageTaken),
            Csv.Num(cardsPlayed));

        // Ordenadas por carta y gema.
        public IEnumerable<string> CardLines() => cards
            .OrderBy(c => c.Key.CardId, StringComparer.Ordinal)
            .ThenBy(c => c.Key.GemId ?? "", StringComparer.Ordinal)
            .Select(c => Csv.Line(c.Key.CardId, c.Key.GemId, Csv.Num(c.Value.Plays), Csv.Num(c.Value.Energy), Csv.Num(c.Value.Repeats),
                Csv.Num(c.Value.Damage), Csv.Num(c.Value.Overkill), Csv.Num(c.Value.Kills)));

        public string SummaryLine(in RunInfo info, RunResult result, double endTime, double realTime, in SceneSample final,
            string deathCause, IEnumerable<string> finalDeck)
        {
            var deck = finalDeck != null ? finalDeck.Where(d => d != null).OrderBy(d => d, StringComparer.Ordinal) : Enumerable.Empty<string>();
            bosses.TryGetValue(MinibossId, out var miniboss);
            bosses.TryGetValue(FinalBossId, out var finalBoss);
            bought.TryGetValue(GreedId, out int greed);
            bought.TryGetValue(ScholarId, out int scholar);

            return Csv.Line(
                info.Version, info.PlayerCode, Csv.Num(info.RunNumber), info.Start, Csv.Num(info.Seed),
                CheatsUsed ? "si" : "no", ResultText(result), Csv.Num(endTime), Csv.Num(realTime),
                Csv.Num(final.Level), Csv.Num(xpTotal),
                Csv.Num(GoldEarned), Csv.Num(goldEnemies), Csv.Num(goldChests), Csv.Num(goldBosses), Csv.Num(goldSpent), Csv.Num(final.Gold),
                firstLevelUp.HasValue ? Csv.Num(firstLevelUp.Value) : null,
                Csv.Num(final.EnergyWasted), Csv.Num(EnergyFullSeconds), Csv.Num(endTime > 0 ? EnergyFullSeconds / endTime : 0),
                Csv.Num(cardsPlayed), Csv.Num(playsFailed), deathCause,
                miniboss != null ? Csv.Num(miniboss.Spawned) : null, Ended(miniboss), miniboss?.Outcome,
                finalBoss != null ? Csv.Num(finalBoss.Spawned) : null, Ended(finalBoss), finalBoss?.Outcome,
                Csv.Num(greed), Csv.Num(scholar), string.Join("|", deck));
        }

        CardStats Stats(DamageSource key)
        {
            if (!cards.TryGetValue(key, out var stats))
            {
                stats = new CardStats();
                cards.Add(key, stats);
            }
            return stats;
        }

        void EndBoss(double time, string bossId, string outcome)
        {
            if (!bosses.TryGetValue(bossId ?? "", out var record)) return;
            record.Ended = time;
            record.Outcome = outcome;
        }

        static string Ended(BossRecord record) => record?.Ended != null ? Csv.Num(record.Ended.Value) : null;

        static string ResultText(RunResult result) => result switch
        {
            RunResult.Victory => "victoria",
            RunResult.Abandoned => "abandono",
            _ => "muerte",
        };
    }
}
