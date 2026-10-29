using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.Cards
{
    // Lo que necesita un efecto para aplicarse: quién la lanza, desde dónde y hacia dónde.
    public readonly struct CardContext
    {
        public CardContext(CardCaster caster, CardDefinition card, Vector2 origin, Vector2 aim, Enemy target, Vector2 targetPoint)
        {
            Caster = caster;
            Card = card;
            Origin = origin;
            Aim = aim;
            Target = target;
            TargetPoint = targetPoint;
        }

        public CardCaster Caster { get; }
        public CardDefinition Card { get; }
        public Vector2 Origin { get; }

        // Dirección normalizada hacia el objetivo, o la del movimiento si no hay.
        public Vector2 Aim { get; }

        // null si no había ningún enemigo en rango.
        public Enemy Target { get; }

        // Posición del objetivo; sin objetivo, el punto a distancia de búsqueda en la dirección de apuntado.
        public Vector2 TargetPoint { get; }
    }
}
