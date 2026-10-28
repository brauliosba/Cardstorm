using System.Collections.Generic;
using UnityEngine;

namespace Cardstorm.Spawning
{
    // Añade a FormationMath las comprobaciones de mapa y obstáculos, y el azar de las formaciones.
    public sealed class SpawnPositionPicker
    {
        const int JitterRetries = 5;
        const float MarchStep = 0.5f;

        static readonly ViewSide[] AllSides = { ViewSide.Top, ViewSide.Bottom, ViewSide.Left, ViewSide.Right };

        readonly Rect bounds;
        readonly int obstacleMask;
        readonly ViewSide[] sideOrder = (ViewSide[])AllSides.Clone();

        public Rect Bounds => bounds;

        public SpawnPositionPicker(Rect bounds, int obstacleMask)
        {
            this.bounds = bounds;
            this.obstacleMask = obstacleMask;
        }

        public bool IsFree(Vector2 point, float radius) =>
            FormationMath.IsInside(bounds, point, radius) &&
            Physics2D.OverlapCircle(point, radius, obstacleMask) == null;

        // Punto de entrada de un grupo de los tramos: un lado al azar de la vista, fuera de pantalla.
        // Si cae fuera del mapa (el jugador está junto a un muro), prueba otro lado.
        public Vector2 PickEdgeAnchor(Rect view, float margin, float clearance)
        {
            Shuffle(sideOrder);
            foreach (var side in sideOrder)
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Vector2 point = FormationMath.EdgePoint(view, side, Random.value, margin);
                    if (FormationMath.IsInside(bounds, point, clearance))
                        return point;
                }
            }
            return FormationMath.MoveInside(bounds, FormationMath.EdgePoint(view, sideOrder[0], Random.value, margin), clearance);
        }

        // Lado al azar cuyo punto central queda dentro del mapa (para pinza, estampida y tormenta).
        public ViewSide PickSide(Rect view, float margin)
        {
            Shuffle(sideOrder);
            foreach (var side in sideOrder)
            {
                if (FormationMath.IsInside(bounds, FormationMath.EdgePoint(view, side, 0.5f, margin), 0f))
                    return side;
            }
            return sideOrder[0];
        }

        // Lados distintos al azar, para que cada tipo de la Tormenta entre por uno.
        public void ShuffledSides(List<ViewSide> results)
        {
            Shuffle(sideOrder);
            results.Clear();
            results.AddRange(sideOrder);
        }

        // Ajusta un punto para que esté dentro del mapa y fuera de obstáculos:
        // 1. Avanza desde el punto hacia towards (en el Cerco, sobre el mismo ángulo hacia el jugador).
        // 2. Si no hay sitio, reintenta alrededor.
        // 3. Si tampoco, lo deja en el punto válido más cercano que conozca (dentro del mapa).
        public Vector2 Resolve(Vector2 desired, float radius, Vector2 towards)
        {
            if (IsFree(desired, radius))
                return desired;

            Vector2 toTarget = towards - desired;
            float distance = toTarget.magnitude;
            if (distance > 1e-4f)
            {
                Vector2 dir = toTarget / distance;
                for (float d = MarchStep; d < distance; d += MarchStep)
                {
                    Vector2 p = desired + dir * d;
                    if (IsFree(p, radius))
                        return p;
                }
            }

            Vector2 inside = FormationMath.MoveInside(bounds, desired, radius);
            for (int i = 0; i < JitterRetries; i++)
            {
                Vector2 p = FormationMath.MoveInside(bounds, inside + Random.insideUnitCircle * (1f + i), radius);
                if (IsFree(p, radius))
                    return p;
            }
            return inside;
        }

        public void ResolveAll(List<Vector2> points, float radius, Vector2 towards)
        {
            for (int i = 0; i < points.Count; i++)
                points[i] = Resolve(points[i], radius, towards);
        }

        static void Shuffle(ViewSide[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
    }
}
