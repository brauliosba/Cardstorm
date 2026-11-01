using System;

namespace Cardstorm.Progression
{
    // Precios de la tienda. Cartas y refrescos suben con el nivel por la inflación; eliminar no.
    public static class Pricing
    {
        public static double Inflation(ProgressionRules rules, int level) =>
            1.0 + rules.inflationPerLevel * Math.Max(0, level - 1);

        // basePrice · multiplicador^copias · inflación, redondeado al entero.
        public static int CardPrice(ProgressionRules rules, float basePrice, float multiplierPerCopy, int copiesOwned, int level)
        {
            double price = basePrice * Math.Pow(multiplierPerCopy, Math.Max(0, copiesOwned)) * Inflation(rules, level);
            return Round(price);
        }

        // Refrescar por k-ésima vez en la misma tienda: paso · k · inflación.
        public static int RefreshPrice(ProgressionRules rules, int level, int k) =>
            Round(rules.refreshPriceStep * Math.Max(1, k) * Inflation(rules, level));

        // Eliminar una carta: base + paso · eliminaciones anteriores, sin inflación.
        public static int RemovalPrice(ProgressionRules rules, int previousRemovals) =>
            rules.removalBasePrice + rules.removalStep * Math.Max(0, previousRemovals);

        static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
