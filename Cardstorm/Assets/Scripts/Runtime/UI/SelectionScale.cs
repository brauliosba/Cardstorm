using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Agranda el botón seleccionado. Pasar el ratón por encima también lo selecciona, para que ratón,
    // teclado y mando marquen siempre el mismo botón.
    [RequireComponent(typeof(Selectable))]
    public class SelectionScale : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [SerializeField, Min(1f)] float selectedScale = 1.06f;
        [Tooltip("Segundos en tiempo real.")]
        [SerializeField, Min(0f)] float duration = 0.08f;

        Selectable selectable;
        Tween tween;

        void Awake() => selectable = GetComponent<Selectable>();

        void OnDisable()
        {
            tween?.Kill();
            transform.localScale = Vector3.one;
        }

        public void OnSelect(BaseEventData eventData) => ScaleTo(selectedScale);

        public void OnDeselect(BaseEventData eventData) => ScaleTo(1f);

        // Solo si el ratón se ha movido: el módulo de UI también lanza PointerEnter con el ratón quieto cuando
        // cambia lo que hay debajo (abrir la tienda, el escalado de la selección, una ranura que se oculta),
        // y eso le robaba la selección al teclado o al mando.
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.delta.sqrMagnitude < 0.01f) return;
            var es = EventSystem.current;
            if (es != null && selectable.IsInteractable() && es.currentSelectedGameObject != gameObject)
                es.SetSelectedGameObject(gameObject);
        }

        void ScaleTo(float scale)
        {
            tween?.Kill();
            tween = transform.DOScale(scale, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }
}
