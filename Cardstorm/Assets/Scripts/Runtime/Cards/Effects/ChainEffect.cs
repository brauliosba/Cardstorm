using System;
using System.Collections.Generic;
using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.Cards.Effects
{
    // Rayo que empieza en el objetivo y salta al enemigo más cercano que no haya alcanzado.
    // Sin objetivo no hace nada.
    [Serializable]
    public class ChainEffect : CardEffect
    {
        // Cubre el máximo de enemigos vivos, como los búferes de CardCaster.
        const int BufferCapacity = 600;

        static readonly List<Vector2> Positions = new(BufferCapacity);
        static readonly List<Enemy> Candidates = new(BufferCapacity);
        static readonly List<int> Hits = new(16);
        static readonly List<Enemy> HitEnemies = new(16);

        [Min(0f)] public float damage = 12f;
        [Min(1)] public int maxTargets = 4;

        [Tooltip("Distancia máxima entre un enemigo alcanzado y el siguiente.")]
        [Min(0.1f)] public float jumpRange = 4f;

        [Tooltip("Segundos entre un salto y el siguiente: el daño llega con el rayo. 0 = todos a la vez.")]
        [Min(0f)] public float hopTime;

        [Tooltip("Chasquido en cada enemigo alcanzado.")]
        public ParticleSystem hitVfx;

        [Tooltip("Destello en el jugador al lanzar el rayo, girado hacia el objetivo.")]
        public ParticleSystem muzzleVfx;

        public override CardTraits Traits => CardTraits.Damage | CardTraits.Area | CardTraits.Targeted;

        public override void Apply(in CardContext context)
        {
            var caster = context.Caster;
            if (context.Target == null || caster.Enemies == null) return;

            var active = caster.Enemies.Active;
            Positions.Clear();
            Candidates.Clear();
            int start = -1;
            for (int i = 0; i < active.Count; i++)
            {
                var e = active[i];
                if (!e.IsAlive) continue;
                if (e == context.Target) start = Candidates.Count;
                Positions.Add(e.Body.position);
                Candidates.Add(e);
            }

            Targeting.Chain(start, Positions, jumpRange * context.AreaMultiplier, maxTargets, Hits);

            // Primero se guardan los alcanzados: al morir, un enemigo sale de la lista de activos.
            HitEnemies.Clear();
            for (int i = 0; i < Hits.Count; i++) HitEnemies.Add(Candidates[Hits[i]]);

            var flashes = caster.Flashes;
            var strikes = caster.Strikes;
            var vfx = caster.Vfx;
            Color color = CardPalette.For(context.Card.type);
            float finalDamage = damage * context.DamageMultiplier;
            Vector2 from = context.Origin;
            if (vfx != null && Hits.Count > 0) vfx.Burst(muzzleVfx, from, Positions[Hits[0]] - from, orient: true);
            for (int i = 0; i < Hits.Count; i++)
            {
                Vector2 to = Positions[Hits[i]];
                if (strikes != null)
                {
                    var previous = i > 0 ? HitEnemies[i - 1] : null;
                    strikes.ScheduleChainHit(HitEnemies[i], previous, from, finalDamage, hopTime * i, color, context.Source, hitVfx);
                }
                else
                {
                    if (flashes != null) flashes.Bolt(from, to, color);
                    HitEnemies[i].TakeDamage(finalDamage, context.Source);
                    if (vfx != null) vfx.Burst(hitVfx, to, to - from, orient: true);
                }
                from = to;
            }

            Candidates.Clear();
            HitEnemies.Clear();
        }
    }
}
