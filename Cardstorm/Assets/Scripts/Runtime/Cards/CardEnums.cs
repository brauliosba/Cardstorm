namespace Cardstorm.Cards
{
    public enum CardType { Attack, Defense, Mobility, Summon, Power }

    public enum CardRarity { Basic, Common, Uncommon, Rare }

    // A qué apunta la carta cuando se juega.
    public enum TargetPriority
    {
        Nearest,        // Enemigo más cercano (por defecto)
        HighestHealth,  // Enemigo con más vida
        DensestArea,    // Enemigo con más vecinos alrededor
        MoveDirection,  // Dirección del movimiento (movilidad)
    }
}
