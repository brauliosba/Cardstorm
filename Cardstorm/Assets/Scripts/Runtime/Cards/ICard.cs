namespace Cardstorm.Cards
{
    public interface ICard
    {
        float Cost { get; }

        // Las cartas de poder salen del mazo al jugarse en lugar de ir al descarte.
        bool ExhaustOnPlay { get; }
    }
}
