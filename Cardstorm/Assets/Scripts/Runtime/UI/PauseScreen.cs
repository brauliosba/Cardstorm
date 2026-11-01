using Cardstorm.Core;
using Cardstorm.Player;
using Cardstorm.Progression;
using DG.Tweening;
using UnityEngine;

namespace Cardstorm.UI
{
    // Pausa con Esc / Start: congela la partida como la tienda y muestra un panel. Otra pulsación la reanuda.
    // Los controles de juego siguen activos para poder leer la tecla de pausa; las cartas ya se bloquean
    // porque el reloj está en pausa, y el movimiento se para con Time.timeScale = 0.
    public class PauseScreen : MonoBehaviour
    {
        [SerializeField] GameClock clock;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerHealth health;
        [SerializeField] LevelUpController levelUp;
        [SerializeField] VictoryScreen victory;

        [Tooltip("Panel hijo con CanvasGroup. Si no lo tiene, se añade al empezar.")]
        [SerializeField] GameObject root;

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
            (health == null || !health.IsDead) &&
            (clock == null || !clock.Paused);

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            if (levelUp == null) levelUp = FindFirstObjectByType<LevelUpController>();
            if (victory == null) victory = FindFirstObjectByType<VictoryScreen>(FindObjectsInactive.Include);
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        void OnEnable()
        {
            if (input != null) input.PausePressed += Toggle;
        }

        void OnDisable()
        {
            if (input != null) input.PausePressed -= Toggle;
        }

        void OnDestroy() => fade?.Kill();

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

            group.blocksRaycasts = true;
            UiTweens.Fade(group, true, fadeTime, ref fade);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = previousTimeScale;
            if (clock != null) clock.Paused = false;

            group.blocksRaycasts = false;
            UiTweens.Fade(group, false, fadeTime, ref fade);
        }
    }
}
