using Cardstorm.Cards;
using UnityEngine;

namespace Cardstorm.UI
{
    // Colores y textos comunes del HUD greybox.
    public static class HudColors
    {
        public static readonly Color Bar = new Color32(0x1B, 0x1C, 0x21, 0xFF);
        public static readonly Color Panel = new Color32(0x2A, 0x2D, 0x36, 0xFF);
        public static readonly Color Empty = new Color32(0x3A, 0x3A, 0x40, 0xFF);
        public static readonly Color PanelBorder = new Color32(0x8A, 0x8D, 0x96, 0xFF);
        public static readonly Color Text = Color.white;
        public static readonly Color TextDim = new Color32(0xB0, 0xB3, 0xBA, 0xFF);
        public static readonly Color Energy = new Color32(0xFF, 0xC8, 0x3D, 0xFF);
        public static readonly Color EnergyEmpty = new Color32(0x4A, 0x43, 0x34, 0xFF);
        public static readonly Color NotEnough = new Color32(0xFF, 0x6B, 0x6B, 0xFF);

        public static string TypeName(CardType type) => type switch
        {
            CardType.Defense => "Defensa",
            CardType.Mobility => "Movilidad",
            CardType.Summon => "Invocación",
            CardType.Power => "Poder",
            _ => "Ataque",
        };

        public static string RarityName(CardRarity rarity) => rarity switch
        {
            CardRarity.Basic => "Básica",
            CardRarity.Uncommon => "Poco común",
            CardRarity.Rare => "Rara",
            _ => "Común",
        };

        // Sin decimales si el coste es entero.
        public static string CostLabel(float cost) =>
            cost == Mathf.Round(cost) ? ((int)cost).ToString() : cost.ToString("0.#");
    }
}
