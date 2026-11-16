using System;

namespace Cardstorm.Telemetry
{
    // Código anónimo del jugador, para juntar sus partidas con el cuestionario.
    public static class PlayerCode
    {
        // Sin I, L, O, 0 ni 1, que se confunden al copiarlos.
        public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        public const int Length = 4;

        public static string Generate(Random random)
        {
            var chars = new char[Length];
            for (int i = 0; i < Length; i++) chars[i] = Alphabet[random.Next(Alphabet.Length)];
            return new string(chars);
        }
    }
}
