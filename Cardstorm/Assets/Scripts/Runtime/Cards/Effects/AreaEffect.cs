using System;
using Cardstorm.Combat;
using UnityEngine;

namespace Cardstorm.Cards.Effects
{
    public enum AreaCenter { Target, Self }

    // Daño en un círculo, al momento o tras un aviso. Puede ralentizar a los alcanzados.
    [Serializable]
    public class AreaEffect : CardEffect
    {
        [Tooltip("Target: el punto del objetivo (o delante del jugador si no hay). Self: el jugador.")]
        public AreaCenter center = AreaCenter.Target;

        [Min(0f)] public float damage = 12f;
        [Min(0.1f)] public float radius = 2f;

        [Tooltip("Segundos de aviso antes del impacto. 0 = inmediato.")]
        [Min(0f)] public float delay;

        [Tooltip("Multiplicador de la velocidad de los alcanzados. 1 = sin ralentizar.")]
        [Range(0.05f, 1f)] public float slowFactor = 1f;
        [Min(0f)] public float slowDuration;

        public override void Apply(in CardContext context)
        {
            var caster = context.Caster;
            Vector2 point = center == AreaCenter.Self ? context.Origin : context.TargetPoint;
            float finalDamage = damage * caster.DamageMultiplier;
            Color color = CardPalette.For(context.Card.type);

            if (delay > 0f && caster.Strikes != null)
            {
                caster.Strikes.Schedule(point, radius, finalDamage, delay, color, slowFactor, slowDuration);
                return;
            }

            AreaDamage.Apply(point, radius, finalDamage, slowFactor, slowDuration);
            if (caster.Flashes != null) caster.Flashes.Area(point, radius, color);
        }
    }
}
