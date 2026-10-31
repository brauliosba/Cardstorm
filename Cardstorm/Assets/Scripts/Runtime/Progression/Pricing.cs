using System;

namespace Cardstorm.Progression
{
    // Precios de la tienda. Todos suben con el nivel por la inflación.
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

        static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
