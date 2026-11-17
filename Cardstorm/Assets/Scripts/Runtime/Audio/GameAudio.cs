using Cardstorm.Cards;
using Cardstorm.Core;
using Cardstorm.Enemies;
using Cardstorm.Player;
using Cardstorm.Progression;
using UnityEngine;

namespace Cardstorm.Audio
{
    // Decide qué efecto suena con cada suceso de la partida. Es lo único del sonido que conoce el juego.
    public class GameAudio : MonoBehaviour
    {
        [SerializeField] SfxPlayer player;
        [SerializeField] PlayerCards cards;
        [SerializeField] PlayerHealth health;
        [SerializeField] EnemyManager enemies;

        [Header("Cartas")]
        [SerializeField] SfxCue cardAttack;
        [SerializeField] SfxCue cardDefense;
        [SerializeField] SfxCue cardMobility;
        [SerializeField] SfxCue cardSummon;
        [SerializeField] SfxCue cardPower;
        [Tooltip("Sin energía suficiente o con la carta aún robándose.")]
        [SerializeField] SfxCue cardUnavailable;
        [SerializeField] SfxCue energyFull;

        [Header("Combate")]
        [SerializeField] SfxCue hit;
        [SerializeField] SfxCue enemyDeath;
        [SerializeField] SfxCue playerHurt;
        [SerializeField] SfxCue shieldBreak;
        [SerializeField] SfxCue bossWarning;
        [SerializeField] SfxCue bossSpawn;
        [SerializeField] SfxCue bossDefeated;

        [Header("Progreso")]
        [SerializeField] SfxCue pickupXp;
        [SerializeField] SfxCue pickupGold;
        [SerializeField] SfxCue levelUp;
        [SerializeField] SfxCue buy;
        [SerializeField] SfxCue victory;
        [SerializeField] SfxCue defeat;

        [Tooltip("Recogidas de experiencia seguidas (con menos de estos segundos entre una y otra) suben el tono.")]
        [SerializeField, Min(0f)] float xpStreakWindow = 0.4f;
        [SerializeField, Min(0f)] float xpPitchStep = 0.03f;
        [SerializeField, Min(1f)] float xpMaxPitch = 1.4f;

        bool wasFull = true;
        int xpStreak;
        float lastXpTime = -10f;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<SfxPlayer>();
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
        }

        void OnEnable()
        {
            if (cards != null)
            {
                cards.CardPlayed += OnCardPlayed;
                cards.PlayFailed += OnPlayFailed;
            }
            if (health != null)
            {
                health.Damaged += OnPlayerDamaged;
                health.ShieldBroken += OnShieldBroken;
                health.Died += OnDied;
            }
            if (enemies != null)
            {
                enemies.EnemyKilled += OnEnemyKilled;
                enemies.BossWindupStarted += OnBossWindup;
            }
            DamageEvents.EnemyDamaged += OnEnemyDamaged;
            RunEvents.XpGained += OnXp;
            RunEvents.GoldGained += OnGold;
            RunEvents.LevelUp += OnLevelUp;
            RunEvents.CardBought += OnCardBought;
            RunEvents.GemApplied += OnGemApplied;
            RunEvents.BossSpawned += OnBossSpawned;
            RunEvents.BossKilled += OnBossKilled;
            RunEvents.RunWon += OnRunWon;
        }

        void OnDisable()
        {
            if (cards != null)
            {
                cards.CardPlayed -= OnCardPlayed;
                cards.PlayFailed -= OnPlayFailed;
            }
            if (health != null)
            {
                health.Damaged -= OnPlayerDamaged;
                health.ShieldBroken -= OnShieldBroken;
                health.Died -= OnDied;
            }
            if (enemies != null)
            {
                enemies.EnemyKilled -= OnEnemyKilled;
                enemies.BossWindupStarted -= OnBossWindup;
            }
            DamageEvents.EnemyDamaged -= OnEnemyDamaged;
            RunEvents.XpGained -= OnXp;
            RunEvents.GoldGained -= OnGold;
            RunEvents.LevelUp -= OnLevelUp;
            RunEvents.CardBought -= OnCardBought;
            RunEvents.GemApplied -= OnGemApplied;
            RunEvents.BossSpawned -= OnBossSpawned;
            RunEvents.BossKilled -= OnBossKilled;
            RunEvents.RunWon -= OnRunWon;
        }

        // La energía no avisa al llenarse: se mira cada fotograma. Empieza llena, así que no suena al empezar.
        void Update()
        {
            var energy = cards != null ? cards.Energy : null;
            if (energy == null) return;
            bool full = energy.IsFull;
            if (full && !wasFull) Play(energyFull);
            wasFull = full;
        }

        void Play(SfxCue cue, float pitchScale = 1f)
        {
            if (player != null && cue != null) player.Play(cue, pitchScale);
        }

        void OnCardPlayed(int slot, CardInstance card)
        {
            if (card?.Definition == null) return;
            Play(card.Definition.type switch
            {
                CardType.Defense => cardDefense,
                CardType.Mobility => cardMobility,
                CardType.Summon => cardSummon,
                CardType.Power => cardPower,
                _ => cardAttack,
            });
        }

        void OnPlayFailed(int slot, PlayResult result)
        {
            if (result == PlayResult.NotEnoughEnergy || result == PlayResult.Drawing) Play(cardUnavailable);
        }

        // Si el golpe lo mata, ya suena la muerte.
        void OnEnemyDamaged(DamageSource source, float dealt, float overkill, bool killed)
        {
            if (!killed) Play(hit);
        }

        void OnEnemyKilled(Enemy enemy, Vector2 position) => Play(enemyDeath);

        void OnBossWindup(Enemy boss, BossPattern pattern) => Play(bossWarning);

        void OnPlayerDamaged(float amount) => Play(playerHurt);

        void OnShieldBroken() => Play(shieldBreak);

        void OnDied() => Play(defeat);

        void OnXp(double time, float amount)
        {
            float now = Time.unscaledTime;
            xpStreak = now - lastXpTime <= xpStreakWindow ? xpStreak + 1 : 0;
            lastXpTime = now;
            Play(pickupXp, Mathf.Min(1f + xpStreak * xpPitchStep, xpMaxPitch));
        }

        void OnGold(double time, int amount, GoldSource source) => Play(pickupGold);

        void OnLevelUp(double time, int level) => Play(levelUp);

        void OnCardBought(double time, string card, int price) => Play(buy);

        void OnGemApplied(double time, string gem, string card, string replaced) => Play(buy);

        void OnBossSpawned(double time, string boss) => Play(bossSpawn);

        void OnBossKilled(double time, string boss) => Play(bossDefeated);

        void OnRunWon(double time, int level) => Play(victory);
    }
}
