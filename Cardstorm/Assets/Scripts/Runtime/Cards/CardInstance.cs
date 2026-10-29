namespace Cardstorm.Cards
{
    // Una copia concreta de una carta en el mazo.
    public class CardInstance : ICard
    {
        public CardInstance(CardDefinition definition) => Definition = definition;

        public CardDefinition Definition { get; }
        public float Cost => Definition.cost;
        public bool ExhaustOnPlay => Definition.ExhaustOnPlay;

        public override string ToString() => Definition != null ? Definition.displayName : "(sin carta)";
    }
}
