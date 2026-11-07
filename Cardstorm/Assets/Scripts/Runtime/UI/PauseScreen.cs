using Cardstorm.Core;
using Cardstorm.Player;
using Cardstorm.Progression;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Menú de pausa con Esc / Start: congela la partida como la tienda y muestra un panel con
    // Continuar, Reiniciar y Salir. Otra pulsación de pausa la reanuda.
    // Los controles de juego siguen activos para poder leer la tecla de pausa; las cartas ya se bloquean
    // porque el reloj está en pausa, y el movimiento se para con Time.timeScale = 0.
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
        [Tooltip("Cierra el juego. En el editor sale del modo Play.")]
        [SerializeField] Button quitButton;
        [SerializeField] TMP_Text hintText;
        [SerializeField] string hint = "Esc / Start para continuar";

        [Tooltip("Segundos del fundido, en tiempo real.")]
        [SerializeField, Min(0f)] float fadeTime = 0.15f;

        CanvasGroup group;
        Tween fade;
        float previousTimeScale = 1f;

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
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            if (hintText != null) hintText.text = hint;
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (restartButton != null) restartButton.onClick.AddListener(SceneReload.ReloadActive);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        void OnEnable()
        {
            if (input != null) input.PausePressed += Toggle;
        }

        void OnDisable()
        {
            if (input != null) input.PausePressed -= Toggle;
        }

        void OnDestroy()
        {
            fade?.Kill();
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (restartButton != null) restartButton.onClick.RemoveListener(SceneReload.ReloadActive);
            if (quitButton != null) quitButton.onClick.RemoveListener(Quit);
        }

        void Update()
        {
            // Si la selección se pierde (clic fuera), vuelve al primer botón para que el mando siga funcionando.
            if (!IsPaused) return;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null) SelectFirst();
        }

        public void Toggle()
        {
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

        void SelectFirst()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var first = resumeButton != null ? resumeButton : restartButton != null ? restartButton : quitButton;
            if (first != null) es.SetSelectedGameObject(first.gameObject);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
