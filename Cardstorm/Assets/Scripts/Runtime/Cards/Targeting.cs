using System.Collections.Generic;
using UnityEngine;

namespace Cardstorm.Cards
{
    // Selección de objetivo de las cartas. Trabaja con listas de posiciones para no depender de la escena.
    // Todas devuelven -1 si no hay ningún punto en rango.
    public static class Targeting
    {
        public static int Nearest(Vector2 origin, IReadOnlyList<Vector2> points, float maxRange)
        {
            int best = -1;
            float bestSqr = maxRange * maxRange;
            for (int i = 0; i < points.Count; i++)
            {
                float d = (points[i] - origin).sqrMagnitude;
                if (d <= bestSqr && (best < 0 || d < bestSqr))
                {
                    best = i;
                    bestSqr = d;
                }
            }
            return best;
        }

        // Empate de vida: gana el más cercano.
        public static int HighestHealth(Vector2 origin, IReadOnlyList<Vector2> points, IReadOnlyList<float> health, float maxRange)
        {
            int best = -1;
            float rangeSqr = maxRange * maxRange;
            float bestHealth = float.MinValue, bestSqr = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float d = (points[i] - origin).sqrMagnitude;
                if (d > rangeSqr) continue;
                if (health[i] > bestHealth || (Mathf.Approximately(health[i], bestHealth) && d < bestSqr))
                {
                    best = i;
                    bestHealth = health[i];
                    bestSqr = d;
                }
            }
            return best;
        }

        // El punto en rango con más vecinos a menos de areaRadius; empate: el más cercano.
        // Coste O(n²): solo se llama al jugar una carta, no cada frame.
        public static int Densest(Vector2 origin, IReadOnlyList<Vector2> points, float maxRange, float areaRadius)
        {
            int best = -1, bestCount = -1;
            float rangeSqr = maxRange * maxRange, areaSqr = areaRadius * areaRadius, bestSqr = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float d = (points[i] - origin).sqrMagnitude;
                if (d > rangeSqr) continue;
                int count = 0;
                for (int j = 0; j < points.Count; j++)
                    if ((points[j] - points[i]).sqrMagnitude <= areaSqr) count++;
                if (count > bestCount || (count == bestCount && d < bestSqr))
                {
                    best = i;
                    bestCount = count;
                    bestSqr = d;
                }
            }
            return best;
        }

        // Saltos de una cadena: desde start, cada vez al punto sin alcanzar más cercano al último.
        // Deja en result los índices en orden, empezando por start. Vacío si start no es válido.
        public static void Chain(int start, IReadOnlyList<Vector2> points, float jumpRange, int maxTargets, List<int> result)
        {
            result.Clear();
            if (start < 0 || start >= points.Count || maxTargets < 1) return;

            result.Add(start);
            float rangeSqr = jumpRange * jumpRange;
            while (result.Count < maxTargets)
            {
                Vector2 from = points[result[result.Count - 1]];
                int best = -1;
                float bestSqr = rangeSqr;
                for (int i = 0; i < points.Count; i++)
                {
                    if (result.Contains(i)) continue;
                    float d = (points[i] - from).sqrMagnitude;
                    if (d <= bestSqr && (best < 0 || d < bestSqr))
                    {
                        best = i;
                        bestSqr = d;
                    }
                }
                if (best < 0) return;
                result.Add(best);
            }
        }

        public static bool InArc(Vector2 origin, Vector2 aim, Vector2 point, float arcDegrees)
        {
            Vector2 to = point - origin;
            if (to.sqrMagnitude < 1e-6f) return true;
            return Vector2.Angle(aim, to) <= arcDegrees * 0.5f;
        }
    }
}
