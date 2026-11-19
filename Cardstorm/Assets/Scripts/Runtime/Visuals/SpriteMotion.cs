using Cardstorm.Player;
using UnityEngine;

namespace Cardstorm.Visuals
{
    // Anima un sprite sin fotogramas.
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteMotion : MonoBehaviour
    {
        [Header("Andar")]
        [Tooltip("Unidades/s a las que la animación de andar está completa.")]
        [SerializeField] float referenceSpeed = 3f;
        [Tooltip("Largo de cada paso, en tamaños del sprite.")]
        [SerializeField] float stepLength = 0.7f;
        [Tooltip("Altura del rebote, en tamaños del sprite.")]
        [SerializeField] float bobHeight = 0.08f;
        [Tooltip("Grados de balanceo a cada lado.")]
        [SerializeField] float tiltAngle = 7f;

        [Header("Reposo")]
        [SerializeField] float breatheAmount = 0.04f;
        [Tooltip("Respiraciones por segundo.")]
        [SerializeField] float breatheRate = 0.8f;

        [Header("Flotar")]
        [Tooltip("Altura del vaivén, en tamaños del sprite.")]
        [SerializeField] float hoverHeight = 0.1f;
        [Tooltip("Vaivenes por segundo.")]
        [SerializeField] float hoverRate = 1.2f;
        [Tooltip("Grados que se inclina hacia donde va.")]
        [SerializeField] float leanAngle = 10f;

        [Header("Golpe")]
        [SerializeField] float squashStrength = 0.25f;
        [SerializeField] float squashDuration = 0.15f;

        [Tooltip("Velocidad horizontal mínima para darse la vuelta.")]
        [SerializeField] float flipDeadZone = 0.2f;

        // Rapidez con la que se pasa de quieto a andar, para que el ruido de la velocidad no tiemble.
        const float MoveBlend = 8f;

        SpriteRenderer spriteRenderer;
        PlayerHealth playerHealth;
        Vector3 basePosition;
        Vector3 baseScale;
        Vector3 lastPosition;
        float phase;
        float move;
        float breatheTime;
        float squashElapsed;
        float lean;

        // Flota en vez de andar: vaivén continuo e inclinación hacia donde va. Sin pasos ni respiración.
        public bool Floating { get; set; }

        Transform Anchor => transform.parent != null ? transform.parent : transform;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            basePosition = transform.localPosition;
            baseScale = transform.localScale;
            playerHealth = GetComponentInParent<PlayerHealth>();
        }

        // Viene del pool: se reinicia y cada uno respira a destiempo.
        void OnEnable()
        {
            lastPosition = Anchor.position;
            phase = 0f;
            move = 0f;
            lean = 0f;
            breatheTime = Random.value / Mathf.Max(breatheRate, 0.01f);
            squashElapsed = squashDuration;
            spriteRenderer.flipX = false;
            if (playerHealth != null) playerHealth.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (playerHealth != null) playerHealth.Damaged -= OnDamaged;
            transform.localPosition = basePosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = baseScale;
        }

        public void Punch() => squashElapsed = 0f;

        void OnDamaged(float amount) => Punch();

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 position = Anchor.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;

            // El tamaño lo da la escala del padre (los enemigos se escalan a su diámetro).
            float size = Mathf.Max(Anchor.lossyScale.y, 0.01f);
            float distance = delta.magnitude;
            float target = SpriteMotionMath.MoveAmount(distance / dt, referenceSpeed);
            move = Mathf.MoveTowards(move, target, MoveBlend * dt);
            phase = SpriteMotionMath.AdvancePhase(phase, distance, stepLength * size);
            breatheTime += dt;
            squashElapsed += dt;

            float velocityX = delta.x / dt;
            float leanTarget = SpriteMotionMath.Lean(velocityX, referenceSpeed, leanAngle);
            lean = Mathf.MoveTowards(lean, leanTarget, leanAngle * MoveBlend * dt);
            spriteRenderer.flipX = SpriteMotionMath.FacingLeft(spriteRenderer.flipX, velocityX, flipDeadZone);

            float squash = SpriteMotionMath.Squash(squashElapsed, squashDuration, squashStrength);
            float scaleX = 1f + squash;
            float scaleY = 1f - squash;
            if (!Floating) scaleY *= SpriteMotionMath.Breathe(breatheTime, breatheAmount, breatheRate, move);

            // Al aplastarse se mantienen los pies en el suelo.
            float height = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.y * baseScale.y : baseScale.y;
            float rise = Floating
                ? SpriteMotionMath.Hover(breatheTime, hoverHeight, hoverRate)
                : SpriteMotionMath.Bob(phase, bobHeight, move);
            float lift = rise * height + (scaleY - 1f) * height * 0.5f;
            float angle = Floating ? lean : SpriteMotionMath.Tilt(phase, tiltAngle, move);

            transform.localPosition = basePosition + new Vector3(0f, lift, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            transform.localScale = new Vector3(baseScale.x * scaleX, baseScale.y * scaleY, baseScale.z);
        }
    }
}
