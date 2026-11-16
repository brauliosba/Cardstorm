using Cardstorm.Cards;
using Cardstorm.Localization;
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
        public static readonly Color Xp = new Color32(0x5B, 0x8C, 0xFF, 0xFF);
        public static readonly Color Gold = new Color32(0xFF, 0xD1, 0x26, 0xFF);

        public static string TypeName(CardType type) => L10n.TypeName(type);

        public static string RarityName(CardRarity rarity) => L10n.RarityName(rarity);

        // Tipo de la carta y, si la copia tiene gema, su nombre: «Ataque · Filo».
        public static string TypeLine(CardType type, GemDefinition gem) =>
            gem != null ? $"{TypeName(type)} · {L10n.GemName(gem)}" : TypeName(type);

        // Sin decimales si el coste es entero.
        public static string CostLabel(float cost) =>
            cost == Mathf.Round(cost) ? ((int)cost).ToString() : cost.ToString("0.#");
    }
}
