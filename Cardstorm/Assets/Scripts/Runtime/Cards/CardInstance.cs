namespace Cardstorm.Cards
{
    // Una copia concreta de una carta en el mazo, con su gema si la tiene.
    public class CardInstance : ICard
    {
        public CardInstance(CardDefinition definition) => Definition = definition;

        public CardDefinition Definition { get; }

        // null si no tiene gema.
        public GemDefinition Gem { get; private set; }

        public GemModifiers Modifiers => Gem != null ? Gem.modifiers : GemModifiers.None;

        public float Cost => GemRules.Cost(Definition.cost, Modifiers);
        public bool ExhaustOnPlay => Definition.ExhaustOnPlay;

        public TargetPriority Targeting
        {
            get
            {
                var m = Modifiers;
                return m.overrideTargeting ? m.targeting : Definition.targeting;
            }
        }

        // Las de poder no admiten gemas: salen del mazo al jugarse.
        public bool Accepts(GemDefinition gem) =>
            gem != null && gem != Gem && Definition != null && Definition.type != CardType.Power
            && GemRules.IsCompatible(Definition.Traits, gem.required);

        // Sustituye a la gema anterior.
        public void SetGem(GemDefinition gem) => Gem = gem;

        public override string ToString()
        {
            if (Definition == null) return "(sin carta)";
            return Gem != null ? $"{Definition.displayName} + {Gem.displayName}" : Definition.displayName;
        }
    }
}
