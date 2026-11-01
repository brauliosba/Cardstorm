using Cardstorm.Spawning;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Barra de vida del jefe en la parte superior. Solo se ve mientras hay un jefe vivo.
    public class BossHealthBar : MonoBehaviour
    {
        [SerializeField] BossSpawner bosses;

        [Tooltip("Panel hijo con CanvasGroup. Si no lo tiene, se añade al empezar.")]
        [SerializeField] GameObject root;
        [Tooltip("Image en modo Filled, horizontal, desde la izquierda.")]
        [SerializeField] Image fill;
        [SerializeField] TMP_Text nameText;

        CanvasGroup group;
        string shownName;
        float shownFraction = -1f;

        void Awake()
        {
            if (bosses == null) bosses = FindFirstObjectByType<BossSpawner>();
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;
        }

        void LateUpdate()
        {
            var boss = bosses != null ? bosses.Current : null;
            bool visible = boss != null && boss.IsAlive;
            group.alpha = visible ? 1f : 0f;
            if (!visible)
            {
                shownName = null;
                return;
            }

            string bossName = bosses.CurrentName;
            if (!ReferenceEquals(bossName, shownName))
            {
                shownName = bossName;
                if (nameText != null) nameText.text = bossName;
            }

            float fraction = boss.MaxHealth > 0f ? Mathf.Clamp01(boss.Health / boss.MaxHealth) : 0f;
            if (!Mathf.Approximately(fraction, shownFraction))
            {
                shownFraction = fraction;
                fill.fillAmount = fraction;
            }
        }
    }
}
