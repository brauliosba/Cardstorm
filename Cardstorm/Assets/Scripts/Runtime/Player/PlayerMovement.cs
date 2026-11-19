using Cardstorm.Combat;
using Cardstorm.Core;
using Cardstorm.Visuals;
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
        Vector2 dashVelocity;
        float dashTimer;

        // Lo que se dibuja durante el desplazamiento.
        public struct DashVisuals
        {
            public VfxManager Vfx;
            public ParticleSystem Ghost, Start, End;
        }

        // Dónde sale el polvo respecto al centro del jugador: a sus pies y un poco por detrás.
        static readonly Vector2 FeetOffset = new(0f, -0.3125f);
        const float StartBack = 0.25f;
        const float EndBack = 0.1875f;

        SpriteRenderer art;
        DashVisuals dashVisuals;
        Vector2 dashLast;
        float dashCarry;
        bool dashMirror;

        // Multiplicador de la carta Zancada (+10 % por copia).
        public float SpeedMultiplier { get; set; } = 1f;

        public float Speed => speed * SpeedMultiplier;

        // Último vector de movimiento no nulo: lo usan la cámara y Esquivar.
        public Vector2 LastMoveDirection { get; private set; } = Vector2.up;

        public Vector2 CurrentMove { get; private set; }

        public bool IsDashing => dashTimer > 0f;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            input = GetComponent<PlayerInputReader>();
            var motion = GetComponentInChildren<SpriteMotion>(true);
            if (motion != null) art = motion.GetComponent<SpriteRenderer>();

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

        // Desplazamiento rápido que ignora la entrada. Atraviesa a los enemigos, pero no los obstáculos.
        public void Dash(Vector2 direction, float distance, float duration, DashVisuals visuals = default)
        {
            if (distance <= 0f || duration <= 0f) return;
            if (direction.sqrMagnitude < 0.0001f) direction = LastMoveDirection;

            dashVelocity = direction.normalized * (distance / duration);
            dashTimer = duration;
            rb.excludeLayers = Layers.EnemyMask;

            // Los fantasmas miran hacia donde va; en vertical, hacia donde ya miraba.
            Vector2 dir = direction.normalized;
            dashMirror = Mathf.Abs(dir.x) > 0.01f ? dir.x < 0f : art != null && art.flipX;
            dashVisuals = visuals;
            dashLast = rb.position;
            dashCarry = 0f;
            if (visuals.Vfx == null) return;
            visuals.Vfx.Burst(visuals.Ghost, rb.position, mirror: dashMirror);
            visuals.Vfx.Burst(visuals.Start, rb.position - dir * StartBack + FeetOffset, dir, orient: true, mirror: dir.x < 0f);
        }

        // Estela desde el último paso y, al acabar, el polvo de la llegada.
        void DashTrail()
        {
            Vector2 position = rb.position;
            dashVisuals.Vfx.Trail(dashVisuals.Ghost, dashLast, position, ref dashCarry, dashMirror);
            dashLast = position;
            if (IsDashing) return;

            Vector2 dir = dashVelocity.normalized;
            dashVisuals.Vfx.Burst(dashVisuals.End, position - dir * EndBack + FeetOffset);
            dashVisuals = default;
        }

        void FixedUpdate()
        {
            if (dashVisuals.Vfx != null) DashTrail();

            if (IsDashing)
            {
                rb.linearVelocity = dashVelocity;
                dashTimer -= Time.fixedDeltaTime;
                if (dashTimer <= 0f)
                {
                    dashTimer = 0f;
                    rb.excludeLayers = 0;
                }
                return;
            }

            Vector2 target = CurrentMove * Speed;
            rb.linearVelocity = acceleration <= 0f
                ? target
                : Vector2.MoveTowards(rb.linearVelocity, target, acceleration * Time.fixedDeltaTime);
        }
    }
}
