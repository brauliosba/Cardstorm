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

        [Tooltip("Partículas del impacto. Si se deja vacío, se dibuja el círculo del color del tipo.")]
        public ParticleSystem vfx;

        [Tooltip("Radio al que está dibujado el VFX. Se escala con el radio final.")]
        [Min(0.1f)] public float vfxRadius = 2f;

        [Tooltip("Partículas que caen sobre el punto y llegan al acabar el aviso. Solo con aviso.")]
        public ParticleSystem fallVfx;

        [Tooltip("Sacudida de cámara al impactar. 0 = ninguna.")]
        [Min(0f)] public float shake;
        [Min(0f)] public float shakeDuration = 0.25f;

        public override CardTraits Traits =>
            CardTraits.Damage | CardTraits.Area | (center == AreaCenter.Target ? CardTraits.Targeted : CardTraits.None);

        public override void Apply(in CardContext context)
        {
            var caster = context.Caster;
            Vector2 point = center == AreaCenter.Self ? context.Origin : context.TargetPoint;
            float finalDamage = damage * context.DamageMultiplier;
            float finalRadius = radius * context.AreaMultiplier;
            Color color = CardPalette.For(context.Card.type);

            if (caster.Strikes != null)
            {
                var visuals = new StrikeManager.Visuals
                {
                    Impact = vfx, Fall = fallVfx, Scale = finalRadius / vfxRadius,
                    Shake = shake, ShakeDuration = shakeDuration,
                };
                caster.Strikes.Schedule(point, finalRadius, finalDamage, delay, color, context.Source, slowFactor, slowDuration, visuals);
                return;
            }

            AreaDamage.Apply(point, finalRadius, finalDamage, context.Source, slowFactor, slowDuration);
            if (vfx != null && caster.Vfx != null) caster.Vfx.Burst(vfx, point, scale: finalRadius / vfxRadius);
            else if (caster.Flashes != null) caster.Flashes.Area(point, finalRadius, color);
        }
    }
}
