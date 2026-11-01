using Cardstorm.Core;
using Cardstorm.Player;
using Cardstorm.Progression;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Pantalla de victoria: congela la partida como la tienda y muestra el tiempo y el nivel alcanzado.
    public class VictoryScreen : MonoBehaviour
    {
        [SerializeField] GameClock clock;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerHealth health;

        [Tooltip("Panel hijo con CanvasGroup. Si no lo tiene, se añade al empezar.")]
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text timeText;
        [SerializeField] TMP_Text levelText;
        [SerializeField] Button restartButton;

        [Tooltip("Segundos del fundido al abrir, en tiempo real.")]
        [SerializeField, Min(0f)] float fadeTime = 0.3f;

        CanvasGroup group;
        Tween fade;

        public bool IsVisible { get; private set; }

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            restartButton.onClick.AddListener(Restart);
        }

        void OnEnable() => RunEvents.RunWon += OnRunWon;

        void OnDisable() => RunEvents.RunWon -= OnRunWon;

        void OnDestroy()
        {
            fade?.Kill();
            restartButton.onClick.RemoveListener(Restart);
        }

        void OnRunWon(double time, int level)
        {
            IsVisible = true;
            if (clock != null) clock.Paused = true;
            Time.timeScale = 0f;
            if (input != null) input.SetGameplayEnabled(false);
            if (health != null) health.GodMode = true;

            timeText.text = "Tiempo " + GameClock.Format(time);
            levelText.SetText("Nivel {0}", level);

            group.interactable = true;
            group.blocksRaycasts = true;
            UiTweens.Fade(group, true, fadeTime, ref fade);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }

        void Update()
        {
            // Si la selección se pierde (clic fuera), vuelve a Reiniciar para que el mando siga funcionando.
            if (!IsVisible) return;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null) es.SetSelectedGameObject(restartButton.gameObject);
        }

        void Restart()
        {
            // La escala de tiempo sobrevive a la carga de escena.
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }
    }
}
