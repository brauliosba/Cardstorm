using System;
using Cardstorm.Core;
using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.Combat
{
    // Daño en un círculo: explosiones, heladas y meteoros.
    public static class AreaDamage
    {
        static readonly Collider2D[] Buffer = new Collider2D[128];
        static ContactFilter2D filter;
        static bool filterReady;

        // OverlapCircle ya cuenta a los enemigos cuyo borde entra en el círculo, también a los grandes.
        // Devuelve los enemigos alcanzados.
        public static int Apply(Vector2 center, float radius, float damage, float slowFactor = 1f, float slowDuration = 0f)
        {
            if (!filterReady)
            {
                filter = new ContactFilter2D { useTriggers = false };
                filter.SetLayerMask(Layers.EnemyMask);
                filterReady = true;
            }

            bool slows = slowFactor < 1f && slowDuration > 0f;
            int count = Physics2D.OverlapCircle(center, radius, filter, Buffer);
            int hits = 0;

            // Un collider por enemigo: cada uno se golpea como mucho una vez.
            for (int i = 0; i < count; i++)
            {
                if (!Buffer[i].TryGetComponent(out Enemy enemy) || !enemy.IsAlive) continue;

                // Primero la ralentización: si el golpe lo mata, ya no importa.
                if (slows) enemy.ApplySlow(slowFactor, slowDuration);
                enemy.TakeDamage(damage);
                hits++;
            }

            Array.Clear(Buffer, 0, count);
            return hits;
        }
    }
}
