using Cardstorm.Core;
using Cardstorm.Localization;
using Cardstorm.Player;
using Cardstorm.Telemetry;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Pantalla de fin de partida: congela la partida, muestra el tiempo y el nivel, y ofrece reiniciar.
    public abstract class RunEndScreen : MonoBehaviour
    {
        [SerializeField] protected GameClock clock;
        [SerializeField] protected PlayerInputReader input;
        [SerializeField] protected PlayerHealth health;

        [Tooltip("Panel hijo con CanvasGroup. Si no lo tiene, se añade al empezar.")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text timeText;
        [SerializeField] TMP_Text levelText;
        [SerializeField] Button restartButton;

        [Tooltip("Opcional. Texto con la indicación para reiniciar.")]
        [SerializeField] TMP_Text hintText;

        [Tooltip("Opcional. Código de jugador para el cuestionario.")]
        [SerializeField] TMP_Text codeText;
        [Tooltip("Opcional. Abre la carpeta de telemetría.")]
        [SerializeField] Button openFolderButton;
        [SerializeField] TelemetryRecorder telemetry;

        [Tooltip("Segundos del fundido al abrir, en tiempo real.")]
        [SerializeField, Min(0f)] float fadeTime = 0.3f;

        CanvasGroup group;
        Tween fade;

        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            if (restartButton != null) restartButton.onClick.AddListener(SceneReload.ReloadActive);
            if (telemetry == null) telemetry = FindFirstObjectByType<TelemetryRecorder>();
            if (openFolderButton != null) openFolderButton.onClick.AddListener(OpenFolder);
        }

        protected virtual void OnDestroy()
        {
            fade?.Kill();
            if (restartButton != null) restartButton.onClick.RemoveListener(SceneReload.ReloadActive);
            if (openFolderButton != null) openFolderButton.onClick.RemoveListener(OpenFolder);
        }

        void OpenFolder()
        {
            if (telemetry != null) TelemetryUi.OpenFolder(telemetry);
        }

        protected void Show(double time, int level)
        {
            if (IsVisible) return;
            IsVisible = true;
            if (clock != null) clock.Paused = true;
            Time.timeScale = 0f;
            if (input != null) input.SetGameplayEnabled(false);
            if (health != null) health.GodMode = true;

            if (timeText != null) timeText.text = L10n.Format(L10nKeys.EndTime, GameClock.Format(time));
            if (levelText != null) levelText.SetText(L10n.Get(L10nKeys.Level), level);
            if (hintText != null) hintText.text = L10n.Get(L10nKeys.EndHint);
            TelemetryUi.ShowCode(codeText, telemetry);
            if (openFolderButton != null) openFolderButton.gameObject.SetActive(TelemetryUi.IsOn(telemetry));

            group.interactable = true;
            group.blocksRaycasts = true;
            UiTweens.Fade(group, true, fadeTime, ref fade);
            Select();
        }

        void Update()
        {
            // Si la selección se pierde (clic fuera), vuelve a Reiniciar para que el mando siga funcionando.
            if (!IsVisible) return;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null) Select();
        }

        void Select()
        {
            if (restartButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }
    }
}
