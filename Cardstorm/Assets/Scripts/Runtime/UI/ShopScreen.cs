using ChristinaCreatesGames.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Pantalla de la tienda que se abre en cada subida de nivel. Por ahora solo título, oro y Continuar;
    // las ranuras de cartas y Refrescar llegan con la tienda completa.
    public class ShopScreen : MonoBehaviour
    {
        [Tooltip("Panel hijo que se muestra y se oculta. Este componente va en el Canvas, que sigue activo.")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text titleText;
        [Tooltip("«1 de 3» cuando hay varias subidas seguidas; oculto si solo hay una.")]
        [SerializeField] TMP_Text queueText;
        [SerializeField] TMP_Text goldText;
        [Tooltip("Solo aparece en los niveles múltiplos de 5.")]
        [SerializeField] Button removeButton;
        [SerializeField] Button continueButton;
        [SerializeField] SetsUiElementToSelectOnInteraction selectableOnInteraction;

        public event Action ContinuePressed;

        public bool IsVisible => root != null && root.activeSelf;

        void Awake()
        {
            continueButton.onClick.AddListener(OnContinueClicked);
            root.SetActive(false);
        }

        void OnDestroy() => continueButton.onClick.RemoveListener(OnContinueClicked);

        public void Show(int level, int position, int total, int gold, bool offersRemoval)
        {
            selectableOnInteraction.JumpToElement();
            titleText.SetText("Nivel {0}", level);
            UpdateQueue(position, total);
            goldText.SetText("Oro {0}", gold);
            if (removeButton != null)
            {
                removeButton.gameObject.SetActive(offersRemoval);
                removeButton.interactable = false;  // Se activa con la eliminación de cartas
            }

            root.SetActive(true);
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
            Select(null);
            root.SetActive(false);
        }

        void OnContinueClicked() => ContinuePressed?.Invoke();

        static void Select(GameObject target)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
