using Cardstorm.Core;
using Cardstorm.Visuals;
using UnityEngine;

namespace Cardstorm.Enemies
{
    // Enemigo genérico: un único prefab que se configura con su EnemyDefinition al salir del pool.
    // El movimiento lo aplica EnemyManager.
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
    public class Enemy : MonoBehaviour, IContactDamageSource
    {
        [Header("Sprites por forma")]
        [SerializeField] Sprite circleSprite;
        [SerializeField] Sprite squareSprite;
        [SerializeField] Sprite triangleSprite;
        [SerializeField] Sprite diamondSprite;
        [SerializeField] Sprite ringSprite;

        [Tooltip("Franja que marca la trayectoria de la embestida. Hijo desactivado del prefab.")]
        [SerializeField] SpriteRenderer chargeTelegraph;

        [Tooltip("Hijo con el sprite del enemigo. Se usa si la definición tiene sprite; si no, la forma.")]
        [SerializeField] SpriteRenderer art;

        [Tooltip("Material del sprite mientras está ralentizado. Vacío = tinte azul.")]
        [SerializeField] Material frozenMaterial;

        const float HitFlashTime = 0.08f;
        const float TelegraphMinAlpha = 0.15f;
        const float TelegraphMaxAlpha = 0.5f;
        const float SlowTintAmount = 0.4f;
        static readonly Color SlowTint = new(0.604f, 0.843f, 1f);  // #9AD7FF
        // El blanco no se ve sobre un sprite: el golpe y el aviso lo tiñen de rojo.
        static readonly Color ArtHitTint = new(1f, 0.35f, 0.35f);

        Rigidbody2D rb;
        SpriteRenderer spriteRenderer;
        SpriteMotion artMotion;
        CircleCollider2D circle;
        Slow slow;

        public EnemyDefinition Definition { get; private set; }
        public Rigidbody2D Body => rb;
        public float Speed { get; private set; }
        public float SlowFactor => slow.Factor;
        public float CurrentSpeed => Speed * slow.Factor;
        public bool IsSlowed => slow.Active;
        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public bool IsAlive { get; private set; }
        // Cuenta las salidas del pool: distingue esta vida de la instancia de las anteriores.
        public int Spawns { get; private set; }
        public bool IsBoss => Definition != null && Definition.isBoss;

        // Huye sin recibir ni hacer daño y se desvanece. Al terminar, EnemyManager lo recicla.
        public bool IsRetreating { get; private set; }
        public bool RetreatFinished => IsRetreating && RetreatTimer >= RetreatDuration;

        // La experiencia se multiplica por el refuerzo del tramo; el oro no.
        public float XpMultiplier { get; private set; } = 1f;

        public float ContactDamage => Definition != null && !IsRetreating ? Definition.contactDamage : 0f;

        public string SourceId => Definition != null ? Definition.id : null;

        public float Radius => Definition != null ? Definition.radius : 0.5f;

        // Estado que gestiona EnemyManager.
        internal EnemyManager Manager { get; set; }
        internal int ActiveIndex { get; set; } = -1;
        internal float FarTimer { get; set; }

        // Segundos hasta el siguiente disparo (KeepDistance).
        internal float FireTimer { get; set; }

        // Tiempo que queda del destello blanco al recibir daño.
        internal float FlashTimer { get; set; }

        // Embestida (Charge). La dirección se fija al empezar el aviso.
        internal ChargeLogic Charge { get; private set; }
        internal Vector2 ChargeDirection { get; set; }

        // Patrones (Patterns). RingTurn es el giro en radianes del siguiente anillo.
        internal BossPatternLogic Patterns { get; private set; }
        internal float RingTurn { get; set; }

        internal float RetreatTimer { get; set; }
        internal float RetreatDuration { get; private set; }
        internal float RetreatSpeed { get; private set; }

        // Opacidad del sprite, de 1 a 0 durante la retirada.
        float fade = 1f;

        // El que se ve: la forma del greybox o el sprite de la definición.
        SpriteRenderer view;
        bool usesArt;
        Material artMaterial;

        void Awake()
        {
            Charge = new ChargeLogic();
            Patterns = new BossPatternLogic();
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            view = spriteRenderer;
            if (art != null)
            {
                artMotion = art.GetComponent<SpriteMotion>();
                artMaterial = art.sharedMaterial;
            }

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            circle = GetComponent<CircleCollider2D>();
            circle.radius = 0.5f;  // El sprite mide 1 unidad; el tamaño real lo da la escala.
        }

        // Reinicia todo el estado: la instancia viene del pool y puede haber sido otro tipo de enemigo.
        public void Activate(EnemyDefinition definition, Vector2 position, float reinforcement)
        {
            Definition = definition;
            Spawns++;
            Speed = definition.moveSpeed;
            MaxHealth = definition.maxHealth * reinforcement;
            Health = MaxHealth;
            XpMultiplier = reinforcement;
            IsAlive = true;
            FarTimer = 0f;
            FlashTimer = 0f;
            slow = default;
            // Desfase aleatorio para que los tiradores de una misma oleada no disparen a la vez.
            FireTimer = definition.fireInterval * Random.Range(0.5f, 1.5f);
            Charge.Reset();
            ChargeDirection = Vector2.zero;
            HideChargeTelegraph();
            Patterns.Reset(definition.PatternSettings);
            RingTurn = 0f;
            IsRetreating = false;
            RetreatTimer = 0f;
            fade = 1f;

            float diameter = definition.radius * 2f;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = new Vector3(diameter, diameter, 1f);
            usesArt = art != null && definition.sprite != null;
            spriteRenderer.enabled = !usesArt;
            if (art != null)
            {
                art.gameObject.SetActive(usesArt);
                if (usesArt) art.sprite = definition.sprite;
            }
            if (!usesArt) spriteRenderer.sprite = SpriteFor(definition.shape);
            if (artMotion != null) artMotion.Floating = definition.floats;
            view = usesArt ? art : spriteRenderer;
            ApplyFrozen();
            view.color = BaseColor;

            // Los jefes son demasiado grandes para el flow field: atraviesan los obstáculos.
            circle.excludeLayers = definition.isBoss ? Layers.ObstacleMask : 0;

            rb.mass = definition.mass;
            rb.position = position;
            rb.linearVelocity = Vector2.zero;

            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            IsAlive = false;
            Manager = null;
            ActiveIndex = -1;
            rb.linearVelocity = Vector2.zero;
            HideChargeTelegraph();
            gameObject.SetActive(false);
        }

        public void BeginRetreat(float duration, float speed)
        {
            if (!IsAlive || IsRetreating) return;

            IsRetreating = true;
            RetreatTimer = 0f;
            RetreatDuration = Mathf.Max(duration, 0.01f);
            RetreatSpeed = speed;
            Patterns.BeginRetreat();
            Charge.Reset();
            HideChargeTelegraph();
            RestoreColor();
        }

        internal void SetFade(float alpha)
        {
            fade = Mathf.Clamp01(alpha);
            ApplyBaseColor();
        }

        public void TakeDamage(float amount, in DamageSource source)
        {
            if (!IsAlive || IsRetreating || amount <= 0f) return;

            float dealt = Mathf.Min(amount, Health);
            Health -= amount;
            bool killed = Health <= 0f;
            DamageEvents.RaiseEnemyDamaged(in source, dealt, amount - dealt, killed);

            if (killed)
            {
                Health = 0f;
                IsAlive = false;
                Manager?.NotifyKilled(this);
                return;
            }

            // Con menos destellos, el golpe solo aclara el color.
            float flash = GameSettings.Current.FlashScale;
            Color hit = usesArt ? ArtHitTint : Color.white;
            view.color = flash >= 1f || Definition == null ? hit : Color.Lerp(BaseColor, hit, flash);
            FlashTimer = HitFlashTime;
            if (usesArt && artMotion != null) artMotion.Punch();
        }

        // Los jefes se ralentizan a la mitad.
        public void ApplySlow(float factor, float duration)
        {
            if (!IsAlive) return;
            if (IsBoss) factor = Slow.ForBoss(factor);

            bool wasSlowed = slow.Active;
            slow.Apply(factor, duration);
            if (!wasSlowed && slow.Active)
            {
                ApplyFrozen();
                ApplyBaseColor();
            }
        }

        internal void TickSlow(float dt)
        {
            if (!slow.Active) return;
            slow.Tick(dt);
            if (!slow.Active)
            {
                ApplyFrozen();
                ApplyBaseColor();
            }
        }

        // Con sprite y material de congelado, el material sustituye al tinte azul.
        bool UsesFrozenMaterial => usesArt && frozenMaterial != null;

        void ApplyFrozen()
        {
            if (art == null || artMaterial == null) return;
            art.sharedMaterial = UsesFrozenMaterial && slow.Active ? frozenMaterial : artMaterial;
        }

        internal void RestoreColor()
        {
            FlashTimer = 0f;
            ApplyBaseColor();
        }

        // Parpadeo del aviso. El destello de daño tiene prioridad.
        internal void SetBlink(bool white)
        {
            if (FlashTimer > 0f || Definition == null) return;
            Color blink = usesArt ? ArtHitTint : Color.white;
            blink.a = fade;
            view.color = white ? blink : BaseColor;
        }

        // Color de la definición (blanco con sprite), teñido de azul mientras está ralentizado si no hay material de congelado.
        Color BaseColor
        {
            get
            {
                Color tint = usesArt ? Color.white : Definition.color;
                Color c = slow.Active && !UsesFrozenMaterial ? Color.Lerp(tint, SlowTint, SlowTintAmount) : tint;
                c.a *= fade;
                return c;
            }
        }

        void ApplyBaseColor()
        {
            if (FlashTimer > 0f || Definition == null) return;
            view.color = BaseColor;
        }

        // Franja de largo length y ancho igual al diámetro, desde el centro hacia dir.
        // El hijo hereda la escala del enemigo: se compensa dividiendo por el diámetro.
        internal void ShowChargeTelegraph(Vector2 dir, float length, float progress)
        {
            if (chargeTelegraph == null || Definition == null) return;

            float diameter = Definition.radius * 2f;
            var t = chargeTelegraph.transform;
            t.localPosition = dir * (length * 0.5f / diameter);
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(length / diameter, 1f, 1f);

            Color c = Definition.color;
            c.a = Mathf.Lerp(TelegraphMinAlpha, TelegraphMaxAlpha, progress);
            chargeTelegraph.color = c;
            if (!chargeTelegraph.gameObject.activeSelf) chargeTelegraph.gameObject.SetActive(true);
        }

        internal void HideChargeTelegraph()
        {
            if (chargeTelegraph != null && chargeTelegraph.gameObject.activeSelf)
                chargeTelegraph.gameObject.SetActive(false);
        }

        Sprite SpriteFor(EnemyShape shape) => shape switch
        {
            EnemyShape.Square => squareSprite,
            EnemyShape.Triangle => triangleSprite,
            EnemyShape.Diamond => diamondSprite,
            EnemyShape.Ring => ringSprite,
            _ => circleSprite,
        };
    }
}
