using System;

namespace Cardstorm.Progression
{
    // Nivel y experiencia de la partida. La experiencia sobrante pasa al nivel siguiente.
    public class LevelProgression
    {
        readonly ProgressionRules rules;

        public LevelProgression(ProgressionRules rules) => this.rules = rules;

        public int Level { get; private set; } = 1;

        // Experiencia acumulada dentro del nivel actual.
        public float Xp { get; private set; }

        public float TotalXp { get; private set; }

        public int XpNeeded => XpToNext(Level);

        // Experiencia para pasar del nivel indicado al siguiente, redondeada al entero.
        public int XpToNext(int level)
        {
            double raw = rules.xpBase + rules.xpFactor * Math.Pow(Math.Max(0, level - 1), rules.xpExponent);
            return Math.Max(1, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
        }

        // Devuelve cuántos niveles se han subido (pueden ser varios de golpe).
        public int AddXp(float xp)
        {
            if (xp <= 0f) return 0;

            Xp += xp;
            TotalXp += xp;
            int levelUps = 0;
            for (int needed = XpNeeded; Xp >= needed; needed = XpNeeded)
            {
                Xp -= needed;
                Level++;
                levelUps++;
            }
            return levelUps;
        }
    }
}
