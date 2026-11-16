using Cardstorm.Cards;
using Cardstorm.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Cartas que quedan en el mazo, en el descarte y agotadas. El texto solo cambia cuando cambia el número.
    public class PileCountersView : MonoBehaviour
    {
        [SerializeField] PlayerCards cards;
        [SerializeField] TMP_Text deckText;
        [SerializeField] TMP_Text discardText;

        [Tooltip("Solo se muestra si hay alguna carta agotada.")]
        [SerializeField] TMP_Text exhaustedText;

        [Tooltip("Rótulo bajo el mazo; mientras se baraja dice «Barajando…».")]
        [SerializeField] TMP_Text deckLabel;

        [Tooltip("Anillo radial sobre el mazo que se llena durante el rebarajado.")]
        [SerializeField] Image reshuffleRing;

        int shownDeck = -1;
        int shownDiscard = -1;
        int shownExhausted = -1;
        bool? shownReshuffling;

        void Awake()
        {
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
        }

        void OnEnable() => L10n.Changed += OnLanguageChanged;

        void OnDisable() => L10n.Changed -= OnLanguageChanged;

        void OnLanguageChanged()
        {
            shownExhausted = -1;
            shownReshuffling = null;
        }

        void LateUpdate()
        {
            if (cards == null || cards.Cycle == null) return;

            var cycle = cards.Cycle;
            if (cycle.DrawCount != shownDeck)
            {
                shownDeck = cycle.DrawCount;
                deckText.SetText("{0}", shownDeck);
            }
            if (cycle.DiscardCount != shownDiscard)
            {
                shownDiscard = cycle.DiscardCount;
                discardText.SetText("{0}", shownDiscard);
            }
            if (cycle.ExhaustedCount != shownExhausted)
            {
                shownExhausted = cycle.ExhaustedCount;
                exhaustedText.gameObject.SetActive(shownExhausted > 0);
                exhaustedText.SetText(L10n.Get(L10nKeys.HudExhausted), shownExhausted);
            }

            bool reshuffling = cycle.IsReshuffling;
            if (reshuffling != shownReshuffling)
            {
                shownReshuffling = reshuffling;
                if (deckLabel != null) deckLabel.text = L10n.Get(reshuffling ? L10nKeys.HudReshuffling : L10nKeys.HudDeck);
                if (reshuffleRing != null) reshuffleRing.gameObject.SetActive(reshuffling);
            }
            if (reshuffling && reshuffleRing != null)
                reshuffleRing.fillAmount = cycle.ReshuffleProgress;
        }
    }
}
