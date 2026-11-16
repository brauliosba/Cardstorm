using Cardstorm.Core;
using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.Cards
{
    // Lo que necesita un efecto para aplicarse: quién la lanza, desde dónde, hacia dónde y con qué gema.
    public readonly struct CardContext
    {
        public CardContext(CardCaster caster, CardDefinition card, in GemModifiers modifiers, in DamageSource source, Vector2 origin, Vector2 aim,
            Enemy target, Vector2 targetPoint)
        {
            Caster = caster;
            Card = card;
            Modifiers = modifiers;
            Source = source;
            Origin = origin;
            Aim = aim;
            Target = target;
            TargetPoint = targetPoint;
        }

        public CardCaster Caster { get; }
        public CardDefinition Card { get; }

        // Los de la gema de la copia, o None.
        public GemModifiers Modifiers { get; }

        // La carta y la gema, para atribuir el daño.
        public DamageSource Source { get; }

        public Vector2 Origin { get; }

        // Dirección normalizada hacia el objetivo, o la del movimiento si no hay.
        public Vector2 Aim { get; }

        // null si no había ningún enemigo en rango.
        public Enemy Target { get; }

        // Posición del objetivo; sin objetivo, el punto a distancia de búsqueda en la dirección de apuntado.
        public Vector2 TargetPoint { get; }

        // Daño del personaje (Furia) por el de la gema.
        public float DamageMultiplier => (Caster != null ? Caster.DamageMultiplier : 1f) * Modifiers.damageMultiplier;

        // Radios, alcances y arcos.
        public float AreaMultiplier => Modifiers.areaMultiplier;
    }
}
