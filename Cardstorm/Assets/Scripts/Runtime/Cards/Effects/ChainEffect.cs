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

            Targeting.Chain(start, Positions, jumpRange, maxTargets, Hits);

            // Primero se guardan los alcanzados: al morir, un enemigo sale de la lista de activos.
            HitEnemies.Clear();
            for (int i = 0; i < Hits.Count; i++) HitEnemies.Add(Candidates[Hits[i]]);

            var flashes = caster.Flashes;
            Color color = CardPalette.For(context.Card.type);
            float finalDamage = damage * caster.DamageMultiplier;
            Vector2 from = context.Origin;
            for (int i = 0; i < Hits.Count; i++)
            {
                Vector2 to = Positions[Hits[i]];
                if (flashes != null) flashes.Beam(from, to, color);
                HitEnemies[i].TakeDamage(finalDamage);
                from = to;
            }

            Candidates.Clear();
            HitEnemies.Clear();
        }
    }
}
