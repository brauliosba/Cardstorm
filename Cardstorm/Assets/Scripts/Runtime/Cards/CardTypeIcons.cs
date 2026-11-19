using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.Cards
{
    // Icono de cada tipo de carta.
    [CreateAssetMenu(menuName = "Cardstorm/Iconos de tipo", fileName = "CardTypeIcons")]
    public class CardTypeIcons : ScriptableObject
    {
        public Sprite attack;
        public Sprite defense;
        public Sprite mobility;
        public Sprite summon;
        public Sprite power;

        public Sprite For(CardType type) => type switch
        {
            CardType.Defense => defense,
            CardType.Mobility => mobility,
            CardType.Summon => summon,
            CardType.Power => power,
            _ => attack,
        };

        // Sin asset o sin sprite, la imagen se oculta.
        public static void Apply(CardTypeIcons icons, Image image, CardType type)
        {
            if (image == null) return;
            var sprite = icons != null ? icons.For(type) : null;
            image.sprite = sprite;
            image.color = CardPalette.For(type);
            image.enabled = sprite != null;
        }
    }
}
