using System;
using Cardstorm.Cards;
using Cardstorm.Localization;
using Cardstorm.Progression;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Tienda de Nueva carta en la subida de nivel; las compras las hace LevelUpController.
    public class ShopScreen : ModalScreen
    {
        [SerializeField] TMP_Text titleText;
        [Tooltip("«1 de 3» cuando hay varias subidas seguidas; oculto si solo hay una.")]
        [SerializeField] TMP_Text queueText;
        [SerializeField] TMP_Text goldText;

        [Tooltip("Seis ranuras: las tres primeras de acción y las tres últimas de poder.")]
        [SerializeField] ShopSlotView[] slots = new ShopSlotView[6];

        [SerializeField] Button refreshButton;
        [SerializeField] TMP_Text refreshText;
        [Tooltip("Vuelve a la elección del nivel; se oculta en cuanto se compra una carta.")]
        [SerializeField] Button backButton;
        [SerializeField] Button continueButton;

        Tween goldCount;
        Tween goldFlash;
        Tween refreshWobble;
        Color goldColor;
        Color refreshColor;
        int shownGold;

        public event Action<int> BuyPressed;
        public event Action RefreshPressed;
        public event Action BackPressed;
        public event Action ContinuePressed;

        protected override void Awake()
        {
            base.Awake();
            goldColor = goldText.color;
            if (refreshText != null) refreshColor = refreshText.color;

            if (slots.Length != ShopOffer<CardDefinition>.SlotCount)
                Debug.LogWarning($"ShopScreen tiene {slots.Length} ranuras y la tienda ofrece {ShopOffer<CardDefinition>.SlotCount}.", this);

            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].Button.onClick.AddListener(() => { if (AcceptsInput) BuyPressed?.Invoke(index); });
            }
            if (refreshButton != null) refreshButton.onClick.AddListener(OnRefreshClicked);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            goldCount?.Kill();
            goldFlash?.Kill();
            refreshWobble?.Kill();
            foreach (var slot in slots)
                if (slot != null) slot.Button.onClick.RemoveAllListeners();
            if (refreshButton != null) refreshButton.onClick.RemoveListener(OnRefreshClicked);
            if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
            continueButton.onClick.RemoveListener(OnContinueClicked);
        }

        public void Show(int level, int position, int total, ShopOffer<CardDefinition> offer, bool canGoBack, bool lockInput)
        {
            titleText.SetText(L10n.Get(L10nKeys.Level), level);
            UpdateQueue(position, total);
            SetBackAvailable(canGoBack);
            shownGold = offer.State.Gold;
            UpdateOffer(offer);
            Open(lockInput);
        }

        public void UpdateQueue(int position, int total)
        {
            if (queueText == null) return;
            queueText.gameObject.SetActive(total > 1);
            queueText.SetText(L10n.Get(L10nKeys.Queue), position, total);
        }

        public void SetBackAvailable(bool value)
        {
            if (backButton != null) backButton.gameObject.SetActive(value);
        }

        // Vuelve a pintar oro, ranuras y Refrescar después de comprar o refrescar.
        public void UpdateOffer(ShopOffer<CardDefinition> offer)
        {
            int gold = offer.State.Gold;
            UiTweens.Count(goldText, L10n.Get(L10nKeys.Gold), shownGold, gold, ref goldCount);
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
                refreshText.SetText(L10n.Get(L10nKeys.Refresh), price);
                refreshText.color = gold >= price ? refreshColor : HudColors.NotEnough;
            }
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

        void FlashGold() => UiTweens.Flash(goldText, goldColor, HudColors.NotEnough, ref goldFlash);

        // La primera carta que se puede comprar; si no queda ninguna, Continuar.
        protected override Selectable DefaultSelection()
        {
            foreach (var slot in slots)
                if (slot != null && IsSelectable(slot.Button)) return slot.Button;
            return continueButton;
        }

        void OnRefreshClicked() { if (AcceptsInput) RefreshPressed?.Invoke(); }

        void OnBackClicked() { if (AcceptsInput) BackPressed?.Invoke(); }

        void OnContinueClicked() { if (AcceptsInput) ContinuePressed?.Invoke(); }
    }
}
