using Cardstorm.Core;
using Cardstorm.Player;
using Cardstorm.Spawning;
using UnityEngine;

namespace Cardstorm.Pickups
{
    // Coloca el cofre de cada oleada a su hora cerca del jugador, dentro del mapa y fuera de obstáculos.
    // Mira el reloj en cada frame, así que un salto de tiempo (F3) crea los cofres que se hayan pasado.
    public class ChestSpawner : MonoBehaviour
    {
        [SerializeField] WaveSpawner spawner;
        [SerializeField] GameClock clock;
        [SerializeField] PickupManager pickups;
        [SerializeField] MapBounds map;
        [SerializeField] Transform player;

        [Tooltip("Distancia al jugador a la que aparece el cofre.")]
        [SerializeField, Min(0f)] float distance = 4f;
        [Tooltip("Radio que debe quedar libre de obstáculos alrededor del cofre.")]
        [SerializeField, Min(0.1f)] float clearance = 0.7f;

        const int Attempts = 8;

        bool[] spawned;
        SpawnPositionPicker picker;

        void Awake()
        {
            if (spawner == null) spawner = FindFirstObjectByType<WaveSpawner>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (pickups == null) pickups = FindFirstObjectByType<PickupManager>();
            if (map == null) map = FindFirstObjectByType<MapBounds>();
        }

        void Start()
        {
            if (player == null)
            {
                var health = FindFirstObjectByType<PlayerHealth>();
                if (health != null) player = health.transform;
            }
            spawned = new bool[spawner.Curve.waves.Count];
            picker = new SpawnPositionPicker(map.Rect, Layers.ObstacleMask);
        }

        void Update()
        {
            if (player == null) return;

            var waves = spawner.Curve.waves;
            double now = clock.Elapsed;
            for (int i = 0; i < spawned.Length; i++)
            {
                if (spawned[i]) continue;

                var wave = waves[i];
                if (wave.chestGold <= 0)
                {
                    spawned[i] = true;
                    continue;
                }
                if (now < wave.chestTime) continue;

                spawned[i] = true;
                pickups.SpawnChest(PickPosition(), wave.chestGold);
            }
        }

        Vector2 PickPosition()
        {
            Vector2 center = player.position;
            for (int i = 0; i < Attempts; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (picker.IsFree(point, clearance)) return point;
            }
            return picker.Resolve(center + Vector2.right * distance, clearance, center);
        }
    }
}
