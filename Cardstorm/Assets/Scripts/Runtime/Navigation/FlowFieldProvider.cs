using Cardstorm.Core;
using UnityEngine;

namespace Cardstorm.Navigation
{
    // Mantiene el campo de flujo hacia el jugador: bake de los obstáculos al empezar y lo recalcula al cambiar de celda.
    public class FlowFieldProvider : MonoBehaviour
    {
        [SerializeField] MapBounds map;
        [SerializeField] Transform target;

        [Tooltip("Tamaño de celda en unidades.")]
        [SerializeField, Min(0.25f)] float cellSize = 1f;

        [Tooltip("Margen alrededor de los obstáculos: las celdas a menos de esta distancia se consideran bloqueadas.")]
        [SerializeField, Min(0f)] float obstacleInflation = 0.3f;

        [Tooltip("Distancia máxima calculada desde el jugador. Más allá, los enemigos van en línea recta.")]
        [SerializeField, Min(1f)] float maxDistance = 40f;

        [Tooltip("Tiempo mínimo entre recálculos.")]
        [SerializeField, Min(0f)] float minRebuildInterval = 0.2f;

        [Header("Depuración")]
        [SerializeField] bool drawGizmos;
        [SerializeField, Min(1)] int gizmoRadiusCells = 20;

        FlowField field;
        int lastCellX = int.MinValue, lastCellY = int.MinValue;
        float lastBuildTime = float.NegativeInfinity;

        public FlowField Field => field;
        public Transform Target => target;

        void Awake()
        {
            Rect rect = map.Rect;
            int width = Mathf.CeilToInt(rect.width / cellSize);
            int height = Mathf.CeilToInt(rect.height / cellSize);
            field = new FlowField(rect.min, width, height, cellSize) { MaxDistance = maxDistance };
            BakeObstacles();
        }

        void Start() => Rebuild();

        void Update()
        {
            field.WorldToCell(target.position, out int x, out int y);
            if ((x != lastCellX || y != lastCellY) && Time.time - lastBuildTime >= minRebuildInterval)
                Rebuild();
        }

        public Vector2 GetDirection(Vector2 position) => field.GetDirection(position, target.position);

        public void Rebuild()
        {
            field.WorldToCell(target.position, out lastCellX, out lastCellY);
            field.Build(target.position);
            lastBuildTime = Time.time;
        }

        // Marca como bloqueadas las celdas que tocan un collider de la capa Obstacle. Los obstáculos son estáticos.
        public void BakeObstacles()
        {
            Physics2D.SyncTransforms();
            var size = Vector2.one * (cellSize + obstacleInflation * 2f);
            for (int y = 0; y < field.Height; y++)
            for (int x = 0; x < field.Width; x++)
            {
                bool hit = Physics2D.OverlapBox(field.CellCenter(x, y), size, 0f, Layers.ObstacleMask) != null;
                field.SetBlocked(x, y, hit);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos || field == null || !field.IsBuilt || target == null) return;

            field.WorldToCell(target.position, out int tx, out int ty);
            for (int y = ty - gizmoRadiusCells; y <= ty + gizmoRadiusCells; y++)
            for (int x = tx - gizmoRadiusCells; x <= tx + gizmoRadiusCells; x++)
            {
                if (!field.InGrid(x, y)) continue;
                Vector2 center = field.CellCenter(x, y);
                if (field.IsBlocked(x, y))
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
                    Gizmos.DrawCube(center, Vector3.one * cellSize * 0.9f);
                    continue;
                }
                if (!field.IsReachable(x, y)) continue;

                Vector2 dir = field.GetDirection(center, target.position);
                Gizmos.color = field.HasLineOfSight(x, y) ? new Color(0.3f, 1f, 0.3f, 0.5f) : new Color(1f, 0.8f, 0.2f, 0.9f);
                Gizmos.DrawLine(center, center + 0.45f * cellSize * dir);
            }
        }
    }
}
