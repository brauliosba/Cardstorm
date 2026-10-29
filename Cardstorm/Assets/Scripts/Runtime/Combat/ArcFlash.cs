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

        SpriteRenderer spriteRenderer;
        Color color;
        float timer;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            // Si empieza activo en la escena, se oculta; si lo activa Show, sigue visible.
            if (timer <= 0f) gameObject.SetActive(false);
        }

        // El sprite es un sector de radio 1 apuntando hacia +X. Si ya se estaba mostrando, vuelve a empezar.
        public void Show(Vector2 aim, float range, Color color)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

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
