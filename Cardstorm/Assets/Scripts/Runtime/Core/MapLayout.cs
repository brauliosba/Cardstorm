using System.Collections.Generic;
using UnityEngine;

namespace Cardstorm.Core
{
    // Distribución de obstáculos con semilla fija: el mapa es siempre el mismo y las pruebas son comparables.
    public static class MapLayout
    {
        public enum ObstacleKind { Rock, Column }

        public struct Obstacle
        {
            public ObstacleKind Kind;
            public Vector2 Position;
            // Rock: radio. Column: lado.
            public float Size;

            public readonly float BoundingRadius => Kind == ObstacleKind.Rock ? Size : Size * 0.70710678f;
        }

        public struct Settings
        {
            public Rect Bounds;
            public int Seed;
            public int Count;
            public float CenterClearance;   // Zona libre de inicio y del jefe final
            public float MinGap;            // Separación mínima entre bordes, para que haya pasillos
            public float RockMinRadius;
            public float RockMaxRadius;
            public float ColumnSize;
            public float ColumnChance;
            public int MaxAttempts;
        }

        public static Settings DefaultSettings(Rect bounds) => new()
        {
            Bounds = bounds,
            Seed = 2026,
            Count = 24,
            CenterClearance = 8f,
            MinGap = 6f,
            RockMinRadius = 1.5f,
            RockMaxRadius = 3f,
            ColumnSize = 2f,
            ColumnChance = 0.4f,
            MaxAttempts = 5000,
        };

        public static List<Obstacle> GenerateObstacles(Settings s)
        {
            var random = new System.Random(s.Seed);
            var result = new List<Obstacle>(s.Count);

            for (int attempt = 0; attempt < s.MaxAttempts && result.Count < s.Count; attempt++)
            {
                var candidate = new Obstacle();
                if (random.NextDouble() < s.ColumnChance)
                {
                    candidate.Kind = ObstacleKind.Column;
                    candidate.Size = s.ColumnSize;
                }
                else
                {
                    candidate.Kind = ObstacleKind.Rock;
                    candidate.Size = Mathf.Lerp(s.RockMinRadius, s.RockMaxRadius, (float)random.NextDouble());
                }

                float r = candidate.BoundingRadius;
                candidate.Position = new Vector2(
                    Mathf.Lerp(s.Bounds.xMin + r, s.Bounds.xMax - r, (float)random.NextDouble()),
                    Mathf.Lerp(s.Bounds.yMin + r, s.Bounds.yMax - r, (float)random.NextDouble()));

                if (candidate.Position.magnitude - r < s.CenterClearance)
                    continue;

                bool fits = true;
                foreach (var other in result)
                {
                    if (Vector2.Distance(candidate.Position, other.Position) - r - other.BoundingRadius < s.MinGap)
                    {
                        fits = false;
                        break;
                    }
                }
                if (fits)
                    result.Add(candidate);
            }

            if (result.Count < s.Count)
                Debug.LogWarning($"[MapLayout] Solo caben {result.Count} de {s.Count} obstáculos con estas reglas.");
            return result;
        }

        // Celdas de la rejilla de 1 unidad que ocupa el obstáculo; la celda (x, y) cubre [x, x + 1] × [y, y + 1].
        // El tamaño se redondea a celdas enteras: la roca es un disco con las esquinas recortadas y la columna, un cuadrado.
        public static void GetCells(Obstacle o, List<Vector2Int> cells)
        {
            cells.Clear();
            int n = Mathf.Max(2, Mathf.RoundToInt(o.Kind == ObstacleKind.Rock ? o.Size * 2f : o.Size));

            // Con n par el centro cae en una esquina de celda; con n impar, en el centro de una celda.
            Vector2 center = n % 2 == 0
                ? new Vector2(Mathf.Round(o.Position.x), Mathf.Round(o.Position.y))
                : new Vector2(Mathf.Floor(o.Position.x) + 0.5f, Mathf.Floor(o.Position.y) + 0.5f);
            int originX = Mathf.RoundToInt(center.x - n * 0.5f);
            int originY = Mathf.RoundToInt(center.y - n * 0.5f);
            float limit = n * 0.5f;

            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                var cell = new Vector2Int(originX + i, originY + j);
                if (o.Kind == ObstacleKind.Rock && Vector2.Distance(cell + new Vector2(0.5f, 0.5f), center) > limit)
                    continue;
                cells.Add(cell);
            }
        }
    }
}
