using System.Collections.Generic;
using UnityEngine;

namespace Cardstorm.Cards
{
    // Lista de cartas y copias con la que empieza la partida.
    [CreateAssetMenu(menuName = "Cardstorm/Mazo", fileName = "Deck_")]
    public class DeckDefinition : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public CardDefinition card;
            [Min(1)] public int copies;
        }

        public List<Entry> cards = new();

        public int Count
        {
            get
            {
                int n = 0;
                foreach (var entry in cards) n += entry.copies;
                return n;
            }
        }

        public List<CardInstance> Build()
        {
            var result = new List<CardInstance>(Count);
            foreach (var entry in cards)
            {
                if (entry.card == null) continue;
                for (int i = 0; i < entry.copies; i++)
                    result.Add(new CardInstance(entry.card));
            }
            return result;
        }
    }
}
