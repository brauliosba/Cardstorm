using System;
using UnityEngine;

namespace Cardstorm.Cards.Effects
{
    // Coloca una torreta fija en la posición del jugador que dispara al enemigo más cercano.
    [Serializable]
    public class TurretEffect : CardEffect
    {
        [Tooltip("Segundos que dura la torreta.")]
        [Min(0f)] public float duration = 6f;
        [Tooltip("Segundos entre disparos.")]
        [Min(0.05f)] public float fireInterval = 0.5f;
        [Tooltip("Daño de cada disparo.")]
        [Min(0f)] public float damage = 5f;
        [Tooltip("Distancia a la que busca enemigos.")]
        [Min(0.1f)] public float range = 8f;

        [Header("Proyectil")]
        [Min(0.1f)] public float projectileSpeed = 15f;
        [Min(0.01f)] public float projectileRadius = 0.15f;
        [Min(0.1f)] public float projectileDistance = 10f;

        public override void Apply(in CardContext context)
        {
            var caster = context.Caster;
            if (caster.Turrets == null) return;

            // El daño se fija al ponerla: una Furia posterior no la cambia.
            caster.Turrets.Place(context.Origin, duration, fireInterval, damage * caster.DamageMultiplier, range,
                projectileSpeed, projectileRadius, projectileDistance, CardPalette.For(context.Card.type));
        }
    }
}
