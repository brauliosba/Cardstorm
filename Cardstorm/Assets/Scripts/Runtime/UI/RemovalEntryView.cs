using Cardstorm.Cards;
using Cardstorm.Localization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Una carta del mazo en las listas de eliminar.
    [RequireComponent(typeof(Button))]
    public class RemovalEntryView : MonoBehaviour
    {
        const float BlockedVeil = 0.6f;

        [SerializeField] Image border;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text countText;
        [SerializeField] TMP_Text typeText;
        [SerializeField] TMP_Text costText;
        [Tooltip("Imagen oscura sobre la carta cuando no se puede elegir.")]
        [SerializeField] Image veil;
        [Tooltip("Rombo del color de la gema; oculto si la copia no tiene.")]
        [SerializeField] Image gemBadge;

        Button button;
        Tween wobble;

        public Button Button => button != null ? button : button = GetComponent<Button>();

        void Awake() => button = GetComponent<Button>();

        void OnDisable()
        {
            wobble?.Kill();
            transform.localRotation = Quaternion.identity;
        }

        public void Show(RemovalEntry entry)
        {
            gameObject.SetActive(true);

            var card = entry.Card;
            var gem = entry.Gem;
            Color color = CardPalette.For(card.type);
            if (border != null) border.color = color;
            if (icon != null)
            {
                icon.sprite = card.icon;
                icon.enabled = card.icon != null;
                icon.color = color;
            }
            nameText.text = L10n.CardName(card);
            countText.SetText("×{0}", entry.Count);
            if (typeText != null)
            {
                typeText.text = !string.IsNullOrEmpty(entry.Note) ? entry.Note : HudColors.TypeLine(card.type, gem);
                typeText.color = color;
            }
            if (costText != null)
                costText.text = HudColors.CostLabel(entry.Instance != null ? entry.Instance.Cost : card.cost);
            if (gemBadge != null)
            {
                gemBadge.gameObject.SetActive(gem != null);
                if (gem != null) gemBadge.color = gem.color;
            }

            Button.interactable = entry.Allowed;
            if (veil != null)
            {
                veil.gameObject.SetActive(!entry.Allowed);
                var c = veil.color;
                c.a = BlockedVeil;
                veil.color = c;
            }
        }

        public void Wobble() => UiTweens.Wobble(transform, ref wobble);

        public void Hide() => gameObject.SetActive(false);
    }
}
