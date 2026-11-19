using System.Collections.Generic;
using Cardstorm.Combat;
using Cardstorm.Core;
using Cardstorm.Enemies;
using Cardstorm.Player;
using Cardstorm.Progression;
using UnityEngine;

namespace Cardstorm.Cards
{
    // Lanza las cartas del jugador: elige el objetivo según su prioridad y aplica sus efectos.
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerMovement), typeof(PlayerHealth))]
    public class CardCaster : MonoBehaviour
    {
        // Capacidad inicial de los búferes de objetivos; cubre el máximo de enemigos vivos.
        const int TargetBufferCapacity = 600;

        [SerializeField] EnemyManager enemies;
        [SerializeField] ProjectileManager projectiles;
        [SerializeField] ArcFlash arcFlash;
        [Tooltip("Impactos con aviso. Si se deja vacío, se busca en la escena.")]
        [SerializeField] StrikeManager strikes;
        [Tooltip("Destellos en el mundo. Si se deja vacío, se busca en la escena.")]
        [SerializeField] FlashManager flashes;
        [Tooltip("Torretas. Si se deja vacío, se busca en la escena.")]
        [SerializeField] TurretManager turrets;
        [Tooltip("Partículas de las cartas. Si se deja vacío, se busca en la escena.")]
        [SerializeField] VfxManager vfx;

        [Tooltip("Radio con el que se cuentan los vecinos al buscar la zona más densa.")]
        [Min(0.1f)] [SerializeField] float densityRadius = 2f;

        [Tooltip("Radio del destello en el jugador cuando una gema repite la carta.")]
        [Min(0f)] [SerializeField] float repeatFlashRadius = 0.8f;

        [Tooltip("Da la semilla del azar de las gemas. Si se deja vacío, se busca en la escena.")]
        [SerializeField] RunManager run;

        readonly List<Vector2> positions = new(TargetBufferCapacity);
        readonly List<float> healths = new(TargetBufferCapacity);
        Rigidbody2D rb;
        PlayerStatsBinder statsBinder;
        System.Random random;

        public PlayerMovement Movement { get; private set; }
        public PlayerHealth Health { get; private set; }
        public EnemyManager Enemies => enemies;
        public ProjectileManager Projectiles => projectiles;
        public ArcFlash ArcFlash => arcFlash;
        public StrikeManager Strikes => strikes;
        public FlashManager Flashes => flashes;
        public TurretManager Turrets => turrets;
        public VfxManager Vfx => vfx;

        // null si el jugador no tiene PlayerStatsBinder.
        public PlayerStats Stats => statsBinder != null ? statsBinder.Stats : null;

        // Multiplicador de daño de todas las cartas (Furia).
        public float DamageMultiplier { get; set; } = 1f;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            Movement = GetComponent<PlayerMovement>();
            Health = GetComponent<PlayerHealth>();
            statsBinder = GetComponent<PlayerStatsBinder>();
            if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
            if (projectiles == null) projectiles = FindFirstObjectByType<ProjectileManager>();
            if (arcFlash == null) arcFlash = GetComponentInChildren<ArcFlash>(true);
            if (strikes == null) strikes = FindFirstObjectByType<StrikeManager>();
            if (flashes == null) flashes = FindFirstObjectByType<FlashManager>();
            if (turrets == null) turrets = FindFirstObjectByType<TurretManager>();
            if (vfx == null) vfx = FindFirstObjectByType<VfxManager>();
            if (run == null) run = FindFirstObjectByType<RunManager>();
        }

        // Se crea al primer uso: RunManager fija la semilla en su Start.
        System.Random Random => random ??= new System.Random(run != null && run.Seed != 0
            ? unchecked(run.Seed * 31 + 7)
            : System.Environment.TickCount);

        // Sin gema. Para la depuración.
        public void Cast(CardDefinition card)
        {
            if (card == null) return;
            CastOnce(card, card.targeting, GemModifiers.None, null);
        }

        // La copia lleva la gema: su prioridad y sus modificadores pasan a los efectos.
        public void Cast(CardInstance card)
        {
            if (card == null || card.Definition == null) return;

            var modifiers = card.Modifiers;
            var targeting = card.Targeting;
            CastOnce(card.Definition, targeting, modifiers, card.Gem);

            // Resonancia: otra vez, con objetivo nuevo y sin gastar energía.
            if (modifiers.repeatChance > 0f && GemRules.Repeats(modifiers, Random.NextDouble()))
            {
                CastOnce(card.Definition, targeting, modifiers, card.Gem);
                if (flashes != null && card.Gem != null) flashes.Area(rb.position, repeatFlashRadius, card.Gem.color);
                RunEvents.RaiseCardRepeated(run != null ? run.Now : Time.timeSinceLevelLoad, card.Definition.id, card.Gem != null ? card.Gem.id : null);
            }
        }

        void CastOnce(CardDefinition card, TargetPriority targeting, in GemModifiers modifiers, GemDefinition gem)
        {
            Vector2 origin = rb.position;
            Enemy target = targeting == TargetPriority.MoveDirection ? null : FindTarget(card, targeting, origin);

            Vector2 aim = Movement.LastMoveDirection;
            if (target != null)
            {
                Vector2 to = target.Body.position - origin;
                if (to.sqrMagnitude > 1e-6f) aim = to.normalized;
            }

            Vector2 targetPoint = target != null ? target.Body.position : origin + aim * card.targetRange;
            var source = new DamageSource(card.id, gem != null ? gem.id : null);
            var context = new CardContext(this, card, in modifiers, in source, origin, aim, target, targetPoint);

            var effects = card.effects;
            for (int i = 0; i < effects.Count; i++)
                effects[i]?.Apply(in context);

#if UNITY_EDITOR
            Debug.DrawLine(origin, targetPoint, target != null ? Color.yellow : Color.cyan, 0.5f);
#endif
        }

        Enemy FindTarget(CardDefinition card, TargetPriority targeting, Vector2 origin)
        {
            if (enemies == null) return null;

            var active = enemies.Active;
            positions.Clear();
            healths.Clear();
            bool needsHealth = targeting == TargetPriority.HighestHealth;
            for (int i = 0; i < active.Count; i++)
            {
                positions.Add(active[i].Body.position);
                if (needsHealth) healths.Add(active[i].Health);
            }

            int index = targeting switch
            {
                TargetPriority.HighestHealth => Targeting.HighestHealth(origin, positions, healths, card.targetRange),
                TargetPriority.DensestArea => Targeting.Densest(origin, positions, card.targetRange, densityRadius),
                _ => Targeting.Nearest(origin, positions, card.targetRange),
            };
            return index >= 0 ? active[index] : null;
        }
    }
}
