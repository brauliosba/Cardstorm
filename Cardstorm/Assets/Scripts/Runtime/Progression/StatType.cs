namespace Cardstorm.Progression
{
    // Estadísticas del personaje que mejoran las cartas de poder.
    public enum StatType
    {
        MaxHealth,      // Vida máxima
        HealthRegen,    // Vida por segundo
        Damage,         // Multiplicador de daño de las cartas
        EnergyRegen,    // Energía por segundo
        MaxEnergy,      // Energía máxima
        MoveSpeed,      // Unidades por segundo
        PickupRadius,   // Radio de atracción de orbes y monedas
        XpGain,         // Multiplicador de la experiencia recogida
        GoldGain,       // Multiplicador del oro recogido
    }

    public static class StatTypes
    {
        public const int Count = 9;
    }
}
