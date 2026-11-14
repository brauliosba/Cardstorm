using System;

namespace Cardstorm.Cards.Effects
{
    // Un efecto de carta. Cada tipo nuevo de efecto es una subclase; las cartas los combinan en el inspector.
    [Serializable]
    public abstract class CardEffect
    {
        // Lo que aporta a la carta para saber qué gemas admite.
        public virtual CardTraits Traits => CardTraits.None;

        public abstract void Apply(in CardContext context);
    }
}
