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
        [Tooltip("Tiempo que le queda (1:30). Opcional; se oculta si el jefe no tiene límite.")]
        [SerializeField] TMP_Text timerText;
        [Tooltip("En los últimos segundos el tiempo se pone en rojo.")]
        [SerializeField, Min(0f)] float warningTime = 10f;

        CanvasGroup group;
        string shownName;
        float shownFraction = -1f;
        int shownSeconds = int.MinValue;
        Color timerColor;

        void Awake()
        {
            if (bosses == null) bosses = FindFirstObjectByType<BossSpawner>();
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;
            if (timerText != null) timerColor = timerText.color;
        }

        void LateUpdate()
        {
            var boss = bosses != null ? bosses.Current : null;
            bool visible = boss != null && boss.IsAlive && !boss.IsRetreating;
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

            UpdateTimer(bosses.CurrentTimeLeft);
        }

        // Solo reescribe el texto cuando cambia el segundo.
        void UpdateTimer(float timeLeft)
        {
            if (timerText == null) return;

            int seconds = timeLeft < 0f ? -1 : Mathf.CeilToInt(timeLeft);
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;

            bool show = seconds >= 0;
            if (timerText.gameObject.activeSelf != show) timerText.gameObject.SetActive(show);
            if (!show) return;

            timerText.SetText("{0}:{1:00}", seconds / 60, seconds % 60);
            timerText.color = seconds <= warningTime ? HudColors.NotEnough : timerColor;
        }
    }
}
