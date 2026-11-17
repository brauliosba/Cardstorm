using Cardstorm.Core;
using Cardstorm.Localization;
using Cardstorm.Player;
using Cardstorm.Progression;
using Cardstorm.Telemetry;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Menú de pausa con Esc / Start.
    public class PauseScreen : MonoBehaviour
    {
        [SerializeField] GameClock clock;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerHealth health;
        [SerializeField] LevelUpController levelUp;
        [SerializeField] VictoryScreen victory;
        [SerializeField] DefeatScreen defeat;

        [Tooltip("Panel hijo con CanvasGroup. Si no lo tiene, se añade al empezar.")]
        [SerializeField] GameObject root;

        [Header("Opcionales")]
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [Tooltip("Vuelve al menú principal. La partida queda como abandonada.")]
        [FormerlySerializedAs("quitButton")]
        [SerializeField] Button menuButton;
        [SerializeField] Button optionsButton;
        [SerializeField] OptionsScreen options;
        [SerializeField] TMP_Text hintText;
        [Tooltip("Código de jugador, para copiarlo sin acabar la partida.")]
        [SerializeField] TMP_Text codeText;
        [SerializeField] TelemetryRecorder telemetry;

        [Tooltip("Segundos del fundido, en tiempo real.")]
        [SerializeField, Min(0f)] float fadeTime = 0.15f;

        CanvasGroup group;
        Tween fade;
        float previousTimeScale = 1f;
        bool suspended;
        int frontFrame = -1;

        public bool IsPaused { get; private set; }

        // No se pausa encima de otra pantalla que ya congela la partida, ni con el jugador muerto.
        bool CanPause =>
            (levelUp == null || !levelUp.IsOpen) &&
            (victory == null || !victory.IsVisible) &&
            (defeat == null || !defeat.IsVisible) &&
            (health == null || !health.IsDead) &&
            (clock == null || !clock.Paused);

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            if (levelUp == null) levelUp = FindFirstObjectByType<LevelUpController>();
            if (victory == null) victory = FindFirstObjectByType<VictoryScreen>(FindObjectsInactive.Include);
            if (defeat == null) defeat = FindFirstObjectByType<DefeatScreen>(FindObjectsInactive.Include);
            if (telemetry == null) telemetry = FindFirstObjectByType<TelemetryRecorder>();
            if (options == null) options = FindFirstObjectByType<OptionsScreen>(FindObjectsInactive.Include);
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            ShowTexts();
            L10n.Changed += ShowTexts;
            SettingsStore.BindingsChanged += ShowTexts;
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (restartButton != null) restartButton.onClick.AddListener(SceneFlow.Restart);
            if (menuButton != null) menuButton.onClick.AddListener(SceneFlow.LoadMenu);
            if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        }

        void OnEnable()
        {
            if (input == null) return;
            input.PausePressed += Toggle;
            input.DeviceChanged += ShowTexts;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.PausePressed -= Toggle;
            input.DeviceChanged -= ShowTexts;
        }

        void OnDestroy()
        {
            fade?.Kill();
            L10n.Changed -= ShowTexts;
            SettingsStore.BindingsChanged -= ShowTexts;
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (restartButton != null) restartButton.onClick.RemoveListener(SceneFlow.Restart);
            if (menuButton != null) menuButton.onClick.RemoveListener(SceneFlow.LoadMenu);
            if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
        }

        // Se ve mientras se cambia de idioma, así que se reescribe en el momento.
        // La tecla sale de los controles actuales y del último dispositivo usado.
        void ShowTexts()
        {
            if (hintText != null)
                hintText.text = L10n.Format(L10nKeys.PauseHint, input != null ? input.BindingLabel("Pause") : "Esc");
            TelemetryUi.ShowCode(codeText, telemetry);
        }

        void Update()
        {
            // Si la selección se pierde (clic fuera), vuelve al primer botón para que el mando siga funcionando.
            if (!IsPaused || suspended) return;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null) SelectFirst();
        }

        public void Toggle()
        {
            // Con la escena tapándose, reanudar dejaría correr la partida debajo.
            if (SceneFlow.IsLoading) return;
            // Con las opciones abiertas, primero se cierran ellas. Esc también es Cancelar: si las acaba de
            // cerrar en este fotograma, no reanuda.
            if (suspended || Time.frameCount == frontFrame) return;
            if (IsPaused) Resume();
            else if (CanPause) Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            if (clock != null) clock.Paused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            ShowTexts();

            group.interactable = true;
            group.blocksRaycasts = true;
            UiTweens.Fade(group, true, fadeTime, ref fade);
            SelectFirst();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = previousTimeScale;
            if (clock != null) clock.Paused = false;

            group.interactable = false;
            group.blocksRaycasts = false;
            UiTweens.Fade(group, false, fadeTime, ref fade);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void OpenOptions()
        {
            if (!IsPaused || suspended || options == null) return;
            SetSuspended(true);
            options.Show(OnOptionsClosed);
        }

        void OnOptionsClosed()
        {
            SetSuspended(false);
            var es = EventSystem.current;
            if (es != null && IsPaused && optionsButton != null) es.SetSelectedGameObject(optionsButton.gameObject);
        }

        // Mientras las opciones están encima, la pausa no se puede pulsar ni recupera la selección.
        public void SetSuspended(bool value)
        {
            if (suspended && !value) frontFrame = Time.frameCount;
            suspended = value;
            group.interactable = IsPaused && !value;
            group.blocksRaycasts = IsPaused && !value;
        }

        void SelectFirst()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var first = resumeButton != null ? resumeButton : restartButton != null ? restartButton : menuButton;
            if (first != null) es.SetSelectedGameObject(first.gameObject);
        }
    }
}
