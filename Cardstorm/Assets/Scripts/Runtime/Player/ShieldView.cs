using Cardstorm.Cards;
using UnityEngine;

namespace Cardstorm.Player
{
    // Anillo del escudo. Va como hijo del jugador y solo toca la opacidad, la escala y el frame:
    // PlayerHealth enciende y apaga los sprites del jugador para el parpadeo de invulnerabilidad.
    [RequireComponent(typeof(SpriteRenderer))]
    public class ShieldView : MonoBehaviour
    {
        [SerializeField] PlayerHealth health;

        [Tooltip("Frames del anillo en bucle, con sus colores. Vacío = el sprite del renderer teñido y escalado al radio.")]
        [SerializeField] Sprite[] frames;

        [SerializeField, Min(0.1f)] float frameRate = 10f;

        [Tooltip("Radio del anillo en unidades del mundo (solo sin frames).")]
        [SerializeField, Min(0.1f)] float radius = 0.8f;

        [Tooltip("Opacidad con el escudo entero.")]
        [SerializeField, Range(0f, 1f)] float maxAlpha = 0.9f;

        [Tooltip("Opacidad mínima mientras queda escudo.")]
        [SerializeField, Range(0f, 1f)] float minAlpha = 0.3f;

        [Tooltip("Segundos finales en los que parpadea antes de apagarse.")]
        [SerializeField, Min(0f)] float warningTime = 1f;

        [SerializeField, Min(0.01f)] float blinkInterval = 0.1f;

        [Header("Aparición")]
        [SerializeField] ParticleSystem appearVfx;

        [Tooltip("Segundos sin anillo mientras se cierra la aparición.")]
        [SerializeField, Min(0f)] float appearHide = 0.133f;

        [Header("Golpe absorbido")]
        [SerializeField] ParticleSystem hitVfx;

        [Tooltip("Segundos sin anillo mientras destella el golpe.")]
        [SerializeField, Min(0f)] float hitHide = 0.067f;

        [SerializeField, Min(0.01f)] float pulseDuration = 0.15f;

        [Tooltip("Escala extra del anillo al empezar el pulso (solo sin frames).")]
        [SerializeField, Min(0f)] float pulseScale = 0.25f;

        [Header("Escudo roto")]
        [Tooltip("Vacío = destello circular de FlashManager.")]
        [SerializeField] ParticleSystem breakVfx;

        [Tooltip("Radio del destello al romperse.")]
        [SerializeField, Min(0f)] float breakFlashRadius = 1.2f;

        SpriteRenderer spriteRenderer;
        CardCaster caster;
        float baseScale;
        float pulseTimer;
        float hideTimer;
        float frameTime;

        bool HasFrames => frames != null && frames.Length > 0;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (health == null) health = GetComponentInParent<PlayerHealth>();
            caster = GetComponentInParent<CardCaster>();

            // El jugador está escalado: se compensa para que el radio sea el del mundo.
            // Con frames, el sprite va a su tamaño para que siga nítido.
            float parentScale = transform.parent != null ? Mathf.Max(transform.parent.lossyScale.x, 1e-4f) : 1f;
            if (HasFrames)
            {
                spriteRenderer.sprite = frames[0];
                baseScale = 1f / parentScale;
            }
            else
            {
                float spriteSize = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.x : 1f;
                baseScale = 2f * radius / (spriteSize * parentScale);
            }
            SetScale(1f);
            SetAlpha(0f);
        }

        void OnEnable()
        {
            if (health == null) return;
            health.ShieldGranted += OnShieldGranted;
            health.ShieldHit += OnShieldHit;
            health.ShieldBroken += OnShieldBroken;
        }

        void OnDisable()
        {
            if (health == null) return;
            health.ShieldGranted -= OnShieldGranted;
            health.ShieldHit -= OnShieldHit;
            health.ShieldBroken -= OnShieldBroken;
        }

        void OnShieldGranted()
        {
            if (appearVfx == null || Vfx == null) return;
            Vfx.Burst(appearVfx, transform.position);
            hideTimer = appearHide;
            frameTime = 0f;
        }

        void OnShieldHit()
        {
            pulseTimer = pulseDuration;
            if (hitVfx == null || Vfx == null) return;
            Vfx.Burst(hitVfx, transform.position);
            hideTimer = hitHide;
        }

        void OnShieldBroken()
        {
            pulseTimer = 0f;
            hideTimer = 0f;
            if (breakVfx != null && Vfx != null)
            {
                Vfx.Burst(breakVfx, transform.position);
                return;
            }
            var flashes = caster != null ? caster.Flashes : null;
            if (flashes != null) flashes.Area(transform.position, breakFlashRadius, CardPalette.Defense);
        }

        Combat.VfxManager Vfx => caster != null ? caster.Vfx : null;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (pulseTimer > 0f) pulseTimer = Mathf.Max(0f, pulseTimer - dt);
            if (hideTimer > 0f) hideTimer = Mathf.Max(0f, hideTimer - dt);
            float pulse = pulseTimer / pulseDuration;
            SetScale(HasFrames ? 1f : 1f + pulseScale * pulse);

            // La aparición y el golpe van en espacio local: siguen al jugador.
            if (Vfx != null)
            {
                Vfx.Follow(appearVfx, transform.position);
                Vfx.Follow(hitVfx, transform.position);
            }

            if (health == null || !health.HasShield || hideTimer > 0f)
            {
                SetAlpha(0f);
                return;
            }

            if (HasFrames)
            {
                frameTime = (frameTime + dt) % (frames.Length / frameRate);
                spriteRenderer.sprite = frames[(int)(frameTime * frameRate) % frames.Length];
            }

            float alpha = Mathf.Lerp(minAlpha, maxAlpha, health.ShieldFraction);
            float remaining = health.ShieldRemaining;
            if (remaining < warningTime && Mathf.FloorToInt(remaining / blinkInterval) % 2 == 1)
                alpha = 0f;

            // El pulso se ve aunque el anillo esté en la fase apagada del parpadeo.
            SetAlpha(Mathf.Lerp(alpha, 1f, pulse));
        }

        void SetScale(float factor)
        {
            float s = baseScale * factor;
            transform.localScale = new Vector3(s, s, 1f);
        }

        void SetAlpha(float alpha)
        {
            // Con frames, los colores van en el sprite.
            Color c = HasFrames ? Color.white : CardPalette.Defense;
            c.a = alpha;
            spriteRenderer.color = c;
        }
    }
}
