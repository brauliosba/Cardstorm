using Cardstorm.Cards;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Una carta a la venta en la tienda. Se atenúa si no llega el oro y se bloquea una vez comprada.
    // Se atenúa con un velo oscuro encima y no con transparencia: el borde ocupa todo el fondo de la
    // ranura y, con alfa, se vería su color a través de la carta.
    [RequireComponent(typeof(Button))]
    public class ShopSlotView : MonoBehaviour
    {
        const float UnaffordableVeil = 0.825f;
        const float BoughtVeil = 0.6f;

        [SerializeField] Image border;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text typeText;
        [SerializeField] TMP_Text costText;
        [SerializeField] TMP_Text rarityText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text priceText;
        [Tooltip("Etiqueta «Comprada»; sustituye al precio.")]
        [SerializeField] GameObject boughtLabel;
        [Tooltip("Imagen oscura sobre la carta, por debajo del precio y de «Comprada».")]
        [SerializeField] Image veil;
        [Tooltip("Hijo que hace el «pop» al comprar (Inner). La raíz no, porque la escala la lleva SelectionScale.")]
        [SerializeField] RectTransform content;

        Button button;
        CardDefinition shownCard;
        Tween wobble;
        Tween pop;
        Tween pricePop;

        public Button Button => button != null ? button : button = GetComponent<Button>();

        void Awake() => button = GetComponent<Button>();

        public void Show(CardDefinition card, int price, bool bought, bool affordable)
        {
            if (button == null) Awake();
            gameObject.SetActive(true);
            Draw(card);

            priceText.gameObject.SetActive(!bought);
            priceText.SetText("{0} oro", price);
            priceText.color = affordable ? HudColors.Text : HudColors.NotEnough;
            if (boughtLabel != null) boughtLabel.SetActive(bought);

            button.interactable = !bought;
            SetVeil(bought ? BoughtVeil : affordable ? 0f : UnaffordableVeil);
        }

        // Carta de recompensa: sin precio y siempre se puede coger.
        public void ShowReward(CardDefinition card)
        {
            if (button == null) Awake();
            gameObject.SetActive(true);
            Draw(card);

            priceText.gameObject.SetActive(false);
            if (boughtLabel != null) boughtLabel.SetActive(false);
            button.interactable = true;
            SetVeil(0f);
        }

        void Draw(CardDefinition card)
        {
            if (card != shownCard)
            {
                shownCard = card;
                Color color = CardPalette.For(card.type);
                border.color = color;
                icon.sprite = card.icon;
                icon.enabled = card.icon != null;
                icon.color = color;
                nameText.text = card.displayName;
                typeText.text = HudColors.TypeName(card.type);
                typeText.color = color;
                costText.text = HudColors.CostLabel(card.cost);
                rarityText.text = HudColors.RarityName(card.rarity);
                descriptionText.text = card.description;
            }
        }

        // Se ha intentado comprar sin oro suficiente.
        public void Wobble()
        {
            UiTweens.Wobble(transform, ref wobble);
            UiTweens.Pop(priceText.transform, ref pricePop, 0.3f, 0.3f);
        }

        public void Bought()
        {
            if (content != null) UiTweens.Pop(content, ref pop);
        }

        void SetVeil(float alpha)
        {
            if (veil == null) return;
            veil.gameObject.SetActive(alpha > 0f);
            var color = veil.color;
            color.a = alpha;
            veil.color = color;
        }

        // Ranura sin carta (quedan menos cartas de poder que ranuras).
        public void Hide()
        {
            wobble?.Kill();
            transform.localRotation = Quaternion.identity;
            shownCard = null;
            gameObject.SetActive(false);
        }
    }
}
