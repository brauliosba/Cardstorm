using System;
using System.Collections.Generic;
using Cardstorm.Cards;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Una línea de la lista: una carta del mazo, cuántas copias hay y cuál se quitaría.
    public class RemovalEntry
    {
        public CardDefinition Card;
        public CardInstance Instance;
        public int Count;
        public bool Allowed;
    }

    // Pantalla de eliminar carta, encima de la tienda. Muestra el mazo agrupado por carta y sigue
    // abierta después de cada eliminación; la eliminación la hace LevelUpController.
    public class RemovalScreen : MonoBehaviour
    {
        [Tooltip("Panel hijo con CanvasGroup. Si no lo tiene, se añade al empezar.")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text priceText;
        [SerializeField] TMP_Text goldText;
        [Tooltip("Aviso del mínimo de cartas de acción; solo se ve si alguna carta no se puede quitar.")]
        [SerializeField] TMP_Text noteText;

        [Tooltip("Contenedor de la lista (con un LayoutGroup).")]
        [SerializeField] Transform entriesParent;
        [Tooltip("Primera línea de la lista; se copia para las demás.")]
        [SerializeField] RemovalEntryView entryTemplate;
        [SerializeField] Button backButton;

        [Tooltip("Segundos del fundido al abrir y cerrar, en tiempo real.")]
        [SerializeField, Min(0f)] float fadeTime = 0.12f;

        readonly List<RemovalEntryView> views = new();
        CanvasGroup group;
        Selectable lastSelected;
        Tween fade;
        Tween goldFlash;
        Color goldColor;
        Color priceColor;

        public event Action<int> EntryPressed;
        public event Action BackPressed;

        public bool IsVisible { get; private set; }

        void Awake()
        {
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            SetVisible(false, instant: true);
            goldColor = goldText.color;
            priceColor = priceText.color;

            if (entriesParent == null) entriesParent = entryTemplate.transform.parent;
            AddView(entryTemplate);
            backButton.onClick.AddListener(OnBackClicked);
        }

        void OnDestroy()
        {
            fade?.Kill();
            goldFlash?.Kill();
            foreach (var view in views)
                if (view != null) view.Button.onClick.RemoveAllListeners();
            backButton.onClick.RemoveListener(OnBackClicked);
        }

        // Solo se usan las count primeras entradas: la lista se reutiliza entre tiendas.
        public void Show(IReadOnlyList<RemovalEntry> entries, int count, int price, int gold, int minActionCards)
        {
            while (views.Count < count)
                AddView(Instantiate(entryTemplate, entriesParent));

            bool anyBlocked = false;
            for (int i = 0; i < views.Count; i++)
            {
                if (i >= count)
                {
                    views[i].Hide();
                    continue;
                }
                var entry = entries[i];
                views[i].Show(entry.Card, entry.Count, entry.Allowed);
                anyBlocked |= !entry.Allowed;
            }

            priceText.SetText("Cuesta {0} oro", price);
            priceText.color = gold >= price ? priceColor : HudColors.NotEnough;
            goldText.SetText("Oro {0}", gold);
            if (noteText != null)
            {
                noteText.gameObject.SetActive(anyBlocked);
                noteText.SetText("Deben quedar al menos {0} cartas de acción", minActionCards);
            }

            // Al repintar con la pantalla abierta (después de quitar una carta), se conserva la selección
            // si sigue disponible; KeepFocus se encarga si esa carta ya no está.
            if (IsVisible) return;
            SetVisible(true, instant: false);
            lastSelected = DefaultSelection();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(lastSelected.gameObject);
        }

        public void Hide()
        {
            var es = EventSystem.current;
            if (es != null && IsInside(es.currentSelectedGameObject)) es.SetSelectedGameObject(null);
            lastSelected = null;
            SetVisible(false, instant: false);
        }

        // Se ha intentado quitar una carta sin oro suficiente.
        public void EntryFailed(int index)
        {
            if (index >= 0 && index < views.Count) views[index].Wobble();
            UiTweens.Flash(goldText, goldColor, HudColors.NotEnough, ref goldFlash);
        }

        void Update()
        {
            if (IsVisible) KeepFocus();
        }

        // Igual que en la tienda: si la selección se pierde o sale de la pantalla, vuelve al último botón.
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

            var target = IsSelectable(lastSelected) ? lastSelected : DefaultSelection();
            es.SetSelectedGameObject(target.gameObject);
        }

        // La primera carta que se puede quitar; si no hay ninguna, Volver.
        Selectable DefaultSelection()
        {
            foreach (var view in views)
                if (IsSelectable(view.Button)) return view.Button;
            return backButton;
        }

        static bool IsSelectable(Selectable s) => s != null && s.isActiveAndEnabled && s.interactable;

        void AddView(RemovalEntryView view)
        {
            int index = views.Count;
            views.Add(view);
            view.Button.onClick.AddListener(() => EntryPressed?.Invoke(index));
        }

        void SetVisible(bool visible, bool instant)
        {
            IsVisible = visible;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            UiTweens.Fade(group, visible, instant ? 0f : fadeTime, ref fade);
        }

        bool IsInside(GameObject go) => go != null && go.activeInHierarchy && go.transform.IsChildOf(root.transform);

        void OnBackClicked() => BackPressed?.Invoke();
    }
}
