using UnityEngine;

namespace Cardstorm.Progression
{
    // Asset con las reglas de la progresión. Los números viven en ProgressionRules.
    [CreateAssetMenu(menuName = "Cardstorm/Progresión", fileName = "ProgressionConfig")]
    public class ProgressionConfig : ScriptableObject
    {
        public ProgressionRules rules = new();
    }
}
