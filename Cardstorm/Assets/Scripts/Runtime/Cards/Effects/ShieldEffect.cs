using System;
using UnityEngine;

namespace Cardstorm.Cards.Effects
{
    // Escudo que absorbe daño antes que la vida durante un tiempo.
    [Serializable]
    public class ShieldEffect : CardEffect
    {
        [Min(0f)] public float amount = 25f;

        [Tooltip("Segundos que dura si no se rompe antes.")]
        [Min(0f)] public float duration = 4f;

        public override void Apply(in CardContext context)
        {
            context.Caster.Health.GrantShield(amount, duration);
        }
    }
}
