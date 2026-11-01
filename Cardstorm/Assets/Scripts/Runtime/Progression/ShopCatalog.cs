using System.Collections.Generic;
using Cardstorm.Cards;
using UnityEngine;

namespace Cardstorm.Progression
{
    // Cartas que puede ofrecer la tienda.
    [CreateAssetMenu(menuName = "Cardstorm/Catálogo de la tienda", fileName = "ShopCatalog")]
    public class ShopCatalog : ScriptableObject
    {
        [Tooltip("Cartas de acción, básicas incluidas. Cada compra añade una copia al mazo.")]
        public List<CardDefinition> actionCards = new();

        [Tooltip("Cartas de poder. Dejan de salir al llegar a su máximo de copias.")]
        public List<CardDefinition> powerCards = new();

        void OnValidate()
        {
            for (int i = 0; i < actionCards.Count; i++)
            {
                var card = actionCards[i];
                if (card == null) Debug.LogWarning($"{name}: carta de acción vacía en la posición {i}.", this);
                else if (card.type == CardType.Power) Debug.LogWarning($"{name}: {card.name} es de poder y está entre las de acción.", this);
            }

            for (int i = 0; i < powerCards.Count; i++)
            {
                var card = powerCards[i];
                if (card == null) Debug.LogWarning($"{name}: carta de poder vacía en la posición {i}.", this);
                else if (card.type != CardType.Power) Debug.LogWarning($"{name}: {card.name} no es de poder y está entre las de poder.", this);
            }
        }
    }
}
