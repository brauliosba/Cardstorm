using System.Text;
using Cardstorm.Core;
using Cardstorm.Enemies;
using Cardstorm.Pickups;
using Cardstorm.Player;
using Cardstorm.Progression;
using Cardstorm.Spawning;
using UnityEngine;

namespace Cardstorm.DebugTools
{
    // Panel de depuración arriba a la izquierda (F1). Solo en el editor y en development build.
    // El texto se rehace cuatro veces por segundo; mientras FrameTimeRecorder graba solo se muestra el progreso,
    // para que el panel no afecte a la medición.
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] bool visibleOnStart = true;
        [SerializeField] GameClock clock;
        [SerializeField] WaveSpawner spawner;
        [SerializeField] EnemyManager enemies;
        [SerializeField] PlayerHealth player;
        [SerializeField] FrameTimeRecorder recorder;
        [SerializeField] DebugCheats cheats;
        [SerializeField] RunManager run;
        [SerializeField] PickupManager pickups;

        [Tooltip("Segundos que se muestra el aviso de un jefe.")]
        [SerializeField, Min(0f)] float bossNoticeTime = 6f;

        const float RefreshInterval = 0.25f;

        readonly StringBuilder sb = new(512);
        string text = string.Empty;
        float refreshTimer;

        int fpsFrames;
        float fpsTime;
        float fps;
        float frameMs;

        string bossNotice;
        float bossNoticeUntil;

        GUIStyle style;
        readonly GUIContent content = new();
        Vector2 contentSize;

        public bool Visible { get; set; }

        void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                enabled = false;
                return;
            }

            Visible = visibleOnStart;
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (spawner == null) spawner = FindFirstObjectByType<WaveSpawner>();
            if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
            if (player == null) player = FindFirstObjectByType<PlayerHealth>();
            if (recorder == null) recorder = FindFirstObjectByType<FrameTimeRecorder>();
            if (cheats == null) cheats = FindFirstObjectByType<DebugCheats>();
            if (run == null) run = FindFirstObjectByType<RunManager>();
            if (pickups == null) pickups = FindFirstObjectByType<PickupManager>();
        }

        void OnEnable()
        {
            if (spawner != null) spawner.BossTimeReached += OnBossTimeReached;
        }

        void OnDisable()
        {
            if (spawner != null) spawner.BossTimeReached -= OnBossTimeReached;
        }

        void Update()
        {
            fpsFrames++;
            fpsTime += Time.unscaledDeltaTime;
            if (fpsTime >= 1f)
            {
                fps = fpsFrames / fpsTime;
                frameMs = fpsTime * 1000f / fpsFrames;
                fpsFrames = 0;
                fpsTime = 0f;
            }

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = RefreshInterval;
                Rebuild();
            }
        }

        void Rebuild()
        {
            sb.Clear();

            if (recorder != null && recorder.IsRecording)
            {
                sb.Append("● Grabando ").Append((int)recorder.Elapsed).Append('/').Append((int)recorder.Duration).Append(" s");
                text = sb.ToString();
                return;
            }
            if (!Visible)
            {
                text = string.Empty;
                return;
            }

            sb.AppendLine("CARDSTORM · greybox   (F1 ocultar)");

            if (clock != null)
            {
                sb.Append("Tiempo ").Append(GameClock.Format(clock.Elapsed)).Append("   ×").Append(clock.TimeScale);
                if (clock.Paused) sb.Append("   [reloj en pausa]");
                if (spawner != null && spawner.SpawningPaused) sb.Append("   [apariciones en pausa]");
                sb.AppendLine();
            }

            if (spawner != null)
            {
                var segment = spawner.CurrentSegment;
                if (segment != null)
                    sb.Append("Tramo: ").Append(segment.name).Append("  (f ").Append(segment.reinforcement.ToString("0.0#")).AppendLine(")");

                var next = spawner.NextWave;
                if (next != null)
                    sb.Append("Próxima: ").Append(next.name).Append(" en ").AppendLine(GameClock.Format(spawner.TimeToNextWave));
                else
                    sb.AppendLine("Próxima: —");

                if (bossNotice != null && Time.unscaledTime < bossNoticeUntil)
                    sb.Append("¡").Append(bossNotice).AppendLine("!");
            }

            if (enemies != null)
            {
                sb.Append("Enemigos: ").Append(enemies.AliveCount);
                if (spawner != null)
                {
                    sb.Append("  (");
                    var types = spawner.Curve.enemyTypes;
                    for (int i = 0; i < types.Length; i++)
                    {
                        if (types[i] == null) continue;
                        if (i > 0) sb.Append(" · ");
                        sb.Append(types[i].displayName).Append(' ').Append(enemies.AliveOf(types[i]));
                    }
                    sb.Append(')');
                }
                sb.AppendLine();
                sb.Append("Creados ").Append(enemies.TotalSpawned)
                  .Append(" · Muertos ").Append(enemies.TotalKilled)
                  .Append(" · Reciclados ").Append(enemies.TotalDespawned);
                if (enemies.Pool != null) sb.Append(" · Pool ").Append(enemies.Pool.InstanceCount);
                sb.AppendLine();
            }

            if (player != null)
            {
                sb.Append("Vida ").Append(Mathf.CeilToInt(player.Current)).Append('/').Append(Mathf.CeilToInt(player.MaxHealth));
                if (player.GodMode) sb.Append("   [invulnerable]");
                sb.AppendLine();
            }

            var state = run != null ? run.State : null;
            if (state != null)
            {
                sb.Append("Nv. ").Append(state.Level)
                  .Append(" · XP ").Append(Mathf.FloorToInt(state.Levels.Xp)).Append('/').Append(state.Levels.XpNeeded)
                  .Append(" · Oro ").Append(state.Gold).AppendLine();
            }

            if (pickups != null)
            {
                sb.Append("Recogibles: orbes ").Append(pickups.ActiveOf(PickupKind.Xp))
                  .Append(" · monedas ").Append(pickups.ActiveOf(PickupKind.Gold))
                  .Append(" · cofres ").Append(pickups.ActiveOf(PickupKind.Chest))
                  .Append(" · Pool ").Append(pickups.InstanceCount).AppendLine();
            }

            sb.Append("FPS ").Append(Mathf.RoundToInt(fps)).Append("  (").Append(frameMs.ToString("0.0")).AppendLine(" ms)");

            if (recorder != null && recorder.LastResultPath != null)
            {
                var s = recorder.LastStats;
                sb.Append("Última medición: ").Append(s.AverageFps.ToString("0.0")).Append(" fps · 1 % ")
                  .Append(s.OnePercentLowFps.ToString("0.0")).AppendLine(" fps");
            }

            if (cheats != null && cheats.Defeated)
                sb.AppendLine().AppendLine("DERROTA · R para reiniciar");

            text = sb.ToString();
        }

        void OnGUI()
        {
            if (text.Length == 0) return;

            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                richText = false,
                padding = new RectOffset(10, 10, 8, 8),
            };

            if (!ReferenceEquals(content.text, text))
            {
                content.text = text;
                contentSize = style.CalcSize(content);
            }
            GUI.Box(new Rect(10f, 10f, contentSize.x, contentSize.y), content, style);
        }

        void OnBossTimeReached(BossEvent boss)
        {
            bossNotice = boss.name;
            bossNoticeUntil = Time.unscaledTime + bossNoticeTime;
        }
    }
}
