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
    }
}
