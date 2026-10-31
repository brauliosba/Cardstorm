using Cardstorm.Core;
using Cardstorm.Player;
using Cardstorm.UI;
using UnityEngine;

namespace Cardstorm.Progression
{
    // Pausa la partida en cada subida de nivel y muestra la tienda; al cerrar la última, la reanuda.
    // La pausa necesita GameClock.Paused (reloj, energía, robo, proyectiles, recogibles) y Time.timeScale = 0
    // (enemigos y física del jugador, que no miran GameClock).
    public class LevelUpController : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] GameClock clock;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerHealth health;
        [SerializeField] ShopScreen shop;

        [Tooltip("Invulnerabilidad al volver a la partida, para no recibir un golpe nada más cerrar la tienda.")]
        [SerializeField, Min(0f)] float resumeInvulnerability = 0.5f;

        LevelUpQueue queue;
        float previousTimeScale = 1f;

        public bool IsOpen { get; private set; }
        public LevelUpQueue Queue => queue;

        void Awake()
        {
            if (run == null) run = FindFirstObjectByType<RunManager>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            if (shop == null) shop = FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            RunEvents.LevelUp += OnLevelUp;
            if (shop != null) shop.ContinuePressed += OnContinue;
        }

        void OnDisable()
        {
            RunEvents.LevelUp -= OnLevelUp;
            if (shop != null) shop.ContinuePressed -= OnContinue;
        }

        void OnLevelUp(double time, int level)
        {
            queue ??= new LevelUpQueue(run.State.Rules);
            queue.Enqueue(level);

            if (IsOpen) shop.UpdateQueue(queue.Position, queue.Total);
            else Open();
        }

        void Open()
        {
            IsOpen = true;
            clock.Paused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            input.SetGameplayEnabled(false);
            ShowCurrent();
        }

        void ShowCurrent() =>
            shop.Show(queue.CurrentLevel, queue.Position, queue.Total, run.State.Gold, queue.CurrentOffersRemoval);

        void OnContinue()
        {
            if (!IsOpen) return;

            if (queue.Complete()) ShowCurrent();
            else Close();
        }

        void Close()
        {
            IsOpen = false;
            shop.Hide();
            Time.timeScale = previousTimeScale;

            // La derrota también usa Paused: si el jugador ha muerto, el reloj sigue parado.
            if (health == null || !health.IsDead)
            {
                clock.Paused = false;
                if (health != null) health.GrantInvulnerability(resumeInvulnerability);
            }
            input.SetGameplayEnabled(true);
        }
    }
}
