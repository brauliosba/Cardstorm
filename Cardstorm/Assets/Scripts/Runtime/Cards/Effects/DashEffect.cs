using System;
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

        // Siempre en la dirección del movimiento, aunque la carta apunte a un enemigo.
        public override void Apply(in CardContext context)
        {
            var caster = context.Caster;
            caster.Movement.Dash(caster.Movement.LastMoveDirection, distance, duration);
            caster.Health.GrantInvulnerability(invulnerability);
        }
    }
}
