using UnityEngine;

namespace Cardstorm.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerInputReader))]
    public class PlayerMovement : MonoBehaviour
    {
        [Tooltip("Velocidad base en unidades/s.")]
        [SerializeField] float speed = 5f;

        [Tooltip("Aceleración en unidades/s². 0 = arranque y parada instantáneos.")]
        [SerializeField] float acceleration = 0f;

        [Tooltip("Masa alta para que la horda (masa 1) no empuje al jugador, pero los muros sí lo detengan.")]
        [SerializeField] float mass = 100f;

        [Tooltip("Pivote del triángulo que indica la orientación.")]
        [SerializeField] Transform facingIndicator;

        Rigidbody2D rb;
        PlayerInputReader input;

        // Multiplicador de la carta Zancada (+10 % por copia).
        public float SpeedMultiplier { get; set; } = 1f;

        public float Speed => speed * SpeedMultiplier;

        // Último vector de movimiento no nulo: lo usan la cámara y Esquivar.
        public Vector2 LastMoveDirection { get; private set; } = Vector2.up;

        public Vector2 CurrentMove { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            input = GetComponent<PlayerInputReader>();

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.mass = mass;
            rb.linearDamping = 0f;
        }

        void Update()
        {
            CurrentMove = Vector2.ClampMagnitude(input.Move, 1f);
            if (CurrentMove.sqrMagnitude > 0.0001f)
                LastMoveDirection = CurrentMove.normalized;

            if (facingIndicator != null)
            {
                float angle = Mathf.Atan2(LastMoveDirection.y, LastMoveDirection.x) * Mathf.Rad2Deg - 90f;
                facingIndicator.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        void FixedUpdate()
        {
            Vector2 target = CurrentMove * Speed;
            rb.linearVelocity = acceleration <= 0f
                ? target
                : Vector2.MoveTowards(rb.linearVelocity, target, acceleration * Time.fixedDeltaTime);
        }
    }
}
