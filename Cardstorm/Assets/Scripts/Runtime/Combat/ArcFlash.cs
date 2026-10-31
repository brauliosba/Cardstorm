using UnityEngine;

namespace Cardstorm.Combat
{
    // Destello del golpe en arco. Va como hijo del jugador, desactivado hasta que se muestra.
    [RequireComponent(typeof(SpriteRenderer))]
    public class ArcFlash : MonoBehaviour
    {
        [Tooltip("Segundos que tarda en desvanecerse.")]
        [SerializeField, Min(0.01f)] float duration = 0.12f;

        [Tooltip("Opacidad al aparecer.")]
        [SerializeField, Range(0f, 1f)] float startAlpha = 0.8f;

        [Tooltip("Anillo de radio 1 para los golpes de 360°. El sector es el sprite del SpriteRenderer.")]
        [SerializeField] Sprite ringSprite;

        SpriteRenderer spriteRenderer;
        Sprite arcSprite;
        Color color;
        float timer;

        void Awake()
        {
            Init();

            // Si empieza activo en la escena, se oculta; si lo activa Show, sigue visible.
            if (timer <= 0f) gameObject.SetActive(false);
        }

        // Show puede llegar antes que Awake si el objeto empieza desactivado.
        void Init()
        {
            if (spriteRenderer != null) return;
            spriteRenderer = GetComponent<SpriteRenderer>();
            arcSprite = spriteRenderer.sprite;
        }

        // El sector es de radio 1 y apunta hacia +X. Si ya se estaba mostrando, vuelve a empezar.
        public void Show(Vector2 aim, float range, float arcDegrees, Color color)
        {
            Init();

            bool ring = arcDegrees >= 360f && ringSprite != null;
            spriteRenderer.sprite = ring ? ringSprite : arcSprite;

            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            transform.localScale = new Vector3(range, range, 1f);

            this.color = color;
            timer = duration;
            Apply();
            gameObject.SetActive(true);
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }
            Apply();
        }

        void Apply()
        {
            color.a = startAlpha * Mathf.Clamp01(timer / duration);
            spriteRenderer.color = color;
        }
    }
}
