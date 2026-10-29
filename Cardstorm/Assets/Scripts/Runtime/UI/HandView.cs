using Cardstorm.Cards;
using Cardstorm.Player;
using UnityEngine;

namespace Cardstorm.UI
{
    // La mano en fila. Las ranuras van de izquierda a derecha en el orden de los botones: arriba, izquierda, derecha, abajo.
    public class HandView : MonoBehaviour
    {
        [SerializeField] PlayerCards cards;
        [SerializeField] PlayerInputReader input;
        [SerializeField] CardSlotView[] slots;

        void Awake()
        {
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
        }

        void OnEnable()
        {
            if (input != null) input.DeviceChanged += RefreshKeyLabels;
            if (cards != null)
            {
                cards.CardPlayed += OnCardPlayed;
                cards.PlayFailed += OnPlayFailed;
            }
        }

        void OnDisable()
        {
            if (input != null) input.DeviceChanged -= RefreshKeyLabels;
            if (cards != null)
            {
                cards.CardPlayed -= OnCardPlayed;
                cards.PlayFailed -= OnPlayFailed;
            }
        }

        void Start() => RefreshKeyLabels();

        void LateUpdate()
        {
            if (cards == null || cards.Cycle == null) return;

            var cycle = cards.Cycle;
            bool info = input != null && input.CardInfoHeld;
            int count = Mathf.Min(slots.Length, cycle.HandSize);
            for (int i = 0; i < count; i++)
                slots[i].Refresh(cycle, cards.Energy, i, info);
        }

        void OnCardPlayed(int slot, CardInstance _)
        {
            if (slot < slots.Length) slots[slot].Played();
        }

        void OnPlayFailed(int slot, PlayResult result)
        {
            if (slot < slots.Length) slots[slot].Failed(result);
        }

        void RefreshKeyLabels()
        {
            if (input == null) return;
            int count = Mathf.Min(slots.Length, input.CardSlotCount);
            for (int i = 0; i < count; i++)
                slots[i].SetKeyLabel(input.CardBindingLabel(i));
        }
    }
}
