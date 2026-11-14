using Cardstorm.Progression;
using UnityEngine;

namespace Cardstorm.Cards
{
    // Gema que se engarza en una copia de carta y cambia lo que hace al jugarse.
    [CreateAssetMenu(menuName = "Cardstorm/Gema", fileName = "Gem_")]
    public class GemDefinition : ScriptableObject, IGemItem
    {
        [Header("Identidad")]
        public string id;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        public Color color = Color.white;

        [Header("Juego")]
        [Tooltip("Rasgos que debe tener la carta para admitirla.")]
        public CardTraits required;

        public GemModifiers modifiers = GemModifiers.None;

        string IGemItem.Id => id;

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                Debug.LogWarning($"La gema {name} no tiene id.", this);
        }
    }
}
