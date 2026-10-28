using System.Collections.Generic;
using UnityEngine;

namespace Cardstorm.Spawning
{
    public enum ViewSide { Top, Bottom, Left, Right }

    // Cálculo de posiciones de aparición, sin física ni azar: los lados, ángulos y desplazamientos aleatorios
    // los decide quien llama. Los métodos que rellenan una lista la vacían antes.
    public static class FormationMath
    {
        // Ángulo áureo: reparte puntos en un disco sin que se alineen.
        const float GoldenAngle = 2.39996323f;

        public static Vector2 Outward(ViewSide side) => side switch
        {
            ViewSide.Top => Vector2.up,
            ViewSide.Bottom => Vector2.down,
            ViewSide.Left => Vector2.left,
            _ => Vector2.right,
        };

        public static ViewSide Opposite(ViewSide side) => side switch
        {
            ViewSide.Top => ViewSide.Bottom,
            ViewSide.Bottom => ViewSide.Top,
            ViewSide.Left => ViewSide.Right,
            _ => ViewSide.Left,
        };

        // Punto sobre un lado de la vista (t de 0 a 1 a lo largo del lado), desplazado margin hacia fuera.
        public static Vector2 EdgePoint(Rect view, ViewSide side, float t, float margin) => side switch
        {
            ViewSide.Top => new Vector2(Mathf.Lerp(view.xMin, view.xMax, t), view.yMax + margin),
            ViewSide.Bottom => new Vector2(Mathf.Lerp(view.xMin, view.xMax, t), view.yMin - margin),
            ViewSide.Left => new Vector2(view.xMin - margin, Mathf.Lerp(view.yMin, view.yMax, t)),
            _ => new Vector2(view.xMax + margin, Mathf.Lerp(view.yMin, view.yMax, t)),
        };

        // Radio del Cerco: media diagonal de la vista + extra, para que el anillo empiece fuera de pantalla.
        public static float RingRadius(Rect view, float extra = 1f) => view.size.magnitude * 0.5f + extra;

        // Grupo de count enemigos repartido en un disco de radio spread (patrón de los tramos).
        public static void Cluster(Vector2 center, int count, float spread, List<Vector2> results)
        {
            results.Clear();
            if (count == 1)
            {
                results.Add(center);
                return;
            }
            for (int i = 0; i < count; i++)
            {
                float r = spread * Mathf.Sqrt((i + 0.5f) / count);
                float a = i * GoldenAngle;
                results.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        }

        // Cerco: anillo equiespaciado alrededor de center.
        public static void Ring(Vector2 center, int count, float radius, float startAngle, List<Vector2> results)
        {
            results.Clear();
            float step = 2f * Mathf.PI / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                float a = startAngle + i * step;
                results.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        // Línea a lo largo de un lado de la vista, con los puntos equiespaciados.
        public static void EdgeLine(Rect view, ViewSide side, int count, float margin, List<Vector2> results)
        {
            results.Clear();
            AppendEdgeLine(view, side, count, margin, results);
        }

        // Pinza: la mitad en cada uno de dos lados opuestos (izquierda y derecha si horizontal, arriba y abajo si no).
        // Si el número es impar, el primer lado lleva uno más.
        public static void Pincer(Rect view, bool horizontal, int count, float margin, List<Vector2> results)
        {
            results.Clear();
            ViewSide first = horizontal ? ViewSide.Left : ViewSide.Top;
            int firstCount = (count + 1) / 2;
            AppendEdgeLine(view, first, firstCount, margin, results);
            AppendEdgeLine(view, Opposite(first), count - firstCount, margin, results);
        }

        // Estampida: columna densa de width de ancho que sale del centro de un lado hacia fuera.
        // frontOffset separa la primera fila del borde (para dejar delante al coloso).
        public static void Column(Rect view, ViewSide side, int count, int width, float spacing, float margin,
            float frontOffset, List<Vector2> results)
        {
            results.Clear();
            width = Mathf.Max(1, width);
            Vector2 origin = EdgePoint(view, side, 0.5f, margin + frontOffset);
            Vector2 outward = Outward(side);
            Vector2 lateral = new Vector2(-outward.y, outward.x);

            for (int i = 0; i < count; i++)
            {
                int row = i / width;
                int column = i % width;
                int inRow = Mathf.Min(width, count - row * width);
                float lateralOffset = (column - (inRow - 1) * 0.5f) * spacing;
                results.Add(origin + outward * (row * spacing) + lateral * lateralOffset);
            }
        }

        // Mete el punto en bounds dejando radius de margen con el borde.
        public static Vector2 MoveInside(Rect bounds, Vector2 point, float radius)
        {
            float minX = bounds.xMin + radius, maxX = bounds.xMax - radius;
            float minY = bounds.yMin + radius, maxY = bounds.yMax - radius;
            return new Vector2(
                minX <= maxX ? Mathf.Clamp(point.x, minX, maxX) : bounds.center.x,
                minY <= maxY ? Mathf.Clamp(point.y, minY, maxY) : bounds.center.y);
        }

        public static bool IsInside(Rect bounds, Vector2 point, float radius) =>
            point.x >= bounds.xMin + radius && point.x <= bounds.xMax - radius &&
            point.y >= bounds.yMin + radius && point.y <= bounds.yMax - radius;

        static void AppendEdgeLine(Rect view, ViewSide side, int count, float margin, List<Vector2> results)
        {
            for (int i = 0; i < count; i++)
                results.Add(EdgePoint(view, side, (i + 0.5f) / count, margin));
        }
    }
}
