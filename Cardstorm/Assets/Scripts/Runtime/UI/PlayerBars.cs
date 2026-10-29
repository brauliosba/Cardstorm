using Cardstorm.Player;
using UnityEngine;

namespace Cardstorm.UI
{
    // Barra de vida bajo el personaje. Va como hijo del jugador y crea sus sprites al empezar.
    public class PlayerBars : MonoBehaviour
    {
        const int BackgroundOrder = 20;
        const int FillOrder = 21;

        static readonly Color BackgroundColor = new Color32(0x1E, 0x1E, 0x22, 0xFF);
        static readonly Color HealthColor = new Color32(0xE5, 0x73, 0x73, 0xFF);

        [Tooltip("Sprite cuadrado de 1 unidad.")]
        [SerializeField] Sprite square;

        [Tooltip("Opcional. Si se deja vacío, se usa el material por defecto de los sprites.")]
        [SerializeField] Material material;

        [SerializeField] PlayerHealth health;

        [SerializeField, Min(0.1f)] float width = 1.2f;
        [SerializeField, Min(0.01f)] float height = 0.12f;

        SpriteRenderer healthFill;

        void Awake()
        {
            if (health == null) health = GetComponentInParent<PlayerHealth>();
        }

        void Start()
        {
            CreateBar("HealthBackground", BackgroundColor, BackgroundOrder);
            healthFill = CreateBar("HealthFill", HealthColor, FillOrder);
        }

        void LateUpdate()
        {
            if (health == null || healthFill == null) return;

            float f = health.MaxHealth > 0f ? Mathf.Clamp01(health.Current / health.MaxHealth) : 0f;

            // Relleno anclado a la izquierda: se encoge hacia el borde izquierdo de la barra.
            var t = healthFill.transform;
            t.localScale = new Vector3(width * f, height, 1f);
            t.localPosition = new Vector3(-(1f - f) * width * 0.5f, 0f, 0f);
        }

        SpriteRenderer CreateBar(string name, Color color, int order)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(width, height, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
