using System;

namespace Cardstorm.Progression
{
    // Lo que suelta un enemigo al morir. La experiencia se multiplica por el refuerzo del tramo; el oro, no.
    // Los multiplicadores de Erudición y Codicia se aplican al recoger, en RunState.
    public static class DropRoller
    {
        public static float Xp(int baseXp, float xpMultiplier) => Math.Max(0f, baseXp * xpMultiplier);

        // Siempre una moneda de coinValue; con probabilidad doubleChance vale el doble (dime).
        public static int RollGold(int coinValue, float doubleChance, Random random)
        {
            if (coinValue <= 0) return 0;
            if (doubleChance <= 0f) return coinValue;
            if (doubleChance >= 1f) return coinValue * 2;
            return random.NextDouble() < doubleChance ? coinValue * 2 : coinValue;
        }

        // Con probabilidad chance suelta un orbe de energía.
        public static bool RollEnergy(float chance, Random random)
        {
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;
            return random.NextDouble() < chance;
        }

        public static double ExpectedGold(int coinValue, float doubleChance) =>
            Math.Max(0, coinValue) * (1.0 + Math.Clamp(doubleChance, 0f, 1f));
    }
}
