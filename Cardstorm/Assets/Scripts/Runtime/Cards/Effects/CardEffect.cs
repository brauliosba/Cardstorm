using System;

namespace Cardstorm.Cards.Effects
{
    // Un efecto de carta. Cada tipo nuevo de efecto es una subclase; las cartas los combinan en el inspector.
    [Serializable]
    public abstract class CardEffect
    {
        public abstract void Apply(in CardContext context);
    }
}
