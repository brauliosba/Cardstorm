using System;
using System.Collections.Generic;
using Cardstorm.CameraRig;
using Cardstorm.Core;
using Cardstorm.Enemies;
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
        }

        [SerializeField] WaveSpawner spawner;
        [SerializeField] EnemyManager enemies;
        [SerializeField] RunManager run;
        [SerializeField] GameClock clock;
        [SerializeField] MapBounds map;

        [SerializeField] List<BossEntry> bosses = new();

        [Tooltip("Distancia fuera de la vista a la que aparece, además de su radio.")]
        [SerializeField, Min(0f)] float spawnMargin = 2f;

        readonly List<Enemy> alive = new(2);
        readonly List<BossEntry> aliveEntries = new(2);
        SpawnPositionPicker picker;
        Camera cam;

        // El último jefe que sigue vivo, o null.
        public Enemy Current => alive.Count > 0 ? alive[^1] : null;
        public string CurrentName => aliveEntries.Count > 0 ? DisplayName(aliveEntries[^1]) : null;

        double Now => clock != null ? clock.Elapsed : Time.timeSinceLevelLoad;

        void Awake()
        {
            if (spawner == null) spawner = FindFirstObjectByType<WaveSpawner>();
            if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
            if (run == null) run = FindFirstObjectByType<RunManager>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (map == null) map = FindFirstObjectByType<MapBounds>();
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
            RunEvents.RaiseBossSpawned(Now, definition.id);
        }

        void OnEnemyKilled(Enemy enemy, Vector2 position)
        {
            int index = alive.IndexOf(enemy);
            if (index < 0) return;

            var entry = aliveEntries[index];
            RemoveAt(index);
            RunEvents.RaiseBossKilled(Now, entry.definition.id);
            if (entry.isFinal && run != null) run.Win();
        }

        // Con F10 también se retira a los jefes.
        void OnEnemyDespawned(Enemy enemy)
        {
            int index = alive.IndexOf(enemy);
            if (index >= 0) RemoveAt(index);
        }

        void RemoveAt(int index)
        {
            alive.RemoveAt(index);
            aliveEntries.RemoveAt(index);
        }

        BossEntry Find(string bossName)
        {
            foreach (var entry in bosses)
                if (string.Equals(entry.name, bossName, StringComparison.OrdinalIgnoreCase))
                    return entry;
            return null;
        }

        static string DisplayName(BossEntry entry) =>
            entry.definition != null && !string.IsNullOrEmpty(entry.definition.displayName) ? entry.definition.displayName : entry.name;
    }
}
