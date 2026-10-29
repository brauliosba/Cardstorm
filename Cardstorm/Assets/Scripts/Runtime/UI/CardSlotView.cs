using Cardstorm.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Una ranura de la mano. Solo toca textos e imágenes cuando cambia lo que muestran.
    public class CardSlotView : MonoBehaviour
    {
        const float NoEnergyAlpha = 0.45f;
        const float DrawingAlpha = 0.6f;
        const float WaitingAlpha = 0.5f;
        const float InfoScale = 1.25f;

        [SerializeField] CanvasGroup group;
        [SerializeField] GameObject emptyFrame;
        [SerializeField] GameObject content;
        [SerializeField] Image border;
        [SerializeField] Image art;
        [SerializeField] Image icon;
        [SerializeField] Image costFill;
        [SerializeField] TMP_Text costText;
        [SerializeField] TMP_Text keyText;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text typeText;
        [SerializeField] TMP_Text descriptionText;

        [Tooltip("Velo que se retira de arriba abajo durante el robo.")]
        [SerializeField] Image drawVeil;

        CardInstance shownCard;
        SlotState? shownState;
        bool? shownAffordable;
        bool? shownInfo;

        public void SetKeyLabel(string label) => keyText.text = label;

        public void Refresh(CardCycle<CardInstance> cycle, Energy energy, int slot, bool infoHeld)
        {
            var state = cycle.StateOf(slot);
            var card = cycle.CardIn(slot);

            if (card != shownCard)
            {
                shownCard = card;
                if (card != null) ShowCard(card.Definition);
                shownAffordable = null;
            }

            if (state != shownState)
            {
                shownState = state;
                bool waiting = state == SlotState.Waiting || card == null;
                content.SetActive(!waiting);
                emptyFrame.SetActive(waiting);
                drawVeil.gameObject.SetActive(state == SlotState.Drawing);
                shownAffordable = null;
            }

            if (state == SlotState.Waiting || card == null)
            {
                group.alpha = WaitingAlpha;
                SetInfo(false);
                return;
            }

            float cost = card.Cost;
            bool affordable = energy.CanAfford(cost);
            costFill.fillAmount = cost <= 0f || affordable ? 1f : Mathf.Clamp01(energy.Current / cost);

            if (state == SlotState.Drawing)
            {
                drawVeil.fillAmount = 1f - cycle.DrawProgress(slot);
                group.alpha = DrawingAlpha;
            }
            else
            {
                group.alpha = affordable ? 1f : NoEnergyAlpha;
            }

            if (affordable != shownAffordable)
            {
                shownAffordable = affordable;
                costText.color = affordable ? HudColors.Bar : HudColors.NotEnough;
            }

            SetInfo(infoHeld);
        }

        void ShowCard(CardDefinition definition)
        {
            Color color = CardPalette.For(definition.type);
            border.color = color;
            art.color = Color.Lerp(color, HudColors.Panel, 0.55f);
            icon.sprite = definition.icon;
            icon.enabled = definition.icon != null;
            costText.text = definition.cost == Mathf.Round(definition.cost)
                ? ((int)definition.cost).ToString()
                : definition.cost.ToString("0.#");
            nameText.text = definition.displayName;
            typeText.text = HudColors.TypeName(definition.type);
            descriptionText.text = definition.description;
        }

        // Con la información pulsada, la descripción sustituye al dibujo y la carta se agranda.
        void SetInfo(bool info)
        {
            if (info == shownInfo) return;
            shownInfo = info;

            art.gameObject.SetActive(!info);
            nameText.gameObject.SetActive(!info);
            typeText.gameObject.SetActive(!info);
            descriptionText.gameObject.SetActive(info);
            transform.localScale = Vector3.one * (info ? InfoScale : 1f);
            if (info) transform.SetAsLastSibling();
        }
    }
}
