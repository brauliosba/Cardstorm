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

        // Sacudida al pulsar sin poder jugar.
        const float ShakeTime = 0.15f;
        const float ShakeAmplitude = 8f;
        const float ShakeFrequency = 50f;

        // «Pop» al jugar la carta.
        const float PopTime = 0.1f;
        const float PopScale = 1.1f;

        // Destello rojo del coste al pulsar sin energía.
        const float CostFlashTime = 0.25f;

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

        RectTransform rect;
        Vector2 basePosition;
        CardInstance shownCard;
        SlotState? shownState;
        bool? shownInfo;
        bool? shownCostRed;
        bool info;
        float shakeTimer;
        float popTimer;
        float costFlashTimer;

        void Awake()
        {
            rect = (RectTransform)transform;
            basePosition = rect.anchoredPosition;
        }

        public void SetKeyLabel(string label) => keyText.text = label;

        public void Played() => popTimer = PopTime;

        public void Failed(PlayResult result)
        {
            shakeTimer = ShakeTime;
            if (result == PlayResult.NotEnoughEnergy) costFlashTimer = CostFlashTime;
        }

        public void Refresh(CardCycle<CardInstance> cycle, Energy energy, int slot, bool infoHeld)
        {
            var state = cycle.StateOf(slot);
            var card = cycle.CardIn(slot);

            if (card != shownCard)
            {
                shownCard = card;
                if (card != null) ShowCard(card.Definition);
            }

            if (state != shownState)
            {
                shownState = state;
                bool waiting = state == SlotState.Waiting || card == null;
                content.SetActive(!waiting);
                emptyFrame.SetActive(waiting);
                drawVeil.gameObject.SetActive(state == SlotState.Drawing);
            }

            Animate(Time.deltaTime);

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

            bool costRed = !affordable || costFlashTimer > 0f;
            if (costRed != shownCostRed)
            {
                shownCostRed = costRed;
                costText.color = costRed ? HudColors.NotEnough : HudColors.Bar;
            }

            SetInfo(infoHeld);
        }

        void Animate(float dt)
        {
            shakeTimer = Mathf.Max(0f, shakeTimer - dt);
            popTimer = Mathf.Max(0f, popTimer - dt);
            costFlashTimer = Mathf.Max(0f, costFlashTimer - dt);

            float shake = shakeTimer > 0f
                ? Mathf.Sin(shakeTimer * ShakeFrequency) * ShakeAmplitude * (shakeTimer / ShakeTime)
                : 0f;
            rect.anchoredPosition = basePosition + new Vector2(shake, 0f);

            float pop = 1f + (PopScale - 1f) * (popTimer / PopTime);
            float scale = (info ? InfoScale : 1f) * pop;
            rect.localScale = new Vector3(scale, scale, 1f);
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
        void SetInfo(bool value)
        {
            info = value;
            if (value == shownInfo) return;
            shownInfo = value;

            art.gameObject.SetActive(!value);
            nameText.gameObject.SetActive(!value);
            typeText.gameObject.SetActive(!value);
            descriptionText.gameObject.SetActive(value);
            if (value) transform.SetAsLastSibling();
        }
    }
}
