using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Pantalla de la tienda que se abre en cada subida de nivel. Por ahora solo título, oro y Continuar;
    // las ranuras de cartas y Refrescar llegan con la tienda completa.
    // Se muestra y se oculta con el CanvasGroup de root (el objeto sigue activo) y, mientras está abierta,
    // no deja que el teclado o el mando se queden sin botón seleccionado.
    public class ShopScreen : MonoBehaviour
    {
        [Tooltip("Panel hijo con CanvasGroup. Si no lo tiene, se añade al empezar.")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text titleText;
        [Tooltip("«1 de 3» cuando hay varias subidas seguidas; oculto si solo hay una.")]
        [SerializeField] TMP_Text queueText;
        [SerializeField] TMP_Text goldText;
        [Tooltip("Solo aparece en los niveles múltiplos de 5.")]
        [SerializeField] Button removeButton;
        [SerializeField] Button continueButton;

        [Tooltip("Segundos del fundido al abrir y cerrar. Usa tiempo real: el juego está en pausa.")]
        [SerializeField, Min(0f)] float fadeTime = 0.12f;

        CanvasGroup group;
        Selectable lastSelected;
        Tween fade;

        public event Action ContinuePressed;

        public bool IsVisible { get; private set; }

        void Awake()
        {
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            SetVisible(false, instant: true);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        void OnDestroy()
        {
            fade?.Kill();
            continueButton.onClick.RemoveListener(OnContinueClicked);
        }

        public void Show(int level, int position, int total, int gold, bool offersRemoval)
        {
            titleText.SetText("Nivel {0}", level);
            UpdateQueue(position, total);
            goldText.SetText("Oro {0}", gold);
            if (removeButton != null)
            {
                removeButton.gameObject.SetActive(offersRemoval);
                removeButton.interactable = false;  // Se activa con la eliminación de cartas
            }

            SetVisible(true, instant: false);
            lastSelected = continueButton;
            Select(continueButton.gameObject);
        }

        public void UpdateQueue(int position, int total)
        {
            if (queueText == null) return;
            queueText.gameObject.SetActive(total > 1);
            queueText.SetText("{0} de {1}", position, total);
        }

        public void Hide()
        {
            var es = EventSystem.current;
            if (es != null && IsInside(es.currentSelectedGameObject)) es.SetSelectedGameObject(null);
            lastSelected = null;
            SetVisible(false, instant: false);
        }

        void Update()
        {
            if (IsVisible) KeepFocus();
        }

        // Si se pierde la selección (clic en el fondo) o sale de la tienda, vuelve al último botón usado.
        void KeepFocus()
        {
            var es = EventSystem.current;
            if (es == null) return;

            var current = es.currentSelectedGameObject;
            if (IsInside(current) && current.TryGetComponent(out Selectable selectable) && selectable.IsInteractable())
            {
                lastSelected = selectable;
                return;
            }

            var target = lastSelected != null && lastSelected.isActiveAndEnabled && lastSelected.IsInteractable()
                ? lastSelected
                : continueButton;
            es.SetSelectedGameObject(target.gameObject);
        }

        // Al ocultarse, interactable y blocksRaycasts se quitan en el acto; solo el alfa se desvanece.
        // El fundido ignora Time.timeScale (SetUpdate(true)) porque la tienda se abre con el juego en pausa.
        void SetVisible(bool visible, bool instant)
        {
            IsVisible = visible;
            group.interactable = visible;
            group.blocksRaycasts = visible;

            float target = visible ? 1f : 0f;
            fade?.Kill();
            if (instant || fadeTime <= 0f)
            {
                group.alpha = target;
                return;
            }
            fade = DOTween.To(() => group.alpha, a => group.alpha = a, target, fadeTime)
                .SetEase(visible ? Ease.OutQuad : Ease.InQuad)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        bool IsInside(GameObject go) => go != null && go.activeInHierarchy && go.transform.IsChildOf(root.transform);

        void OnContinueClicked() => ContinuePressed?.Invoke();

        static void Select(GameObject target)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
