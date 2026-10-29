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

        public override void Apply(in CardContext context)
        {
            if (!filterReady)
            {
                filter = new ContactFilter2D { useTriggers = false };
                filter.SetLayerMask(Layers.EnemyMask);
                filterReady = true;
            }

            float finalDamage = damage * context.Caster.DamageMultiplier;
            Vector2 origin = context.Origin;
            int count = Physics2D.OverlapCircle(origin, range + SearchMargin, filter, Buffer);

            // Un collider por enemigo: cada uno se golpea como mucho una vez.
            for (int i = 0; i < count; i++)
            {
                if (!Buffer[i].TryGetComponent(out Enemy enemy) || !enemy.IsAlive) continue;

                Vector2 pos = enemy.Body.position;
                if (Vector2.Distance(origin, pos) - enemy.Radius > range) continue;
                if (!Targeting.InArc(origin, context.Aim, pos, arcDegrees)) continue;

                enemy.TakeDamage(finalDamage);
            }

            Array.Clear(Buffer, 0, count);

            var flash = context.Caster.ArcFlash;
            if (flash != null) flash.Show(context.Aim, range, CardPalette.For(context.Card.type));
        }
    }
}
