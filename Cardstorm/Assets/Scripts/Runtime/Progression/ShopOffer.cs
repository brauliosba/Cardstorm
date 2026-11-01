using System.Collections.Generic;

namespace Cardstorm.Progression
{
    public class ShopSlot<TItem> where TItem : class
    {
        // null si la ranura queda vacía (no había cartas suficientes).
        public TItem Item { get; internal set; }
        public int Price { get; internal set; }
        public bool Bought { get; internal set; }
    }

    // Tienda abierta: las seis ranuras, las compras y los refrescos.
    // Las ranuras 0 a 2 son de acción y las 3 a 5 de poder. Se compran las que el oro permita.
    public class ShopOffer<TItem> where TItem : class, IShopItem
    {
        public const int SlotCount = ShopRoller<TItem>.ActionSlots + ShopRoller<TItem>.PowerSlots;

        readonly ShopRoller<TItem> roller;
        readonly RunState state;
        readonly ShopSlot<TItem>[] slots = new ShopSlot<TItem>[SlotCount];
        readonly List<TItem> rolled = new();

        public ShopOffer(ShopRoller<TItem> roller, RunState state)
        {
            this.roller = roller;
            this.state = state;
            for (int i = 0; i < slots.Length; i++) slots[i] = new ShopSlot<TItem>();
        }

        public RunState State => state;
        public int Level { get; private set; }

        // Veces que se ha refrescado esta tienda; vuelve a 0 al abrir la siguiente.
        public int Refreshes { get; private set; }

        public int RefreshPrice => Pricing.RefreshPrice(state.Rules, Level, Refreshes + 1);

        public ShopSlot<TItem> Slot(int index) => slots[index];

        public void Open(int level, double time)
        {
            Level = level;
            Refreshes = 0;
            Roll(time);
            RunEvents.RaiseShopOffered(time, level);
        }

        public bool TryBuy(int index, double time, out TItem item)
        {
            item = null;
            if (index < 0 || index >= slots.Length) return false;

            var slot = slots[index];
            if (slot.Item == null || slot.Bought || !state.TrySpendGold(slot.Price)) return false;

            slot.Bought = true;
            item = slot.Item;
            state.AddCopy(item.Id);
            UpdatePrices();

            RunEvents.RaiseCardBought(time, item.Id, slot.Price);
            RunEvents.RaiseGoldSpent(time, slot.Price, SpendReason.Buy);
            return true;
        }

        // Vuelve a sortear las seis ranuras, compradas incluidas.
        public bool TryRefresh(double time)
        {
            int price = RefreshPrice;
            if (!state.TrySpendGold(price)) return false;

            Refreshes++;
            Roll(time);

            RunEvents.RaiseShopRefreshed(time, price);
            RunEvents.RaiseGoldSpent(time, price, SpendReason.Refresh);
            return true;
        }

        void Roll(double time)
        {
            rolled.Clear();
            roller.RollActions(state, time, rolled);
            Fill(0, ShopRoller<TItem>.ActionSlots);

            rolled.Clear();
            roller.RollPowers(state, rolled);
            Fill(ShopRoller<TItem>.ActionSlots, ShopRoller<TItem>.PowerSlots);

            UpdatePrices();
        }

        void Fill(int first, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var slot = slots[first + i];
                slot.Item = i < rolled.Count ? rolled[i] : null;
                slot.Bought = false;
            }
        }

        // Una copia comprada puede encarecer las demás ranuras de la misma carta.
        void UpdatePrices()
        {
            foreach (var slot in slots)
                if (slot.Item != null && !slot.Bought)
                    slot.Price = roller.Price(slot.Item, Level, state.CopiesOf(slot.Item.Id));
        }
    }
}
