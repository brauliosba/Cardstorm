using TMPro;
using UnityEngine;

namespace Cardstorm.UI
{
    // Icono hijo de un texto que se coloca justo a la izquierda de la primera letra, con cualquier alineación.
    // Se recoloca cuando el texto cambia, en el LateUpdate siguiente.
    [RequireComponent(typeof(RectTransform))]
    public class TextIcon : MonoBehaviour
    {
        [Tooltip("Por defecto, el texto del padre.")]
        [SerializeField] TMP_Text text;
        [SerializeField, Min(0f)] float gap = 6f;

        RectTransform rect;
        Vector2 target;
        bool pending;

        void OnEnable()
        {
            rect = (RectTransform)transform;
            if (text == null) text = transform.parent != null ? transform.parent.GetComponent<TMP_Text>() : null;
            if (text == null) return;
            text.OnPreRenderText += Measure;
            text.ForceMeshUpdate();
            Measure(text.textInfo);
            Apply();
        }

        void OnDisable()
        {
            if (text != null) text.OnPreRenderText -= Measure;
        }

        void LateUpdate()
        {
            if (pending) Apply();
        }

        // Se llama durante la reconstrucción del lienzo: solo guarda la posición.
        void Measure(TMP_TextInfo info)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, yMax = float.MinValue;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                xMin = Mathf.Min(xMin, c.bottomLeft.x);
                yMin = Mathf.Min(yMin, c.bottomLeft.y);
                yMax = Mathf.Max(yMax, c.topRight.y);
            }
            if (xMin == float.MaxValue) return;
            target = new Vector2(xMin - gap, (yMin + yMax) * 0.5f);
            pending = true;
        }

        void Apply()
        {
            pending = false;
            if (rect == null || text == null) return;
            // Pivote a la derecha y en medio: target es el borde derecho del icono.
            var offset = new Vector2((rect.pivot.x - 1f) * rect.rect.width, (rect.pivot.y - 0.5f) * rect.rect.height);
            rect.localPosition = new Vector3(target.x + offset.x, target.y + offset.y, 0f);
        }
    }
}
