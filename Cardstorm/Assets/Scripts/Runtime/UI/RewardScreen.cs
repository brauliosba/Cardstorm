using System;
using System.Collections.Generic;
using Cardstorm.Cards;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Pantalla de la carta rara al derrotar a un jefe; la carta la añade BossRewardController.
    // No se puede cerrar sin elegir.
    public class RewardScreen : MonoBehaviour
    {
        [Tooltip("Panel hijo con CanvasGroup. Si no lo tiene, se añade al empezar.")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text titleText;

        [Tooltip("Las que sobran se ocultan.")]
        [SerializeField] ShopSlotView[] slots = new ShopSlotView[3];

        [Tooltip("Segundos del fundido al abrir y cerrar. Usa tiempo real: el juego está en pausa.")]
        [SerializeField, Min(0f)] float fadeTime = 0.12f;

        [Tooltip("Segundos en tiempo real tras abrir en los que se ignoran clics, Enviar y navegación, para que "
               + "el golpe que mata al jefe no elija carta. Después, la navegación espera además a que se suelte "
               + "el stick o las flechas.")]
        [SerializeField, Min(0f)] float inputDelay = 0.35f;

        CanvasGroup group;
        Selectable lastSelected;
        Tween fade;
        float readyTime;
        bool inputLocked;

        public event Action<int> CardChosen;

        public bool IsVisible { get; private set; }
        public int SlotCount => slots.Length;

        void Awake()
        {
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            SetVisible(false, instant: true);

            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].Button.onClick.AddListener(() => { if (AcceptsInput) CardChosen?.Invoke(index); });
            }
        }

        void OnDisable() => UnlockInput();

        void OnDestroy()
        {
            fade?.Kill();
            foreach (var slot in slots)
                if (slot != null) slot.Button.onClick.RemoveAllListeners();
        }

        public void Show(string bossName, IReadOnlyList<CardDefinition> cards)
        {
            if (titleText != null) titleText.text = $"Recompensa del {bossName}";

            for (int i = 0; i < slots.Length; i++)
            {
                if (i < cards.Count) slots[i].ShowReward(cards[i]);
                else slots[i].Hide();
            }

            SetVisible(true, instant: false);
            lastSelected = DefaultSelection();
            if (lastSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(lastSelected.gameObject);
            LockInput();
        }

        public void Hide()
        {
            var es = EventSystem.current;
            if (es != null && IsInside(es.currentSelectedGameObject)) es.SetSelectedGameObject(null);
            lastSelected = null;
            UnlockInput();
            SetVisible(false, instant: false);
        }

        void Update()
        {
            if (inputLocked && Time.unscaledTime >= readyTime && !NavigationHeld()) UnlockInput();
            if (IsVisible) KeepFocus();
        }

        bool AcceptsInput => IsVisible && !inputLocked;

        // Igual que en la tienda: durante el bloqueo, el EventSystem no envía navegación ni Enviar/Cancelar.
        void LockInput()
        {
            readyTime = Time.unscaledTime + inputDelay;
            if (inputLocked) return;
            var es = EventSystem.current;
            if (es == null) return;
            inputLocked = true;
            es.sendNavigationEvents = false;
        }

        void UnlockInput()
        {
            if (!inputLocked) return;
            inputLocked = false;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
        }

        static bool NavigationHeld()
        {
            var es = EventSystem.current;
            if (es == null || es.currentInputModule is not InputSystemUIInputModule module) return false;
            var action = module.move != null ? module.move.action : null;
            return action != null && action.ReadValue<Vector2>().sqrMagnitude > 0.01f;
        }

        // Si se pierde la selección, vuelve a la última carta o a la primera.
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
            if (target != null) es.SetSelectedGameObject(target.gameObject);
        }

        Selectable DefaultSelection()
        {
            foreach (var slot in slots)
                if (slot != null && IsSelectable(slot.Button)) return slot.Button;
            return null;
        }

        static bool IsSelectable(Selectable s) => s != null && s.isActiveAndEnabled && s.interactable;

        void SetVisible(bool visible, bool instant)
        {
            IsVisible = visible;
            group.interactable = visible;
            group.blocksRaycasts = visible;

            UiTweens.Fade(group, visible, instant ? 0f : fadeTime, ref fade);
        }

        bool IsInside(GameObject go) => go != null && go.activeInHierarchy && go.transform.IsChildOf(root.transform);
    }
}
