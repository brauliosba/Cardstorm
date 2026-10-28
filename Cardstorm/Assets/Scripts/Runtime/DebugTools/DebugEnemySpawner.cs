#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Cardstorm.CameraRig;
using Cardstorm.Core;
using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.DebugTools
{
    // TEMPORAL: spawner mínimo para probar enemigos y pool hasta que exista el WaveSpawner.
    // Saca un enemigo cada cierto tiempo en un borde de la vista, fuera de pantalla.
    public class DebugEnemySpawner : MonoBehaviour
    {
        [SerializeField] EnemyManager manager;
        [SerializeField] EnemyDefinition definition;
        [SerializeField] MapBounds map;
        [SerializeField, Min(0.05f)] float interval = 1f;
        [SerializeField, Min(0f)] float spawnMargin = 2f;
        [SerializeField, Min(1)] int maxAlive = 150;

        Camera cam;
        float timer;

        void Start() => cam = Camera.main;

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < interval) return;
            timer -= interval;

            if (manager.AliveCount >= maxAlive) return;
            if (TryPickPosition(out Vector2 position))
                manager.Spawn(definition, position);
        }

        bool TryPickPosition(out Vector2 position)
        {
            Rect view = CameraMath.ViewRect(cam.transform.position, cam.orthographicSize, cam.aspect);
            Rect bounds = map.Rect;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float t = Random.value;
                position = Random.Range(0, 4) switch
                {
                    0 => new Vector2(Mathf.Lerp(view.xMin, view.xMax, t), view.yMax + spawnMargin),
                    1 => new Vector2(Mathf.Lerp(view.xMin, view.xMax, t), view.yMin - spawnMargin),
                    2 => new Vector2(view.xMin - spawnMargin, Mathf.Lerp(view.yMin, view.yMax, t)),
                    _ => new Vector2(view.xMax + spawnMargin, Mathf.Lerp(view.yMin, view.yMax, t)),
                };
                if (!bounds.Contains(position)) continue;
                if (Physics2D.OverlapCircle(position, definition.radius, Layers.ObstacleMask) != null) continue;
                return true;
            }
            position = default;
            return false;
        }
    }
}
#endif
