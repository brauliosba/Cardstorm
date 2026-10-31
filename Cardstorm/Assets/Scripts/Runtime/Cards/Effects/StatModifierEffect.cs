using System;
using Cardstorm.Progression;
using UnityEngine;

namespace Cardstorm.Cards.Effects
{
    // Mejora permanente de una estadística del personaje durante la partida (cartas de poder).
    [Serializable]
    public class StatModifierEffect : CardEffect
    {
        public StatType stat;

        [Tooltip("Unidades, o fracción de la base si es porcentaje (0,1 = +10 %).")]
        public float amount;

        public bool isPercent;

        [Tooltip("Vida que cura al jugarse (Vitalidad).")]
        [Min(0f)] public float healAmount;

        public override void Apply(in CardContext context)
        {
            var caster = context.Caster;
            var stats = caster.Stats;
            if (stats == null)
            {
                Debug.LogWarning($"{context.Card.name}: el jugador no tiene PlayerStatsBinder.", caster);
                return;
            }

            stats.Add(stat, amount, isPercent);
            if (healAmount > 0f) caster.Health.Heal(healAmount);
        }
    }
}
