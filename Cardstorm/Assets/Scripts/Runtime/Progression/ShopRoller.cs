using System;
using System.Collections.Generic;
using Cardstorm.Cards;

namespace Cardstorm.Progression
{
    // Sortea las cartas de la tienda y calcula sus precios.
    // Ranuras de acción: rareza por pesos y después una carta de esa rareza; se pueden repetir.
    // Ranuras de poder: cartas distintas que aún no han llegado a su máximo de copias.
    public class ShopRoller<TItem> where TItem : class, IShopItem
    {
        public const int ActionSlots = 3;
        public const int PowerSlots = 3;

        readonly ProgressionRules rules;
        readonly IReadOnlyList<TItem> actionItems;
        readonly IReadOnlyList<TItem> powerItems;
        readonly List<TItem> candidates = new();

        public ShopRoller(ProgressionRules rules, IReadOnlyList<TItem> actionItems, IReadOnlyList<TItem> powerItems)
        {
            this.rules = rules;
            this.actionItems = actionItems ?? Array.Empty<TItem>();
            this.powerItems = powerItems ?? Array.Empty<TItem>();
        }

        // Las básicas se sortean y se cobran como comunes.
        public static CardRarity ShopRarity(CardRarity rarity) =>
            rarity == CardRarity.Basic ? CardRarity.Common : rarity;

        public int BasePrice(TItem item) =>
            item.BasePrice > 0 ? item.BasePrice : rules.RarityPrice(ShopRarity(item.Rarity));

        public int Price(TItem item, int level, int copiesOwned) =>
            Pricing.CardPrice(rules, BasePrice(item), item.PriceMultiplierPerCopy, copiesOwned, level);

        public void RollActions(RunState state, double time, List<TItem> result)
        {
            for (int i = 0; i < ActionSlots; i++)
            {
                var item = RollAction(state, time);
                if (item != null) result.Add(item);
            }
        }

        // null si no hay ninguna carta de acción disponible.
        public TItem RollAction(RunState state, double time)
        {
            bool allowRare = time >= rules.rareMinTime;
            var rolled = RollRarity(state.Random, allowRare);

            // Prueba la rareza sorteada y las inferiores; si ninguna tiene cartas, las superiores.
            for (var r = rolled; r >= CardRarity.Common; r--)
            {
                var item = PickAction(state, r);
                if (item != null) return item;
            }
            for (var r = rolled + 1; r <= CardRarity.Rare; r++)
            {
                if (r == CardRarity.Rare && !allowRare) break;
                var item = PickAction(state, r);
                if (item != null) return item;
            }
            return null;
        }

        // Hasta PowerSlots cartas distintas; menos si no quedan tantas disponibles.
        public void RollPowers(RunState state, List<TItem> result)
        {
            candidates.Clear();
            foreach (var item in powerItems)
                if (IsAvailable(item, state) && !candidates.Contains(item)) candidates.Add(item);

            // Fisher-Yates parcial: las primeras posiciones quedan sorteadas.
            int count = Math.Min(PowerSlots, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                int j = state.Random.Next(i, candidates.Count);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                result.Add(candidates[i]);
            }
        }

        public static bool IsAvailable(TItem item, RunState state) =>
            item != null && (item.MaxCopies <= 0 || state.CopiesOf(item.Id) < item.MaxCopies);

        CardRarity RollRarity(Random random, bool allowRare)
        {
            float common = rules.commonWeight;
            float uncommon = rules.uncommonWeight;
            float rare = allowRare ? rules.rareWeight : 0f;
            double total = common + uncommon + rare;
            if (total <= 0.0) return CardRarity.Common;

            double roll = random.NextDouble() * total;
            if (roll < common) return CardRarity.Common;
            if (roll < common + uncommon) return CardRarity.Uncommon;
            return CardRarity.Rare;
        }

        TItem PickAction(RunState state, CardRarity rarity)
        {
            candidates.Clear();
            foreach (var item in actionItems)
                if (IsAvailable(item, state) && ShopRarity(item.Rarity) == rarity) candidates.Add(item);

            return candidates.Count > 0 ? candidates[state.Random.Next(candidates.Count)] : null;
        }
    }
}
