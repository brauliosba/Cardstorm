using System;

namespace Cardstorm.Progression
{
    // Estadísticas del personaje: base más las mejoras de las cartas de poder, con tope.
    // Los porcentajes se suman sobre la base, no se encadenan.
    public class PlayerStats
    {
        readonly float[] baseValues = new float[StatTypes.Count];
        readonly float[] maxValues = new float[StatTypes.Count];
        readonly float[] flat = new float[StatTypes.Count];
        readonly float[] percent = new float[StatTypes.Count];

        public PlayerStats(ProgressionRules rules)
        {
            for (int i = 0; i < StatTypes.Count; i++) maxValues[i] = float.MaxValue;

            foreach (var rule in rules.stats)
            {
                int i = (int)rule.stat;
                baseValues[i] = rule.baseValue;
                maxValues[i] = rule.max;
            }
        }

        public event Action<StatType> Changed;

        public float Base(StatType stat) => baseValues[(int)stat];

        public float Max(StatType stat) => maxValues[(int)stat];

        public float Value(StatType stat)
        {
            int i = (int)stat;
            return Math.Min(maxValues[i], baseValues[i] * (1f + percent[i]) + flat[i]);
        }

        // Valor / base: para los componentes que guardan su propia base (1 si la base es 0).
        public float Multiplier(StatType stat)
        {
            float b = Base(stat);
            return b != 0f ? Value(stat) / b : 1f;
        }

        // Valor − base: para los componentes que suman una bonificación.
        public float Bonus(StatType stat) => Value(stat) - Base(stat);

        public bool IsCapped(StatType stat) => Value(stat) >= Max(stat) - 1e-4f;

        // amount en unidades, o en fracción de la base si isPercent (0,1 = +10 %).
        public void Add(StatType stat, float amount, bool isPercent)
        {
            if (amount == 0f) return;

            if (isPercent) percent[(int)stat] += amount;
            else flat[(int)stat] += amount;
            Changed?.Invoke(stat);
        }
    }
}
