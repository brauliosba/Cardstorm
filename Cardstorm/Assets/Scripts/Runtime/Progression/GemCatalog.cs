using System.Collections.Generic;
using Cardstorm.Cards;
using UnityEngine;

namespace Cardstorm.Progression
{
    // Gemas que puede ofrecer Mejorar carta.
    [CreateAssetMenu(menuName = "Cardstorm/Catálogo de gemas", fileName = "GemCatalog")]
    public class GemCatalog : ScriptableObject
    {
        public List<GemDefinition> gems = new();

        void OnValidate()
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < gems.Count; i++)
            {
                var gem = gems[i];
                if (gem == null) Debug.LogWarning($"{name}: gema vacía en la posición {i}.", this);
                else if (!string.IsNullOrWhiteSpace(gem.id) && !ids.Add(gem.id))
                    Debug.LogWarning($"{name}: el id {gem.id} está repetido ({gem.name}).", this);
            }
        }
    }
}
