using System;
using Cardstorm.Cards;
using Cardstorm.Progression;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Pantalla de la tienda que se abre en cada subida de nivel; las compras las hace LevelUpController.
    public class ShopScreen : MonoBehaviour
    {
        [Tooltip("Panel hijo con CanvasGroup. Si no lo tiene, se añade al empezar.")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text titleText;
        [Tooltip("«1 de 3» cuando hay varias subidas seguidas; oculto si solo hay una.")]
        [SerializeField] TMP_Text queueText;
        [SerializeField] TMP_Text goldText;

        [Tooltip("Seis ranuras: las tres primeras de acción y las tres últimas de poder.")]
        [SerializeField] ShopSlotView[] slots = new ShopSlotView[6];

        [SerializeField] Button refreshButton;
        [SerializeField] TMP_Text refreshText;
        [Tooltip("Solo aparece en los niveles múltiplos de 5.")]
        [SerializeField] Button removeButton;
        [SerializeField] TMP_Text removeText;
        [SerializeField] Button continueButton;

        [Tooltip("Segundos del fundido al abrir y cerrar. Usa tiempo real: el juego está en pausa.")]
        [SerializeField, Min(0f)] float fadeTime = 0.12f;

        [Tooltip("Segundos en tiempo real tras abrir en los que la tienda ignora clics, Enviar y navegación, para que "
               + "la pulsación de una carta justo al subir de nivel no compre nada. Después, la navegación espera "
               + "además a que se suelte el stick o las flechas.")]
        [SerializeField, Min(0f)] float inputDelay = 0.35f;

        CanvasGroup group;
        Selectable lastSelected;
        Tween fade;
        Tween goldCount;
        Tween goldFlash;
        Tween refreshWobble;
        Tween removeWobble;
        Color goldColor;
        Color refreshColor;
        Color removeColor;
        int shownGold;
        CardRemoval removal;
        bool suspended;
        float readyTime;
        bool inputLocked;

        public event Action<int> BuyPressed;
        public event Action RefreshPressed;
        public event Action RemovePressed;
        public event Action ContinuePressed;

        public bool IsVisible { get; private set; }

        void Awake()
        {
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            SetVisible(false, instant: true);
            goldColor = goldText.color;
            if (refreshText != null) refreshColor = refreshText.color;
            if (removeText != null) removeColor = removeText.color;

            if (slots.Length != ShopOffer<CardDefinition>.SlotCount)
                Debug.LogWarning($"ShopScreen tiene {slots.Length} ranuras y la tienda ofrece {ShopOffer<CardDefinition>.SlotCount}.", this);

            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].Button.onClick.AddListener(() => { if (AcceptsInput) BuyPressed?.Invoke(index); });
            }
            if (refreshButton != null) refreshButton.onClick.AddListener(OnRefreshClicked);
            if (removeButton != null) removeButton.onClick.AddListener(OnRemoveClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        void OnDisable() => UnlockInput();

        void OnDestroy()
        {
            fade?.Kill();
            goldCount?.Kill();
            goldFlash?.Kill();
            refreshWobble?.Kill();
            removeWobble?.Kill();
            foreach (var slot in slots)
                if (slot != null) slot.Button.onClick.RemoveAllListeners();
            if (refreshButton != null) refreshButton.onClick.RemoveListener(OnRefreshClicked);
            if (removeButton != null) removeButton.onClick.RemoveListener(OnRemoveClicked);
            continueButton.onClick.RemoveListener(OnContinueClicked);
        }

        public void Show(int level, int position, int total, ShopOffer<CardDefinition> offer, CardRemoval removal)
        {
            this.removal = removal;
            titleText.SetText("Nivel {0}", level);
            UpdateQueue(position, total);
            if (removeButton != null) removeButton.gameObject.SetActive(removal != null && removal.Offered);
            shownGold = offer.State.Gold;
            UpdateOffer(offer);

            suspended = false;
            SetVisible(true, instant: false);
            lastSelected = DefaultSelection();
            Select(lastSelected.gameObject);
            LockInput();
        }

        public void UpdateQueue(int position, int total)
        {
            if (queueText == null) return;
            queueText.gameObject.SetActive(total > 1);
            queueText.SetText("{0} de {1}", position, total);
        }

        // Vuelve a pintar oro, ranuras, Refrescar y Eliminar después de comprar, refrescar o eliminar.
        public void UpdateOffer(ShopOffer<CardDefinition> offer)
        {
            int gold = offer.State.Gold;
            UiTweens.Count(goldText, "Oro {0}", shownGold, gold, ref goldCount);
            shownGold = gold;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = i < ShopOffer<CardDefinition>.SlotCount ? offer.Slot(i) : null;
                if (slot == null || slot.Item == null) slots[i].Hide();
                else slots[i].Show(slot.Item, slot.Price, slot.Bought, gold >= slot.Price);
            }

            // Refrescar se puede pulsar siempre: sin oro, tambalea en lugar de quedarse gris.
            if (refreshText != null)
            {
                int price = offer.RefreshPrice;
                refreshText.SetText("Refrescar ({0} oro)", price);
                refreshText.color = gold >= price ? refreshColor : HudColors.NotEnough;
            }

            UpdateRemoval(gold);
        }

        // El precio sube con cada eliminación. Sin oro, el botón tambalea, como Refrescar.
        void UpdateRemoval(int gold)
        {
            if (removeText == null || removal == null || !removal.Offered) return;

            int price = removal.Price;
            removeText.SetText("Eliminar carta ({0} oro)", price);
            removeText.color = gold >= price ? removeColor : HudColors.NotEnough;
        }

        // Mientras la pantalla de eliminar está encima, la tienda no se puede pulsar ni recupera la selección.
        public void SetSuspended(bool value)
        {
            suspended = value;
            group.interactable = IsVisible && !value;
            group.blocksRaycasts = IsVisible && !value;
        }

        public void Bought(int slot) => slots[slot].Bought();

        public void BuyFailed(int slot)
        {
            slots[slot].Wobble();
            FlashGold();
        }

        public void RefreshFailed()
        {
            if (refreshButton != null) UiTweens.Wobble(refreshButton.transform, ref refreshWobble);
            FlashGold();
        }

        public void RemoveFailed()
        {
            if (removeButton != null) UiTweens.Wobble(removeButton.transform, ref removeWobble);
            FlashGold();
        }

        void FlashGold() => UiTweens.Flash(goldText, goldColor, HudColors.NotEnough, ref goldFlash);

        public void Hide()
        {
            var es = EventSystem.current;
            if (es != null && IsInside(es.currentSelectedGameObject)) es.SetSelectedGameObject(null);
            lastSelected = null;
            suspended = false;
            removal = null;
            UnlockInput();
            SetVisible(false, instant: false);
        }

        void Update()
        {
            if (inputLocked && Time.unscaledTime >= readyTime && !NavigationHeld()) UnlockInput();
            if (IsVisible && !suspended) KeepFocus();
        }

        bool AcceptsInput => IsVisible && !suspended && !inputLocked;

        // La tienda se abre en mitad del combate: el botón de una carta (A en el mando es también Enviar) o el
        // stick de moverse pueden seguir pulsados. Durante el bloqueo, el EventSystem no envía navegación ni
        // Enviar/Cancelar, y los clics se ignoran en los propios botones.
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

        // Si se entra en la tienda con el stick inclinado, el módulo arrastra el contador de repetición de la
        // partida y la selección saltaría sola hasta el final de la fila.
        static bool NavigationHeld()
        {
            var es = EventSystem.current;
            if (es == null || es.currentInputModule is not InputSystemUIInputModule module) return false;
            var action = module.move != null ? module.move.action : null;
            return action != null && action.ReadValue<Vector2>().sqrMagnitude > 0.01f;
        }

        // Si se pierde la selección (clic en el fondo, carta recién comprada) o sale de la tienda,
        // vuelve al último botón usado o, si ya no se puede pulsar, al primero disponible.
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

        // La primera carta que se puede comprar; si no queda ninguna, Continuar.
        Selectable DefaultSelection()
        {
            foreach (var slot in slots)
                if (slot != null && IsSelectable(slot.Button)) return slot.Button;
            return continueButton;
        }

        // Mira el interactable propio y no IsInteractable(): justo al abrir, el CanvasGroup puede no haber
        // avisado todavía a los botones.
        static bool IsSelectable(Selectable s) => s != null && s.isActiveAndEnabled && s.interactable;

        // Al ocultarse, interactable y blocksRaycasts se quitan en el acto; solo el alfa se desvanece.
        // El fundido ignora Time.timeScale (SetUpdate(true)) porque la tienda se abre con el juego en pausa.
        void SetVisible(bool visible, bool instant)
        {
            IsVisible = visible;
            group.interactable = visible;
            group.blocksRaycasts = visible;

            UiTweens.Fade(group, visible, instant ? 0f : fadeTime, ref fade);
        }

        bool IsInside(GameObject go) => go != null && go.activeInHierarchy && go.transform.IsChildOf(root.transform);

        void OnRefreshClicked() { if (AcceptsInput) RefreshPressed?.Invoke(); }

        void OnRemoveClicked() { if (AcceptsInput) RemovePressed?.Invoke(); }

        void OnContinueClicked() { if (AcceptsInput) ContinuePressed?.Invoke(); }

        static void Select(GameObject target)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
