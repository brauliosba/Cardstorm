using Cardstorm.Cards;
using UnityEngine;

namespace Cardstorm.Player
{
    // Anillo del escudo. Va como hijo del jugador y solo toca la opacidad y la escala:
    // PlayerHealth enciende y apaga los sprites del jugador para el parpadeo de invulnerabilidad.
    [RequireComponent(typeof(SpriteRenderer))]
    public class ShieldView : MonoBehaviour
    {
        [SerializeField] PlayerHealth health;

        [Tooltip("Radio del anillo en unidades del mundo.")]
        [SerializeField, Min(0.1f)] float radius = 0.8f;

        [Tooltip("Opacidad con el escudo entero.")]
        [SerializeField, Range(0f, 1f)] float maxAlpha = 0.9f;

        [Tooltip("Opacidad mínima mientras queda escudo.")]
        [SerializeField, Range(0f, 1f)] float minAlpha = 0.3f;

        [Tooltip("Segundos finales en los que parpadea antes de apagarse.")]
        [SerializeField, Min(0f)] float warningTime = 1f;

        [SerializeField, Min(0.01f)] float blinkInterval = 0.1f;

        [Header("Golpe absorbido")]
        [SerializeField, Min(0.01f)] float pulseDuration = 0.15f;

        [Tooltip("Escala extra del anillo al empezar el pulso.")]
        [SerializeField, Min(0f)] float pulseScale = 0.25f;

        [Header("Escudo roto")]
        [Tooltip("Radio del destello al romperse.")]
        [SerializeField, Min(0f)] float breakFlashRadius = 1.2f;

        SpriteRenderer spriteRenderer;
        CardCaster caster;
        float baseScale;
        float pulseTimer;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (health == null) health = GetComponentInParent<PlayerHealth>();
            caster = GetComponentInParent<CardCaster>();

            // El jugador está escalado: se compensa para que el radio sea el del mundo.
            float spriteSize = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.x : 1f;
            float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
            baseScale = 2f * radius / (spriteSize * Mathf.Max(parentScale, 1e-4f));
            SetScale(1f);
            SetAlpha(0f);
        }

        void OnEnable()
        {
            if (health == null) return;
            health.ShieldHit += OnShieldHit;
            health.ShieldBroken += OnShieldBroken;
        }

        void OnDisable()
        {
            if (health == null) return;
            health.ShieldHit -= OnShieldHit;
            health.ShieldBroken -= OnShieldBroken;
        }

        void OnShieldHit() => pulseTimer = pulseDuration;

        void OnShieldBroken()
        {
            pulseTimer = 0f;
            var flashes = caster != null ? caster.Flashes : null;
            if (flashes != null) flashes.Area(transform.position, breakFlashRadius, CardPalette.Defense);
        }

        void LateUpdate()
        {
            if (pulseTimer > 0f) pulseTimer = Mathf.Max(0f, pulseTimer - Time.deltaTime);
            float pulse = pulseTimer / pulseDuration;
            SetScale(1f + pulseScale * pulse);

            if (health == null || !health.HasShield)
            {
                SetAlpha(0f);
                return;
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
            Color c = CardPalette.Defense;
            c.a = alpha;
            spriteRenderer.color = c;
        }
    }
}
