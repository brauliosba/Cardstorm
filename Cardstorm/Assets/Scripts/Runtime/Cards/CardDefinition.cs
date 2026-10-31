using System.Collections.Generic;
using Cardstorm.Cards.Effects;
using UnityEngine;

namespace Cardstorm.Cards
{
    // Datos de una carta. Se crea y se equilibra desde el inspector.
    [CreateAssetMenu(menuName = "Cardstorm/Carta", fileName = "Card_")]
    public class CardDefinition : ScriptableObject
    {
        [Header("Identidad")]
        public string id;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        public CardType type;
        public CardRarity rarity;

        [Header("Juego")]
        [Tooltip("Energía que cuesta jugarla. Admite decimales.")]
        [Min(0f)] public float cost = 1f;

        public TargetPriority targeting = TargetPriority.Nearest;

        [Tooltip("Distancia máxima a la que busca objetivo.")]
        [Min(0f)] public float targetRange = 12f;

        [Tooltip("Valor V del presupuesto de poder. Solo informativo.")]
        public float powerValue;

        [Header("Tienda")]
        [Tooltip("Copias que se pueden comprar en una partida. 0 = sin límite.")]
        [Min(0)] public int maxCopiesPerRun;

        [Tooltip("Precio antes de la inflación. 0 = el de su rareza.")]
        [Min(0)] public int basePrice;

        [Tooltip("El precio se multiplica por este valor con cada copia comprada. 1 = fijo.")]
        [Min(1f)] public float priceMultiplierPerCopy = 1f;

        [Header("Efectos")]
        [SerializeReference, SubclassSelector]
        public List<CardEffect> effects = new();

        // Las cartas de poder no van al descarte: salen del mazo.
        public bool ExhaustOnPlay => type == CardType.Power;

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                Debug.LogWarning($"La carta {name} no tiene id.", this);

            if (effects == null || effects.Count == 0)
            {
                Debug.LogWarning($"La carta {name} no tiene efectos.", this);
                return;
            }

            for (int i = 0; i < effects.Count; i++)
                if (effects[i] == null)
                    Debug.LogWarning($"La carta {name} tiene un efecto vacío en la posición {i}.", this);
        }
    }
}
