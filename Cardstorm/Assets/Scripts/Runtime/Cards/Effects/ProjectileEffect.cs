using System;
using UnityEngine;

namespace Cardstorm.Cards.Effects
{
    // Lanza uno o varios proyectiles hacia el objetivo. Con varios, se reparten en abanico.
    [Serializable]
    public class ProjectileEffect : CardEffect
    {
        [Min(0f)] public float damage = 10f;

        [Tooltip("Unidades/s.")]
        [Min(0.1f)] public float speed = 15f;

        [Tooltip("Distancia a la que desaparece.")]
        [Min(0.1f)] public float maxDistance = 14f;

        [Min(0.01f)] public float radius = 0.2f;

        [Tooltip("Enemigos que atraviesa además del primero.")]
        [Min(0)] public int pierce;

        [Min(1)] public int count = 1;

        [Tooltip("Ángulo total del abanico cuando hay más de un proyectil.")]
        [Range(0f, 360f)] public float spreadDegrees;

        public override void Apply(in CardContext context)
        {
            var projectiles = context.Caster.Projectiles;
            if (projectiles == null) return;

            float finalDamage = damage * context.Caster.DamageMultiplier;
            Color color = CardPalette.For(context.Card.type);

            for (int i = 0; i < count; i++)
            {
                float angle = count > 1 ? -spreadDegrees * 0.5f + spreadDegrees * i / (count - 1) : 0f;
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * context.Aim;
                projectiles.Launch(context.Origin, dir, speed, maxDistance, radius, finalDamage, pierce, color);
            }
        }
    }
}
