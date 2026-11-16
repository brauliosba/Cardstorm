using System.Collections.Generic;

namespace Cardstorm.Progression
{
    // Mejorar carta abierta: tres gemas distintas y sus refrescos.
    public class GemOffer<TGem> where TGem : class, IGemItem
    {
        public const int SlotCount = 3;

        readonly RunState state;
        readonly List<TGem> catalog = new();
        readonly TGem[] slots = new TGem[SlotCount];
        readonly List<string> rolledIds = new(SlotCount);

        public GemOffer(IEnumerable<TGem> gems, RunState state)
        {
            this.state = state;
            // Sin huecos ni gemas repetidas, para que las ranuras salgan distintas.
            if (gems != null)
                foreach (var gem in gems)
                    if (gem != null && !catalog.Contains(gem)) catalog.Add(gem);
        }

        public RunState State => state;
        public int Level { get; private set; }

        // Veces que se han refrescado las gemas de este nivel; vuelve a 0 al abrir el siguiente.
        public int Refreshes { get; private set; }

        public int RefreshPrice => Pricing.GemRefreshPrice(state.Rules, Level, Refreshes + 1);

        // null si la ranura queda vacía (no había gemas suficientes).
        public TGem Slot(int index) => index >= 0 && index < slots.Length ? slots[index] : null;

        public void Open(int level, double time)
        {
            Level = level;
            Refreshes = 0;
            Roll(time);
            RunEvents.RaiseGemsOffered(time, level);
        }

        public bool TryRefresh(double time)
        {
            int price = RefreshPrice;
            if (!state.TrySpendGold(price)) return false;

            Refreshes++;
            Roll(time);

            RunEvents.RaiseGemsRefreshed(time, price);
            RunEvents.RaiseGoldSpent(time, price, SpendReason.GemRefresh);
            return true;
        }

        // Fisher-Yates parcial: las primeras posiciones quedan sorteadas.
        void Roll(double time)
        {
            int count = System.Math.Min(SlotCount, catalog.Count);
            for (int i = 0; i < SlotCount; i++)
            {
                if (i < count)
                {
                    int j = state.Random.Next(i, catalog.Count);
                    (catalog[i], catalog[j]) = (catalog[j], catalog[i]);
                    slots[i] = catalog[i];
                }
                else slots[i] = null;
            }

            rolledIds.Clear();
            foreach (var gem in slots) rolledIds.Add(gem?.Id);
            RunEvents.RaiseGemsRolled(time, rolledIds);
        }
    }
}
