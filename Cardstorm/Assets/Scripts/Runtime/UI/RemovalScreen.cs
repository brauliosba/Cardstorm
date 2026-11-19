using Cardstorm.Localization;
using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Muestra el mazo agrupado por carta y gema.
    public class RemovalScreen : ModalScreen
    {
        [SerializeField] TMP_Text priceText;
        [SerializeField] TMP_Text goldText;
        [Tooltip("Aviso del mínimo de cartas de acción; solo se ve si alguna carta no se puede quitar.")]
        [SerializeField] TMP_Text noteText;

        [Tooltip("Contenedor de la lista (con un LayoutGroup).")]
        [SerializeField] Transform entriesParent;
        [Tooltip("Primera línea de la lista; se copia para las demás.")]
        [SerializeField] RemovalEntryView entryTemplate;
        [Tooltip("Cierra la lista y sigue con el nivel.")]
        [SerializeField] Button backButton;

        DeckList list;
        Tween goldFlash;
        Color goldColor;
        Color priceColor;

        public event Action<int> EntryPressed;
        public event Action BackPressed;

        protected override void Awake()
        {
            base.Awake();
            goldColor = goldText.color;
            priceColor = priceText.color;
            list = new DeckList(entryTemplate, entriesParent, OnEntryClicked);
            backButton.onClick.AddListener(OnBackClicked);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            goldFlash?.Kill();
            list?.Dispose();
            backButton.onClick.RemoveListener(OnBackClicked);
        }

        // Al repintar con la pantalla abierta (después de quitar una carta), se conserva la selección si
        // sigue disponible.
        public void Show(IReadOnlyList<RemovalEntry> entries, int count, int price, int gold, int minActionCards)
        {
            list.Show(entries, count);

            bool anyBlocked = false;
            for (int i = 0; i < count; i++) anyBlocked |= !entries[i].Allowed;

            priceText.SetText(L10n.Get(L10nKeys.RemovalPrice), price);
            priceText.color = gold >= price ? priceColor : HudColors.NotEnough;
            goldText.SetText("{0}", gold);
            if (noteText != null)
            {
                noteText.gameObject.SetActive(anyBlocked);
                noteText.SetText(L10n.Get(L10nKeys.RemovalMinAction), minActionCards);
            }

            if (!IsVisible) Open(lockInput: false);
        }

        // Se ha intentado quitar una carta sin oro suficiente.
        public void EntryFailed(int index)
        {
            list.Wobble(index);
            UiTweens.Flash(goldText, goldColor, HudColors.NotEnough, ref goldFlash);
        }

        // La primera carta que se puede quitar; si no hay ninguna, Continuar.
        protected override Selectable DefaultSelection() => list?.FirstSelectable() ?? backButton;

        void OnEntryClicked(int index) { if (AcceptsInput) EntryPressed?.Invoke(index); }

        void OnBackClicked() { if (AcceptsInput) BackPressed?.Invoke(); }
    }
}
