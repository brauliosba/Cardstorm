using System.Collections.Generic;
using Cardstorm.CameraRig;
using Cardstorm.Cards;
using Cardstorm.Core;
using Cardstorm.Enemies;
using Cardstorm.Player;
using Cardstorm.Spawning;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Cardstorm.DebugTools
{
    // Teclas de depuración. Solo en el editor y en development build.
    // Leen el teclado directamente: no forman parte de los controles del juego ni de su reasignación.
    public class DebugCheats : MonoBehaviour
    {
        [SerializeField] GameClock clock;
        [SerializeField] WaveSpawner spawner;
        [SerializeField] EnemyManager enemies;
        [SerializeField] PlayerHealth player;
        [SerializeField] PlayerCards cards;
        [SerializeField] MapBounds map;
        [SerializeField] DebugOverlay overlay;
        [SerializeField] FrameTimeRecorder recorder;

        [Header("F5 · Matar enemigos cercanos")]
        [SerializeField, Min(0.5f)] float killRadius = 6f;

        [Header("F9 · Prueba de carga")]
        [Tooltip("Por defecto, el primer tipo de la curva (rondador).")]
        [SerializeField] EnemyDefinition loadTestEnemy;
        [SerializeField, Min(1)] int loadTestCount = 300;
        [SerializeField, Min(1)] int loadTestPerRing = 50;
        [SerializeField, Min(0.5f)] float loadTestRingSpacing = 1.2f;

        readonly List<Vector2> points = new(64);
        readonly List<float> jumpTargets = new();
        SpawnPositionPicker picker;
        Camera cam;

        public bool Defeated { get; private set; }

        void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                enabled = false;
                return;
            }

            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (spawner == null) spawner = FindFirstObjectByType<WaveSpawner>();
            if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
            if (player == null) player = FindFirstObjectByType<PlayerHealth>();
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
            if (map == null) map = FindFirstObjectByType<MapBounds>();
            if (overlay == null) overlay = FindFirstObjectByType<DebugOverlay>();
            if (recorder == null) recorder = FindFirstObjectByType<FrameTimeRecorder>();
        }

        void Start()
        {
            cam = Camera.main;
            if (map != null) picker = new SpawnPositionPicker(map.Rect, Layers.ObstacleMask);
            if (loadTestEnemy == null && spawner != null && spawner.Curve.enemyTypes.Length > 0)
                loadTestEnemy = spawner.Curve.enemyTypes[0];

            // Momentos a los que salta F3: inicio de cada oleada y hora de cada jefe.
            if (spawner != null)
            {
                foreach (var wave in spawner.Curve.waves) jumpTargets.Add(wave.startTime);
                foreach (var boss in spawner.Curve.bosses) jumpTargets.Add(boss.time);
                jumpTargets.Sort();
            }
        }

        void OnEnable()
        {
            if (player != null) player.Died += OnPlayerDied;
        }

        void OnDisable()
        {
            if (player != null) player.Died -= OnPlayerDied;
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;

            if (k.f1Key.wasPressedThisFrame && overlay != null) overlay.Visible = !overlay.Visible;
            if (k.f2Key.wasPressedThisFrame && clock != null) clock.CycleTimeScale();
            if (k.f3Key.wasPressedThisFrame) JumpToNextEvent();
            if (k.f4Key.wasPressedThisFrame && spawner != null) TogglePauseSpawning();
            if (k.f5Key.wasPressedThisFrame) KillNearby();
            if (k.f6Key.wasPressedThisFrame && player != null) ToggleGodMode();
            if (k.f7Key.wasPressedThisFrame) ToggleInfiniteEnergy();
            if (k.f9Key.wasPressedThisFrame) LoadTest();
            if (k.f10Key.wasPressedThisFrame && enemies != null) DespawnAll();
            if (k.f11Key.wasPressedThisFrame && recorder != null) recorder.StartRecording();
            if (k.rKey.wasPressedThisFrame) Restart();
        }

        // F3
        void JumpToNextEvent()
        {
            if (clock == null) return;
            foreach (float t in jumpTargets)
            {
                if (t > clock.Elapsed + 0.01)
                {
                    clock.JumpTo(t);
                    Debug.Log($"[DebugCheats] Salto a {GameClock.Format(t)}");
                    return;
                }
            }
        }

        // F4
        void TogglePauseSpawning()
        {
            spawner.SpawningPaused = !spawner.SpawningPaused;
            Debug.Log($"[DebugCheats] Apariciones {(spawner.SpawningPaused ? "en pausa" : "reanudadas")}");
        }

        // F5: mata a los enemigos a menos de killRadius del jugador (cuenta como muerte, no como reciclado).
        void KillNearby()
        {
            if (enemies == null || enemies.Player == null) return;
            Vector2 center = enemies.Player.position;
            float radiusSqr = killRadius * killRadius;
            int killed = 0;

            // Hacia atrás: cada muerte intercambia con el último, que ya se ha revisado.
            var active = enemies.Active;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                var enemy = active[i];
                if (((Vector2)enemy.transform.position - center).sqrMagnitude > radiusSqr) continue;
                enemy.TakeDamage(enemy.Health);
                killed++;
            }
            Debug.Log($"[DebugCheats] {killed} enemigos eliminados · instancias del pool: {(enemies.Pool != null ? enemies.Pool.InstanceCount : -1)}");
        }

        // F6
        void ToggleGodMode()
        {
            player.GodMode = !player.GodMode;
            Debug.Log($"[DebugCheats] Invulnerable: {(player.GodMode ? "sí" : "no")}");
        }

        // F7
        void ToggleInfiniteEnergy()
        {
            if (cards == null || cards.Energy == null) return;
            cards.Energy.Infinite = !cards.Energy.Infinite;
            Debug.Log($"[DebugCheats] Energía infinita: {(cards.Energy.Infinite ? "sí" : "no")}");
        }

        // F9: loadTestCount enemigos en anillos alrededor del jugador, fuera de la vista; pausa las apariciones normales.
        void LoadTest()
        {
            if (enemies == null || loadTestEnemy == null || cam == null) return;
            if (spawner != null) spawner.SpawningPaused = true;

            Vector2 center = enemies.Player.position;
            Rect view = CameraMath.ViewRect(cam.transform.position, cam.orthographicSize, cam.aspect);
            float baseRadius = FormationMath.RingRadius(view);

            int remaining = loadTestCount;
            for (int ring = 0; remaining > 0; ring++)
            {
                int count = Mathf.Min(loadTestPerRing, remaining);
                float startAngle = ring * 0.37f;  // Desfase para que los anillos no queden alineados
                FormationMath.Ring(center, count, baseRadius + ring * loadTestRingSpacing, startAngle, points);
                picker?.ResolveAll(points, loadTestEnemy.radius, center);
                foreach (var p in points)
                    enemies.Spawn(loadTestEnemy, p);
                remaining -= count;
            }
            Debug.Log($"[DebugCheats] Prueba de carga: {loadTestCount} × {loadTestEnemy.displayName}. Apariciones en pausa (F4 para reanudar).");
        }

        // F10
        void DespawnAll()
        {
            int count = enemies.AliveCount;
            enemies.DespawnAll();
            Debug.Log($"[DebugCheats] {count} enemigos reciclados");
        }

        // R
        void Restart()
        {
            // La escala de tiempo sobrevive a la carga: si se reinicia desde una pausa, la escena empezaría congelada.
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        // Derrota en la greybox: se congela el reloj y el overlay muestra el aviso.
        void OnPlayerDied()
        {
            Defeated = true;
            if (clock != null) clock.Paused = true;
            Debug.Log("[DebugCheats] Derrota · R para reiniciar");
        }
    }
}
