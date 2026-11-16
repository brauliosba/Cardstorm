using System;
using System.Collections.Generic;
using Cardstorm.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Pantalla de la carta rara al derrotar a un jefe; la carta la añade BossRewardController.
    // No se puede cerrar sin elegir.
    public class RewardScreen : ModalScreen
    {
        [SerializeField] TMP_Text titleText;

        [Tooltip("Las que sobran se ocultan.")]
        [SerializeField] ShopSlotView[] slots = new ShopSlotView[3];

        public event Action<int> CardChosen;

        public int SlotCount => slots.Length;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].Button.onClick.AddListener(() => { if (AcceptsInput) CardChosen?.Invoke(index); });
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var slot in slots)
                if (slot != null) slot.Button.onClick.RemoveAllListeners();
        }

        // Bloquea la entrada al abrir para que el golpe que mata al jefe no elija carta.
        public void Show(string bossName, IReadOnlyList<CardDefinition> cards)
        {
            if (titleText != null) titleText.text = $"Recompensa del {bossName}";

            for (int i = 0; i < slots.Length; i++)
            {
                if (i < cards.Count) slots[i].ShowReward(cards[i]);
                else slots[i].Hide();
            }

            Open(lockInput: true);
        }

        protected override Selectable DefaultSelection()
        {
            foreach (var slot in slots)
                if (slot != null && IsSelectable(slot.Button)) return slot.Button;
            return null;
        }
    }
}
