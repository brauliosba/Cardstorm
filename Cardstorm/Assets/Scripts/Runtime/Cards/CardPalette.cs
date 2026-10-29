using UnityEngine;

namespace Cardstorm.Cards
{
    // Color de cada tipo de carta. Lo usan el HUD y los efectos; contrasta con los enemigos y con el suelo.
    public static class CardPalette
    {
        public static readonly Color Attack = new Color32(0xF5, 0xD5, 0x47, 0xFF);    // Amarillo
        public static readonly Color Defense = new Color32(0x81, 0xC7, 0x84, 0xFF);   // Verde
        public static readonly Color Mobility = new Color32(0x4F, 0xC3, 0xF7, 0xFF);  // Cian
        public static readonly Color Summon = new Color32(0xCE, 0x93, 0xD8, 0xFF);    // Lila
        public static readonly Color Power = new Color32(0xFF, 0xB7, 0x4D, 0xFF);     // Ámbar

        public static Color For(CardType type) => type switch
        {
            CardType.Defense => Defense,
            CardType.Mobility => Mobility,
            CardType.Summon => Summon,
            CardType.Power => Power,
            _ => Attack,
        };
    }
}
