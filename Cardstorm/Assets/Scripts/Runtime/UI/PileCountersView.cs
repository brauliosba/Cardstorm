using Cardstorm.Cards;
using TMPro;
using UnityEngine;

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

        int shownDeck = -1;
        int shownDiscard = -1;
        int shownExhausted = -1;

        void Awake()
        {
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
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
                exhaustedText.SetText("Agotadas {0}", shownExhausted);
            }
        }
    }
}
