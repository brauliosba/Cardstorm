using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Fila con un valor que cambia con izquierda y derecha, para usarla con mando sin abrir un desplegable.
    public class OptionStepper : Selectable
    {
        [SerializeField] TMP_Text valueText;

        [Tooltip("Opcionales. Flechas para el ratón; no reciben el foco.")]
        [SerializeField] Button previousButton;
        [SerializeField] Button nextButton;

        [Tooltip("Del último valor se pasa al primero y al revés.")]
        [SerializeField] bool wrap;

        string[] labels = Array.Empty<string>();

        public int Index { get; private set; }

        public event Action<int> Changed;

        protected override void Awake()
        {
            base.Awake();
            if (previousButton != null) previousButton.onClick.AddListener(Previous);
            if (nextButton != null) nextButton.onClick.AddListener(Next);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (previousButton != null) previousButton.onClick.RemoveListener(Previous);
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
        }

        // No avisa: es para mostrar el valor guardado.
        public void SetOptions(string[] values, int index)
        {
            labels = values ?? Array.Empty<string>();
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, labels.Length - 1));
            ShowValue();
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                base.OnMove(eventData);
                return;
            }

            switch (eventData.moveDir)
            {
                case MoveDirection.Left: Step(-1); break;
                case MoveDirection.Right: Step(1); break;
                default: base.OnMove(eventData); break;
            }
        }

        // Al pulsar una flecha con el ratón, el foco vuelve a la fila para seguir con el teclado o el mando.
        void Previous()
        {
            Reselect();
            Step(-1);
        }

        void Next()
        {
            Reselect();
            Step(1);
        }

        void Reselect()
        {
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(gameObject);
        }

        void Step(int delta)
        {
            if (!IsInteractable() || labels.Length < 2) return;
            int next = Index + delta;
            if (wrap) next = (next + labels.Length) % labels.Length;
            else next = Mathf.Clamp(next, 0, labels.Length - 1);
            if (next == Index) return;

            Index = next;
            ShowValue();
            Changed?.Invoke(Index);
        }

        void ShowValue()
        {
            if (valueText != null) valueText.text = Index < labels.Length ? labels[Index] : string.Empty;
            bool canStep = labels.Length > 1;
            if (previousButton != null) previousButton.interactable = canStep && (wrap || Index > 0);
            if (nextButton != null) nextButton.interactable = canStep && (wrap || Index < labels.Length - 1);
        }
    }
}
