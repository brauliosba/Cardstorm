namespace Cardstorm.Cards
{
    // Cuentas de las gemas, sin escena.
    public static class GemRules
    {
        // La carta tiene todos los rasgos que pide la gema.
        public static bool IsCompatible(CardTraits card, CardTraits required) => (card & required) == required;

        // Sin mínimo: la energía es continua.
        public static float Cost(float baseCost, in GemModifiers m) => baseCost * m.costMultiplier;

        public static int Pierce(int basePierce, in GemModifiers m) => basePierce + m.extraPierce;

        // Los impactos 0 a basePierce hacen el daño completo; los que añade la gema, la fracción.
        public static float HitDamage(float damage, int hitIndex, int basePierce, in GemModifiers m) =>
            hitIndex <= basePierce ? damage : damage * m.extraPierceDamage;

        // El azar lo pone quien llama. En float: con 0,2, una tirada de 0,2 no repite.
        public static bool Repeats(in GemModifiers m, double roll) => (float)roll < m.repeatChance;
    }
}
