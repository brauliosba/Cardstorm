using System;
using System.Collections.Generic;
using Cardstorm.Cards;
using Cardstorm.Localization;
using Cardstorm.Progression;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Mejorar carta en dos fases: primero tres gemas y después la carta del mazo donde engarzarla.
    // Lo que se elige lo aplica LevelUpController.
    public class GemScreen : ModalScreen
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text goldText;
        [Tooltip("Indicación bajo el título: qué hay que elegir en cada fase.")]
        [SerializeField] TMP_Text hintText;

        [Header("Fase 1 · Gemas")]
        [Tooltip("Contenedor de las ranuras de gemas; se oculta en la fase de cartas.")]
        [SerializeField] GameObject gemsPanel;
        [SerializeField] GemSlotView[] slots = new GemSlotView[GemOffer<GemDefinition>.SlotCount];
        [SerializeField] Button refreshButton;
        [SerializeField] TMP_Text refreshText;

        [Header("Fase 2 · Cartas")]
        [Tooltip("Contenedor de la lista (con un LayoutGroup); se oculta en la fase de gemas.")]
        [SerializeField] Transform entriesParent;
        [Tooltip("Primera línea de la lista; se copia para las demás.")]
        [SerializeField] RemovalEntryView entryTemplate;

        [Tooltip("En las gemas, vuelve a la elección del nivel; en las cartas, a las gemas.")]
        [SerializeField] Button backButton;

        DeckList list;
        Tween goldCount;
        Tween goldFlash;
        Tween refreshWobble;
        Color goldColor;
        Color refreshColor;
        int shownGold;

        public event Action<int> GemPicked;
        public event Action<int> CardPicked;
        public event Action RefreshPressed;
        public event Action BackPressed;

        // En la fase de cartas.
        public bool ShowingCards { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            goldColor = goldText.color;
            if (refreshText != null) refreshColor = refreshText.color;
            list = new DeckList(entryTemplate, entriesParent, OnCardClicked);

            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].Button.onClick.AddListener(() => { if (AcceptsInput) GemPicked?.Invoke(index); });
            }
            if (refreshButton != null) refreshButton.onClick.AddListener(OnRefreshClicked);
            backButton.onClick.AddListener(OnBackClicked);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            goldCount?.Kill();
            goldFlash?.Kill();
            refreshWobble?.Kill();
            list?.Dispose();
            foreach (var slot in slots)
                if (slot != null) slot.Button.onClick.RemoveAllListeners();
            if (refreshButton != null) refreshButton.onClick.RemoveListener(OnRefreshClicked);
            backButton.onClick.RemoveListener(OnBackClicked);
        }

        // compatible[i]: copias del mazo que admiten la gema de la ranura i.
        public void ShowGems(GemOffer<GemDefinition> offer, IReadOnlyList<int> compatible, bool lockInput)
        {
            ShowingCards = false;
            SetPhase(cards: false);
            if (titleText != null) titleText.text = L10n.Get(L10nKeys.UpgradeCard);
            if (hintText != null) hintText.text = L10n.Get(L10nKeys.GemPick);
            if (!IsVisible) shownGold = offer.State.Gold;
            UpdateGems(offer, compatible);

            if (IsVisible) ResetFocus();
            else Open(lockInput);
        }

        // Vuelve a pintar oro, gemas y Refrescar después de refrescar.
        public void UpdateGems(GemOffer<GemDefinition> offer, IReadOnlyList<int> compatible)
        {
            int gold = offer.State.Gold;
            UiTweens.Count(goldText, "{0}", shownGold, gold, ref goldCount);
            shownGold = gold;

            for (int i = 0; i < slots.Length; i++)
            {
                var gem = offer.Slot(i);
                if (gem == null) slots[i].Hide();
                else slots[i].Show(gem, i < compatible.Count ? compatible[i] : 0);
            }

            // Como en la tienda: sin oro, Refrescar tambalea en lugar de quedarse gris.
            if (refreshText != null)
            {
                int price = offer.RefreshPrice;
                refreshText.SetText(L10n.Get(L10nKeys.Refresh), price);
                refreshText.color = gold >= price ? refreshColor : HudColors.NotEnough;
            }
        }

        // Todo el mazo que admite la gema, agrupado por carta y gema.
        public void ShowCards(GemDefinition gem, IReadOnlyList<RemovalEntry> entries, int count)
        {
            ShowingCards = true;
            SetPhase(cards: true);
            if (titleText != null) titleText.text = L10n.Format(L10nKeys.GemSocket, L10n.GemName(gem));
            if (hintText != null) hintText.text = L10n.GemDescription(gem);
            list.Show(entries, count);
            ResetFocus();
        }

        public void GemFailed(int slot)
        {
            if (slot >= 0 && slot < slots.Length) slots[slot].Wobble();
        }

        public void CardFailed(int index) => list.Wobble(index);

        public void RefreshFailed()
        {
            if (refreshButton != null) UiTweens.Wobble(refreshButton.transform, ref refreshWobble);
            UiTweens.Flash(goldText, goldColor, HudColors.NotEnough, ref goldFlash);
        }

        public override void Hide()
        {
            ShowingCards = false;
            base.Hide();
        }

        void SetPhase(bool cards)
        {
            if (gemsPanel != null) gemsPanel.SetActive(!cards);
            if (refreshButton != null) refreshButton.gameObject.SetActive(!cards);
            var listObject = entriesParent != null ? entriesParent.gameObject : entryTemplate.transform.parent.gameObject;
            listObject.SetActive(cards);
        }

        protected override Selectable DefaultSelection()
        {
            if (ShowingCards) return list?.FirstSelectable() ?? backButton;
            foreach (var slot in slots)
                if (slot != null && IsSelectable(slot.Button)) return slot.Button;
            return backButton;
        }

        void OnCardClicked(int index) { if (AcceptsInput) CardPicked?.Invoke(index); }

        void OnRefreshClicked() { if (AcceptsInput) RefreshPressed?.Invoke(); }

        void OnBackClicked() { if (AcceptsInput) BackPressed?.Invoke(); }
    }
}
