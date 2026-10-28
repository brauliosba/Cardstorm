using Cardstorm.Core;
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

        Rigidbody2D rb;
        SpriteRenderer spriteRenderer;

        public EnemyDefinition Definition { get; private set; }
        public Rigidbody2D Body => rb;
        public float Speed { get; private set; }
        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public bool IsAlive { get; private set; }

        // La experiencia se multiplica por el refuerzo del tramo; el oro no.
        public float XpMultiplier { get; private set; } = 1f;

        public float ContactDamage => Definition != null ? Definition.contactDamage : 0f;

        // Estado que gestiona EnemyManager.
        internal EnemyManager Manager { get; set; }
        internal int ActiveIndex { get; set; } = -1;
        internal float FarTimer { get; set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            GetComponent<CircleCollider2D>().radius = 0.5f;  // El sprite mide 1 unidad; el tamaño real lo da la escala.
        }

        // Reinicia todo el estado: la instancia viene del pool y puede haber sido otro tipo de enemigo.
        public void Activate(EnemyDefinition definition, Vector2 position, float reinforcement)
        {
            Definition = definition;
            Speed = definition.moveSpeed;
            MaxHealth = definition.maxHealth * reinforcement;
            Health = MaxHealth;
            XpMultiplier = reinforcement;
            IsAlive = true;
            FarTimer = 0f;

            float diameter = definition.radius * 2f;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = new Vector3(diameter, diameter, 1f);
            spriteRenderer.sprite = SpriteFor(definition.shape);
            spriteRenderer.color = definition.color;

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
            gameObject.SetActive(false);
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            Health -= amount;
            if (Health <= 0f)
            {
                Health = 0f;
                IsAlive = false;
                Manager?.NotifyKilled(this);
            }
        }

        Sprite SpriteFor(EnemyShape shape) => shape switch
        {
            EnemyShape.Square => squareSprite,
            EnemyShape.Triangle => triangleSprite,
            _ => circleSprite,
        };
    }
}
