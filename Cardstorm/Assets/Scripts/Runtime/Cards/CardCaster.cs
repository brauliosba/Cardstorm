using System.Collections.Generic;
using Cardstorm.Combat;
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

        [Tooltip("Radio con el que se cuentan los vecinos al buscar la zona más densa.")]
        [Min(0.1f)] [SerializeField] float densityRadius = 2f;

        readonly List<Vector2> positions = new(TargetBufferCapacity);
        readonly List<float> healths = new(TargetBufferCapacity);
        Rigidbody2D rb;
        PlayerStatsBinder statsBinder;

        public PlayerMovement Movement { get; private set; }
        public PlayerHealth Health { get; private set; }
        public EnemyManager Enemies => enemies;
        public ProjectileManager Projectiles => projectiles;
        public ArcFlash ArcFlash => arcFlash;
        public StrikeManager Strikes => strikes;
        public FlashManager Flashes => flashes;

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
        }

        public void Cast(CardDefinition card)
        {
            if (card == null) return;

            Vector2 origin = rb.position;
            Enemy target = card.targeting == TargetPriority.MoveDirection ? null : FindTarget(card, origin);

            Vector2 aim = Movement.LastMoveDirection;
            if (target != null)
            {
                Vector2 to = target.Body.position - origin;
                if (to.sqrMagnitude > 1e-6f) aim = to.normalized;
            }

            Vector2 targetPoint = target != null ? target.Body.position : origin + aim * card.targetRange;
            var context = new CardContext(this, card, origin, aim, target, targetPoint);

            var effects = card.effects;
            for (int i = 0; i < effects.Count; i++)
                effects[i]?.Apply(in context);

#if UNITY_EDITOR
            Debug.DrawLine(origin, targetPoint, target != null ? Color.yellow : Color.cyan, 0.5f);
#endif
        }

        Enemy FindTarget(CardDefinition card, Vector2 origin)
        {
            if (enemies == null) return null;

            var active = enemies.Active;
            positions.Clear();
            healths.Clear();
            bool needsHealth = card.targeting == TargetPriority.HighestHealth;
            for (int i = 0; i < active.Count; i++)
            {
                positions.Add(active[i].Body.position);
                if (needsHealth) healths.Add(active[i].Health);
            }

            int index = card.targeting switch
            {
                TargetPriority.HighestHealth => Targeting.HighestHealth(origin, positions, healths, card.targetRange),
                TargetPriority.DensestArea => Targeting.Densest(origin, positions, card.targetRange, densityRadius),
                _ => Targeting.Nearest(origin, positions, card.targetRange),
            };
            return index >= 0 ? active[index] : null;
        }
    }
}
