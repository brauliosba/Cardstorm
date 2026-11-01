using TMPro;
using UnityEngine;

namespace Cardstorm.UI
{
    // Una línea del panel de estadísticas: nombre a la izquierda y valor a la derecha.
    public class StatRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text valueText;

        public void SetName(string text) => nameText.text = text;

        public void SetValue(string text, bool capped)
        {
            valueText.text = capped ? text + " (máx.)" : text;
            valueText.color = capped ? HudColors.Energy : HudColors.Text;
        }
    }
}
