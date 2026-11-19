using System;
using Cardstorm.Core;
using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.Cards.Effects
{
    // Golpe en arco hacia el objetivo: daña a todos los enemigos dentro del sector.
    [Serializable]
    public class ArcEffect : CardEffect
    {
        // Margen sobre el alcance para encontrar enemigos grandes cuyo borde entra en el arco.
        const float SearchMargin = 1f;

        static readonly Collider2D[] Buffer = new Collider2D[64];
        static ContactFilter2D filter;
        static bool filterReady;

        [Min(0f)] public float damage = 18f;

        [Tooltip("Alcance desde el centro del jugador hasta el borde del enemigo.")]
        [Min(0.1f)] public float range = 2.5f;

        [Range(1f, 360f)] public float arcDegrees = 120f;

        [Tooltip("Barrido del golpe, dibujado hacia +X. Si está vacío se usa el ArcFlash.")]
        public ParticleSystem vfx;

        [Tooltip("Alcance al que está dibujado el barrido.")]
        [Min(0.1f)] public float vfxRange = 2.5f;

        [Tooltip("Corte en cada enemigo alcanzado, girado hacia él.")]
        public ParticleSystem hitVfx;

        [Tooltip("Segundos que tarda el golpe en llegar al alcance, frenando como la onda del VFX. 0 = inmediato.")]
        [Min(0f)] public float waveTime;

        // La onda dibujada se abre con r = 1 - (1 - t)^WaveEase: el golpe llega en t = 1 - (1 - r)^(1 / WaveEase).
        const float WaveEase = 2.2f;

        public override CardTraits Traits =>
            CardTraits.Damage | CardTraits.Area | (arcDegrees < 360f ? CardTraits.Targeted : CardTraits.None);

        public override void Apply(in CardContext context)
        {
            if (!filterReady)
            {
                filter = new ContactFilter2D { useTriggers = false };
                filter.SetLayerMask(Layers.EnemyMask);
                filterReady = true;
            }

            float finalDamage = damage * context.DamageMultiplier;
            float finalRange = range * context.AreaMultiplier;
            float finalArc = Mathf.Min(360f, arcDegrees * context.AreaMultiplier);
            Vector2 origin = context.Origin;
            var vfxManager = context.Caster.Vfx;
            var strikes = waveTime > 0f ? context.Caster.Strikes : null;
            int count = Physics2D.OverlapCircle(origin, finalRange + SearchMargin, filter, Buffer);

            // Un collider por enemigo: cada uno se golpea como mucho una vez.
            for (int i = 0; i < count; i++)
            {
                if (!Buffer[i].TryGetComponent(out Enemy enemy) || !enemy.IsAlive) continue;

                Vector2 pos = enemy.Body.position;
                float edge = Vector2.Distance(origin, pos) - enemy.Radius;
                if (edge > finalRange) continue;
                if (!Targeting.InArc(origin, context.Aim, pos, finalArc)) continue;

                if (strikes != null)
                {
                    float reach = Mathf.Clamp01(edge / finalRange);
                    float delay = waveTime * (1f - Mathf.Pow(1f - reach, 1f / WaveEase));
                    strikes.ScheduleHit(enemy, finalDamage, delay, origin, context.Source, hitVfx);
                    continue;
                }

                enemy.TakeDamage(finalDamage, context.Source);
                if (vfxManager != null) vfxManager.Burst(hitVfx, pos, pos - origin, orient: true);
            }

            Array.Clear(Buffer, 0, count);

            if (vfx != null && vfxManager != null)
            {
                vfxManager.Burst(vfx, origin, context.Aim, orient: true, scale: finalRange / vfxRange);
                return;
            }

            var flash = context.Caster.ArcFlash;
            if (flash != null) flash.Show(context.Aim, finalRange, finalArc, CardPalette.For(context.Card.type));
        }
    }
}
