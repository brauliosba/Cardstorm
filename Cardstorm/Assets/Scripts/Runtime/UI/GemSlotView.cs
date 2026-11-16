using Cardstorm.Cards;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Una gema de Mejorar carta, con cuántas cartas del mazo la admiten. Sin ninguna, se atenúa y no se
    // puede elegir.
    [RequireComponent(typeof(Button))]
    public class GemSlotView : MonoBehaviour
    {
        const float NoCardsVeil = 0.6f;

        [SerializeField] Image border;
        [Tooltip("Rombo del color de la gema.")]
        [SerializeField] Image badge;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text descriptionText;
        [Tooltip("«7 cartas».")]
        [SerializeField] TMP_Text countText;
        [Tooltip("Imagen oscura sobre la gema cuando ninguna carta la admite.")]
        [SerializeField] Image veil;

        Button button;
        Tween wobble;

        public Button Button => button != null ? button : button = GetComponent<Button>();

        void Awake() => button = GetComponent<Button>();

        void OnDisable()
        {
            wobble?.Kill();
            transform.localRotation = Quaternion.identity;
        }

        public void Show(GemDefinition gem, int compatibleCards)
        {
            gameObject.SetActive(true);

            if (border != null) border.color = gem.color;
            if (badge != null)
            {
                if (gem.icon != null) badge.sprite = gem.icon;
                badge.color = gem.color;
            }
            nameText.text = gem.displayName;
            if (descriptionText != null) descriptionText.text = gem.description;
            if (countText != null)
            {
                if (compatibleCards == 0) countText.text = "Ninguna carta";
                else if (compatibleCards == 1) countText.text = "1 carta";
                else countText.SetText("{0} cartas", compatibleCards);
                countText.color = compatibleCards > 0 ? HudColors.TextDim : HudColors.NotEnough;
            }

            bool allowed = compatibleCards > 0;
            Button.interactable = allowed;
            if (veil != null)
            {
                veil.gameObject.SetActive(!allowed);
                var c = veil.color;
                c.a = NoCardsVeil;
                veil.color = c;
            }
        }

        public void Wobble() => UiTweens.Wobble(transform, ref wobble);

        // Ranura sin gema (el catálogo tiene menos de tres).
        public void Hide()
        {
            wobble?.Kill();
            transform.localRotation = Quaternion.identity;
            gameObject.SetActive(false);
        }
    }
}
