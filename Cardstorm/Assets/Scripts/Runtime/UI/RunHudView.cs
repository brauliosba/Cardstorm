using Cardstorm.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Barra de experiencia arriba con el nivel, y el oro. Solo reescribe lo que cambia.
    public class RunHudView : MonoBehaviour
    {
        [SerializeField] RunManager run;

        [Tooltip("Image en modo Filled, horizontal, desde la izquierda.")]
        [SerializeField] Image xpFill;
        [SerializeField] TMP_Text levelText;
        [SerializeField] TMP_Text goldText;

        int shownLevel = -1;
        int shownGold = -1;
        float shownFraction = -1f;

        void Awake()
        {
            if (run == null) run = FindFirstObjectByType<RunManager>();
        }

        void LateUpdate()
        {
            var state = run != null ? run.State : null;
            if (state == null) return;

            if (state.Level != shownLevel)
            {
                shownLevel = state.Level;
                levelText.SetText("Nv. {0}", shownLevel);
            }

            if (state.Gold != shownGold)
            {
                shownGold = state.Gold;
                goldText.SetText("Oro {0}", shownGold);
            }

            var levels = state.Levels;
            float fraction = levels.XpNeeded > 0 ? Mathf.Clamp01(levels.Xp / levels.XpNeeded) : 0f;
            if (!Mathf.Approximately(fraction, shownFraction))
            {
                shownFraction = fraction;
                xpFill.fillAmount = fraction;
            }
        }
    }
}
