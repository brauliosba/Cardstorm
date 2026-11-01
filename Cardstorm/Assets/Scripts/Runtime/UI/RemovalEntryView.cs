using Cardstorm.Cards;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Una carta del mazo en la pantalla de eliminar, con las copias que hay («×4»).
    // Si no se puede quitar, se atenúa con un velo y deja de poder pulsarse.
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
        [Tooltip("Imagen oscura sobre la carta cuando no se puede eliminar.")]
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

        public void Show(CardDefinition card, int count, bool allowed)
        {
            gameObject.SetActive(true);

            Color color = CardPalette.For(card.type);
            if (border != null) border.color = color;
            if (icon != null)
            {
                icon.sprite = card.icon;
                icon.enabled = card.icon != null;
                icon.color = color;
            }
            nameText.text = card.displayName;
            countText.SetText("×{0}", count);
            if (typeText != null)
            {
                typeText.text = HudColors.TypeName(card.type);
                typeText.color = color;
            }
            if (costText != null) costText.text = HudColors.CostLabel(card.cost);

            Button.interactable = allowed;
            if (veil != null)
            {
                veil.gameObject.SetActive(!allowed);
                var c = veil.color;
                c.a = BlockedVeil;
                veil.color = c;
            }
        }

        public void Wobble() => UiTweens.Wobble(transform, ref wobble);

        public void Hide() => gameObject.SetActive(false);
    }
}
