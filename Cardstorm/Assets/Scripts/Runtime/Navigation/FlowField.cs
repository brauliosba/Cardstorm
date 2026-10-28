using UnityEngine;

namespace Cardstorm.Navigation
{
    // Campo de flujo hacia un único objetivo: un solo cálculo sirve para todos los enemigos.
    // 1. Distancias con Fast Marching (eikonal): casi euclídeas, así que desde lejos se apunta a la esquina del obstáculo
    //    en lugar de ir recto hasta él y luego girar.
    // 2. Línea de visión: las celdas que ven el objetivo van directas a su posición real.
    // 3. En el resto, dirección de máxima bajada de la distancia.
    public sealed class FlowField
    {
        const float Unreachable = float.MaxValue;

        static readonly int[] OffsetX = { 1, -1, 0, 0 };
        static readonly int[] OffsetY = { 0, 0, 1, -1 };

        readonly bool[] blocked;
        readonly bool[] accepted;
        readonly float[] cost;
        readonly Vector2[] direction;
        readonly bool[] lineOfSight;

        // Montículo binario con inserción perezosa (sin decrease-key).
        readonly int[] heapCell;
        readonly float[] heapKey;
        int heapCount;

        int targetX, targetY;

        public Vector2 Origin { get; }
        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }
        public bool IsBuilt { get; private set; }

        // Distancia máxima que se calcula desde el objetivo. Más allá, los agentes van en línea recta.
        public float MaxDistance { get; set; } = float.PositiveInfinity;

        public FlowField(Vector2 origin, int width, int height, float cellSize)
        {
            Origin = origin;
            Width = width;
            Height = height;
            CellSize = cellSize;

            int cells = width * height;
            blocked = new bool[cells];
            accepted = new bool[cells];
            cost = new float[cells];
            direction = new Vector2[cells];
            lineOfSight = new bool[cells];
            heapCell = new int[cells * 4 + 1];
            heapKey = new float[cells * 4 + 1];
        }

        public bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool WorldToCell(Vector2 position, out int x, out int y)
        {
            Vector2 local = (position - Origin) / CellSize;
            x = Mathf.FloorToInt(local.x);
            y = Mathf.FloorToInt(local.y);
            return InGrid(x, y);
        }

        public Vector2 CellCenter(int x, int y) => Origin + new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);

        public void SetBlocked(int x, int y, bool value)
        {
            if (InGrid(x, y)) blocked[y * Width + x] = value;
        }

        public bool IsBlocked(int x, int y) => InGrid(x, y) && blocked[y * Width + x];

        public bool IsReachable(int x, int y) => InGrid(x, y) && cost[y * Width + x] < Unreachable;

        public bool HasLineOfSight(int x, int y) => InGrid(x, y) && lineOfSight[y * Width + x];

        public float GetCost(int x, int y) => InGrid(x, y) ? cost[y * Width + x] : Unreachable;

        public Vector2 GetCellDirection(int x, int y) => InGrid(x, y) ? direction[y * Width + x] : Vector2.zero;

        // Recalcula distancias, línea de visión y direcciones hacia target.
        public void Build(Vector2 target)
        {
            WorldToCell(target, out targetX, out targetY);
            targetX = Mathf.Clamp(targetX, 0, Width - 1);
            targetY = Mathf.Clamp(targetY, 0, Height - 1);

            ComputeDistances();
            ComputeLineOfSight();
            ComputeDirections();
            IsBuilt = true;
        }

        // Dirección normalizada para un agente en position. targetPosition es la posición real del objetivo,
        // que se usa con línea de visión, fuera de la rejilla o si la celda es inalcanzable.
        public Vector2 GetDirection(Vector2 position, Vector2 targetPosition)
        {
            Vector2 direct = DirectTo(position, targetPosition);
            if (!IsBuilt || !WorldToCell(position, out int x, out int y))
                return direct;

            int cell = y * Width + x;
            if (cost[cell] >= Unreachable || lineOfSight[cell])
                return direct;

            Vector2 flow = direction[cell];
            return flow.sqrMagnitude > 0f ? flow : direct;
        }

        // ---------------------------------------------------------------------
        // Fast Marching
        // ---------------------------------------------------------------------

        void ComputeDistances()
        {
            for (int i = 0; i < cost.Length; i++)
            {
                cost[i] = Unreachable;
                accepted[i] = false;
            }

            // La celda del objetivo se siembra aunque esté bloqueada (el jugador pegado a una roca).
            int start = targetY * Width + targetX;
            cost[start] = 0f;
            heapCount = 0;
            Push(start, 0f);

            while (heapCount > 0)
            {
                Pop(out int cell, out float key);
                if (accepted[cell] || key > cost[cell]) continue;  // Entrada obsoleta
                if (key > MaxDistance) break;
                accepted[cell] = true;

                int cx = cell % Width, cy = cell / Width;
                for (int n = 0; n < 4; n++)
                {
                    int nx = cx + OffsetX[n], ny = cy + OffsetY[n];
                    if (!InGrid(nx, ny)) continue;
                    int neighbour = ny * Width + nx;
                    if (blocked[neighbour] || accepted[neighbour]) continue;

                    float t = SolveEikonal(nx, ny);
                    if (t < cost[neighbour] && t <= MaxDistance + CellSize)
                    {
                        cost[neighbour] = t;
                        Push(neighbour, t);
                    }
                }
            }
        }

        // Actualización de primer orden con los vecinos ya aceptados.
        float SolveEikonal(int x, int y)
        {
            float a = Mathf.Min(AcceptedCost(x - 1, y), AcceptedCost(x + 1, y));
            float b = Mathf.Min(AcceptedCost(x, y - 1), AcceptedCost(x, y + 1));
            if (a > b) (a, b) = (b, a);

            float h = CellSize;
            if (b >= Unreachable || b - a >= h)
                return a + h;
            float diff = b - a;
            return (a + b + Mathf.Sqrt(2f * h * h - diff * diff)) * 0.5f;
        }

        float AcceptedCost(int x, int y)
        {
            if (!InGrid(x, y)) return Unreachable;
            int i = y * Width + x;
            return accepted[i] ? cost[i] : Unreachable;
        }

        // ---------------------------------------------------------------------
        // Línea de visión: se propaga desde el objetivo hacia fuera, anillo a anillo.
        // Una celda ve el objetivo si no está bloqueada y la celda siguiente hacia él también lo ve.
        // ---------------------------------------------------------------------

        void ComputeLineOfSight()
        {
            int maxRing = Mathf.Max(Mathf.Max(targetX, Width - 1 - targetX), Mathf.Max(targetY, Height - 1 - targetY));
            for (int r = 0; r <= maxRing; r++)
            {
                for (int x = targetX - r; x <= targetX + r; x++)
                {
                    UpdateLineOfSight(x, targetY - r);
                    if (r > 0) UpdateLineOfSight(x, targetY + r);
                }
                for (int y = targetY - r + 1; y <= targetY + r - 1; y++)
                {
                    UpdateLineOfSight(targetX - r, y);
                    UpdateLineOfSight(targetX + r, y);
                }
            }
        }

        void UpdateLineOfSight(int x, int y)
        {
            if (!InGrid(x, y)) return;
            int cell = y * Width + x;
            int dx = x - targetX, dy = y - targetY;

            if (dx == 0 && dy == 0)
            {
                lineOfSight[cell] = true;
                return;
            }
            if (blocked[cell] || cost[cell] >= Unreachable)
            {
                lineOfSight[cell] = false;
                return;
            }

            int sx = -System.Math.Sign(dx), sy = -System.Math.Sign(dy);
            int adx = System.Math.Abs(dx), ady = System.Math.Abs(dy);
            bool visible;
            if (adx >= 2 * ady)
                visible = Los(x + sx, y);
            else if (ady >= 2 * adx)
                visible = Los(x, y + sy);
            else
                visible = Los(x + sx, y + sy) && !IsBlocked(x + sx, y) && !IsBlocked(x, y + sy);
            lineOfSight[cell] = visible;
        }

        bool Los(int x, int y) => InGrid(x, y) && lineOfSight[y * Width + x];

        // ---------------------------------------------------------------------
        // Direcciones: máxima bajada de la distancia en cada eje.
        // ---------------------------------------------------------------------

        void ComputeDirections()
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int cell = y * Width + x;
                float c = cost[cell];
                if (c >= Unreachable)
                {
                    direction[cell] = Vector2.zero;
                    continue;
                }

                // En empate (punto de silla, justo detrás del centro de un muro) se elige siempre el lado negativo.
                float left = GetCost(x - 1, y), right = GetCost(x + 1, y);
                float down = GetCost(x, y - 1), up = GetCost(x, y + 1);
                float minX = Mathf.Min(left, right), minY = Mathf.Min(down, up);
                float vx = minX < c ? (left <= right ? -(c - left) : c - right) : 0f;
                float vy = minY < c ? (down <= up ? -(c - down) : c - up) : 0f;

                var v = new Vector2(vx, vy);
                direction[cell] = v.sqrMagnitude > 0f ? v.normalized : Vector2.zero;
            }
        }

        static Vector2 DirectTo(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float m = d.magnitude;
            return m > 1e-5f ? d / m : Vector2.zero;
        }

        // ---------------------------------------------------------------------
        // Montículo
        // ---------------------------------------------------------------------

        void Push(int cell, float key)
        {
            int i = heapCount++;
            heapCell[i] = cell;
            heapKey[i] = key;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (heapKey[parent] <= heapKey[i]) break;
                Swap(i, parent);
                i = parent;
            }
        }

        void Pop(out int cell, out float key)
        {
            cell = heapCell[0];
            key = heapKey[0];
            heapCount--;
            heapCell[0] = heapCell[heapCount];
            heapKey[0] = heapKey[heapCount];

            int i = 0;
            while (true)
            {
                int left = 2 * i + 1, right = left + 1, smallest = i;
                if (left < heapCount && heapKey[left] < heapKey[smallest]) smallest = left;
                if (right < heapCount && heapKey[right] < heapKey[smallest]) smallest = right;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
        }

        void Swap(int a, int b)
        {
            (heapCell[a], heapCell[b]) = (heapCell[b], heapCell[a]);
            (heapKey[a], heapKey[b]) = (heapKey[b], heapKey[a]);
        }
    }
}
