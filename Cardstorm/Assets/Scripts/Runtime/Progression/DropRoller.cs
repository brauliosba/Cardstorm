using System;

namespace Cardstorm.Progression
{
    // Lo que suelta un enemigo al morir. La experiencia se multiplica por el refuerzo del tramo; el oro, no.
    // Los multiplicadores de Erudición y Codicia se aplican al recoger, en RunState.
    public static class DropRoller
    {
        public static float Xp(int baseXp, float xpMultiplier) => Math.Max(0f, baseXp * xpMultiplier);

        // Valor de la moneda: el oro medio repartido entre las veces que sale (mínimo 1).
        public static int CoinValue(float goldAverage, float goldChance)
        {
            if (goldAverage <= 0f || goldChance <= 0f) return 0;
            double value = goldAverage / Math.Min(1f, goldChance);
            return Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        // Con probabilidad goldChance, una moneda de CoinValue; si no, 0.
        public static int RollGold(float goldAverage, float goldChance, Random random)
        {
            int value = CoinValue(goldAverage, goldChance);
            return value > 0 && random.NextDouble() < goldChance ? value : 0;
        }
    }
}
