using System;

namespace Cardstorm.Audio
{
    public enum MusicLevel { Low, Mid, High }

    // Intensidad de la música según el nombre del tramo.
    public static class MusicIntensity
    {
        static readonly string[] Low = { "aprendizaje", "respiro", "calma" };
        static readonly string[] High = { "oleada", "clímax", "climax" };

        // Los demás (Primer pico, Construcción, los de jefe o uno nuevo) van a media.
        public static MusicLevel ForSegment(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return MusicLevel.Mid;
            string n = name.Trim().ToLowerInvariant();
            if (StartsWithAny(n, Low)) return MusicLevel.Low;
            if (StartsWithAny(n, High)) return MusicLevel.High;
            return MusicLevel.Mid;
        }

        static bool StartsWithAny(string name, string[] prefixes)
        {
            for (int i = 0; i < prefixes.Length; i++)
                if (name.StartsWith(prefixes[i], StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
