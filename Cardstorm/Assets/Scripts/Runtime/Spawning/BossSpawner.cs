using System;
using System.Collections.Generic;
using Cardstorm.CameraRig;
using Cardstorm.Core;
using Cardstorm.Enemies;
using Cardstorm.Localization;
using Cardstorm.Player;
using Cardstorm.Progression;
using UnityEngine;

namespace Cardstorm.Spawning
{
    // Hace aparecer a los jefes a su hora y declara la victoria al morir el jefe final.
    // Las oleadas normales siguen mientras tanto.
    public class BossSpawner : MonoBehaviour
    {
        [Serializable]
        public class BossEntry
        {
            [Tooltip("Igual que el nombre del jefe en la curva de apariciones.")]
            public string name;
            public EnemyDefinition definition;
            [Tooltip("Matarlo gana la partida.")]
            public bool isFinal;
            [Tooltip("Segundos para derrotarlo; después huye sin dar nada. 0 = sin límite.")]
            [Min(0f)] public float timeLimit;
            [Tooltip("Al derrotarlo se elige una carta rara.")]
            public bool rareReward;

            public string DisplayName
            {
                get
                {
                    string shown = L10n.EnemyName(definition);
                    return !string.IsNullOrEmpty(shown) ? shown : name;
                }
            }
        }

        [SerializeField] WaveSpawner spawner;
        [SerializeField] EnemyManager enemies;
        [SerializeField] RunManager run;
        [SerializeField] GameClock clock;
        [SerializeField] MapBounds map;
        [Tooltip("Si se deja vacío, se busca en la escena.")]
        [SerializeField] PlayerHealth playerHealth;

        [SerializeField] List<BossEntry> bosses = new();

        [Tooltip("Distancia fuera de la vista a la que aparece, además de su radio.")]
        [SerializeField, Min(0f)] float spawnMargin = 2f;

        [Header("Retirada")]
        [Tooltip("Segundos que tarda en desvanecerse al agotarse su tiempo.")]
        [SerializeField, Min(0.1f)] float retreatDuration = 1.5f;
        [Tooltip("Unidades/s a las que se aleja del jugador.")]
        [SerializeField, Min(0f)] float retreatSpeed = 6f;

        readonly List<Enemy> alive = new(2);
        readonly List<BossEntry> aliveEntries = new(2);
        readonly List<double> spawnTimes = new(2);
        // Jefes muertos cuya animación de muerte aún no ha acabado, con el momento en que acaba.
        readonly List<(BossEntry entry, double at)> defeated = new(2);
        SpawnPositionPicker picker;
        Camera cam;

        // El último jefe que sigue vivo, o null.
        public Enemy Current => alive.Count > 0 ? alive[^1] : null;
        public string CurrentName => aliveEntries.Count > 0 ? aliveEntries[^1].DisplayName : null;

        // Segundos que le quedan al jefe actual, o -1 si no tiene límite.
        public float CurrentTimeLeft => alive.Count > 0 ? TimeLeft(alive.Count - 1) : -1f;

        // Al acabar la animación de muerte de un jefe, después de la victoria si era el final.
        public event Action<BossEntry> BossDefeated;

        double Now => clock != null ? clock.Elapsed : Time.timeSinceLevelLoad;

        void Awake()
        {
            if (spawner == null) spawner = FindFirstObjectByType<WaveSpawner>();
            if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
            if (run == null) run = FindFirstObjectByType<RunManager>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (map == null) map = FindFirstObjectByType<MapBounds>();
            if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        void Start()
        {
            cam = Camera.main;
            if (map != null) picker = new SpawnPositionPicker(map.Rect, Layers.ObstacleMask);
            if (spawner == null) return;

            foreach (var boss in spawner.Curve.bosses)
            {
                var entry = Find(boss.name);
                if (entry == null || entry.definition == null)
                {
                    Debug.LogWarning($"[BossSpawner] El jefe «{boss.name}» de la curva no tiene EnemyDefinition.", this);
                    continue;
                }
                if (!Mathf.Approximately(boss.health, entry.definition.maxHealth))
                    Debug.LogWarning($"[BossSpawner] {boss.name}: la curva dice {boss.health} de vida y la definición {entry.definition.maxHealth}. Manda la definición.", entry.definition);
                if (entry.definition.deathVfx == null)
                    Debug.LogWarning($"[BossSpawner] {entry.definition.name} no tiene deathVfx: su recompensa y la victoria saldrán sin esperar a la animación de muerte.", entry.definition);
                if (!entry.definition.isBoss)
                    Debug.LogWarning($"[BossSpawner] {entry.definition.name} no tiene marcado isBoss: se quedará atascado y se puede reciclar por distancia.", entry.definition);
            }
        }

        void OnEnable()
        {
            if (spawner != null) spawner.BossTimeReached += OnBossTimeReached;
            if (enemies != null)
            {
                enemies.EnemyKilled += OnEnemyKilled;
                enemies.EnemyDespawned += OnEnemyDespawned;
            }
        }

        void OnDisable()
        {
            if (spawner != null) spawner.BossTimeReached -= OnBossTimeReached;
            if (enemies != null)
            {
                enemies.EnemyKilled -= OnEnemyKilled;
                enemies.EnemyDespawned -= OnEnemyDespawned;
            }
        }

        void OnBossTimeReached(BossEvent boss)
        {
            var entry = Find(boss.name);
            if (entry == null || entry.definition == null || enemies == null || (run != null && run.Won)) return;

            var definition = entry.definition;
            Vector2 playerPos = enemies.Player.position;
            Vector2 position = playerPos;
            if (cam != null && picker != null)
            {
                Rect view = CameraMath.ViewRect(cam.transform.position, cam.orthographicSize, cam.aspect);
                position = picker.PickEdgeAnchor(view, spawnMargin + definition.radius, definition.radius);
            }

            // Refuerzo 1: su vida y su experiencia son las de la definición.
            var enemy = enemies.Spawn(definition, position, 1f);
            alive.Add(enemy);
            aliveEntries.Add(entry);
            spawnTimes.Add(Now);
            RunEvents.RaiseBossSpawned(Now, definition.id);
        }

        // El reloj de la partida se para en pausa, así que el límite también.
        void Update()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                var enemy = alive[i];
                if (enemy.IsRetreating || TimeLeft(i) != 0f) continue;
                enemy.BeginRetreat(retreatDuration, retreatSpeed);
            }

            for (int i = 0; i < defeated.Count;)
            {
                if (Now < defeated[i].at) { i++; continue; }
                var entry = defeated[i].entry;
                defeated.RemoveAt(i);
                if (playerHealth != null && playerHealth.IsDead) continue;
                if (entry.isFinal && run != null) run.Win();
                BossDefeated?.Invoke(entry);
            }
        }

        // Depuración: deja «seconds» al límite del jefe actual. Devuelve false si no hay jefe o no tiene límite.
        public bool SetCurrentTimeLeft(float seconds)
        {
            int index = alive.Count - 1;
            if (index < 0 || aliveEntries[index].timeLimit <= 0f || alive[index].IsRetreating) return false;
            spawnTimes[index] = Now - (aliveEntries[index].timeLimit - seconds);
            return true;
        }

        float TimeLeft(int index)
        {
            float limit = aliveEntries[index].timeLimit;
            if (limit <= 0f) return -1f;
            return Mathf.Max(0f, limit - (float)(Now - spawnTimes[index]));
        }

        void OnEnemyKilled(Enemy enemy, Vector2 position)
        {
            int index = alive.IndexOf(enemy);
            if (index < 0) return;

            var entry = aliveEntries[index];
            RemoveAt(index);
            RunEvents.RaiseBossKilled(Now, entry.definition.id);
            defeated.Add((entry, Now + entry.definition.DeathDuration));
        }

        // Al terminar la retirada. Con F10 también se retira a los jefes, pero sin el evento.
        void OnEnemyDespawned(Enemy enemy)
        {
            int index = alive.IndexOf(enemy);
            if (index < 0) return;

            var entry = aliveEntries[index];
            bool retreated = enemy.RetreatFinished;
            RemoveAt(index);
            if (retreated) RunEvents.RaiseBossRetreated(Now, entry.definition.id);
        }

        void RemoveAt(int index)
        {
            alive.RemoveAt(index);
            aliveEntries.RemoveAt(index);
            spawnTimes.RemoveAt(index);
        }

        BossEntry Find(string bossName)
        {
            foreach (var entry in bosses)
                if (string.Equals(entry.name, bossName, StringComparison.OrdinalIgnoreCase))
                    return entry;
            return null;
        }
    }
}
