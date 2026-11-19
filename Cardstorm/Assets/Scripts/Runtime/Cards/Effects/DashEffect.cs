using System;
using Cardstorm.Player;
using UnityEngine;

namespace Cardstorm.Cards.Effects
{
    // Desplazamiento rápido en la dirección del movimiento, con invulnerabilidad.
    [Serializable]
    public class DashEffect : CardEffect
    {
        [Tooltip("Unidades que recorre.")]
        [Min(0f)] public float distance = 4f;

        [Tooltip("Segundos que dura el desplazamiento.")]
        [Min(0.01f)] public float duration = 0.15f;

        [Tooltip("Segundos de invulnerabilidad desde que empieza.")]
        [Min(0f)] public float invulnerability = 0.3f;

        [Tooltip("Siluetas que deja a lo largo del recorrido (estela, con rateOverDistance).")]
        public ParticleSystem ghostVfx;

        [Tooltip("Polvo al salir. Apunta a +X y se gira hacia la dirección.")]
        public ParticleSystem startVfx;

        [Tooltip("Polvo al llegar.")]
        public ParticleSystem endVfx;

        // Siempre en la dirección del movimiento, aunque la carta apunte a un enemigo.
        public override void Apply(in CardContext context)
        {
            var caster = context.Caster;
            var visuals = new PlayerMovement.DashVisuals { Vfx = caster.Vfx, Ghost = ghostVfx, Start = startVfx, End = endVfx };
            caster.Movement.Dash(caster.Movement.LastMoveDirection, distance, duration, visuals);
            caster.Health.GrantInvulnerability(invulnerability);
        }
    }
}
