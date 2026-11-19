using System;
using Cardstorm.Combat;
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

        [Header("VFX")]
        [Tooltip("Opcional. Sin él, el proyectil es el círculo del color del tipo.")]
        public ProjectileVfx vfx;
        [Tooltip("Polvo al aterrizar.")]
        public ParticleSystem appearVfx;
        [Tooltip("Nube al desaparecer.")]
        public ParticleSystem endVfx;

        public override CardTraits Traits => CardTraits.Damage | CardTraits.Area;

        public override void Apply(in CardContext context)
        {
            var caster = context.Caster;
            if (caster.Turrets == null) return;

            var look = new TurretManager.TurretLook { Projectile = vfx, Appear = appearVfx, End = endVfx };
            // El daño se fija al ponerla: una Furia posterior no la cambia.
            caster.Turrets.Place(context.Origin, duration, fireInterval, damage * context.DamageMultiplier, range * context.AreaMultiplier,
                projectileSpeed, projectileRadius, projectileDistance, CardPalette.For(context.Card.type), context.Source, look);
        }
    }
}
