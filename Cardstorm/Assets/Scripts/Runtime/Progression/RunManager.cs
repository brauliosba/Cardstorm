using Cardstorm.Core;
using UnityEngine;

namespace Cardstorm.Progression
{
    // Dueño del estado de la partida. Suma la experiencia y el oro recogidos y emite los eventos de RunEvents.
    public class RunManager : MonoBehaviour
    {
        [SerializeField] ProgressionConfig config;
        [SerializeField] PlayerStatsBinder player;
        [SerializeField] GameClock clock;

        [Tooltip("Semilla del azar de la partida (drops y tienda). 0 = una distinta cada vez.")]
        [SerializeField] int seed;

        public RunState State { get; private set; }
        public int Seed { get; private set; }

        // La partida ha terminado con victoria: no se abren más tiendas.
        public bool Won { get; private set; }

        double Now => clock != null ? clock.Elapsed : Time.timeSinceLevelLoad;

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (player == null) player = FindFirstObjectByType<PlayerStatsBinder>();
            if (config == null) Debug.LogError("RunManager no tiene ProgressionConfig.", this);
        }

        // En Start porque PlayerStatsBinder crea las estadísticas en su Awake.
        void Start()
        {
            var rules = config != null ? config.rules : new ProgressionRules();
            var stats = player != null ? player.Stats : new PlayerStats(rules);
            Seed = seed != 0 ? seed : System.Environment.TickCount;
            State = new RunState(rules, stats, Seed);
        }

        public void AddXp(float xp)
        {
            if (State == null || xp <= 0f) return;

            float before = State.Levels.TotalXp;
            int startLevel = State.Level;
            int levelUps = State.AddXp(xp);

            double now = Now;
            RunEvents.RaiseXpGained(now, State.Levels.TotalXp - before);
            for (int i = 1; i <= levelUps; i++)
                RunEvents.RaiseLevelUp(now, startLevel + i);
        }

        public void Win()
        {
            if (Won) return;
            Won = true;
            RunEvents.RaiseRunWon(Now, State != null ? State.Level : 1);
        }

        public void AddGold(int amount, GoldSource source)
        {
            if (State == null) return;

            int gained = State.AddGold(amount);
            if (gained > 0) RunEvents.RaiseGoldGained(Now, gained, source);
        }
    }
}
