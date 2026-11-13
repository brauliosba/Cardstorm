using System.Collections.Generic;
using Cardstorm.Core;
using UnityEngine;

namespace Cardstorm.Combat
{
    // Destellos en el mundo (áreas, avisos y rayos) con pools fijos y un único Update.
    // Si un pool se llena, el destello no se dibuja: el daño se aplica igual.
    public class FlashManager : MonoBehaviour
    {
        [SerializeField] GameClock clock;
        [SerializeField] Sprite circleSprite;
        [SerializeField] Sprite ringSprite;
        [Tooltip("El mismo material sin luz que el resto de sprites.")]
        [SerializeField] Material spriteMaterial;
        [Tooltip("Por debajo de los enemigos y por encima de los objetos del suelo.")]
        [SerializeField] int sortingOrder = -3;
        [Tooltip("Sprites creados al empezar. No crece: un aviso usa dos.")]
        [SerializeField, Min(2)] int capacity = 16;

        [Header("Área")]
        [SerializeField, Min(0.01f)] float areaDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] float areaAlpha = 0.5f;

        [Header("Aviso")]
        [SerializeField, Range(0f, 1f)] float telegraphRingAlpha = 0.7f;
        [SerializeField, Range(0f, 1f)] float telegraphFillAlpha = 0.3f;

        [Header("Rayo")]
        [Tooltip("Si se deja vacío, se usa el de los sprites.")]
        [SerializeField] Material lineMaterial;
        [Tooltip("Por encima de los enemigos.")]
        [SerializeField] int beamSortingOrder = 3;
        [Tooltip("Segmentos creados al empezar. No crece.")]
        [SerializeField, Min(1)] int beamCapacity = 12;
        [SerializeField, Min(0.01f)] float beamWidth = 0.12f;
        [SerializeField, Min(0.01f)] float beamDuration = 0.15f;

        enum Kind { Area, Telegraph, Beam }

        struct Flash
        {
            public int Id;
            public Kind Kind;
            public SpriteRenderer Main;
            public SpriteRenderer Fill;  // Solo en los avisos: el círculo que crece
            public LineRenderer Line;    // Solo en los rayos
            public Color Color;
            public float Radius;
            public float Timer;
            public float Duration;
        }

        Stack<SpriteRenderer> free;
        Stack<LineRenderer> freeLines;
        List<Flash> active;
        float circleSize = 1f;
        float ringSize = 1f;
        int nextId = 1;

        public int ActiveCount => active.Count;
        public int InstanceCount { get; private set; }

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (circleSprite != null) circleSize = circleSprite.bounds.size.x;
            if (ringSprite != null) ringSize = ringSprite.bounds.size.x;

            free = new Stack<SpriteRenderer>(capacity);
            active = new List<Flash>(capacity);
            for (int i = 0; i < capacity; i++) free.Push(Create());

            freeLines = new Stack<LineRenderer>(beamCapacity);
            active.Capacity = capacity + beamCapacity;
            for (int i = 0; i < beamCapacity; i++) freeLines.Push(CreateLine(i));
        }

        // Círculo que se desvanece.
        public void Area(Vector2 center, float radius, Color color)
        {
            if (free.Count < 1) return;

            var main = Take(circleSprite, circleSize, center, radius, sortingOrder);
            active.Add(new Flash { Id = nextId++, Kind = Kind.Area, Main = main, Color = color, Radius = radius, Duration = areaDuration });
            SetAlpha(main, color, areaAlpha);
        }

        // Anillo del tamaño final con un círculo que crece hasta llenarlo cuando acaba el aviso.
        // Devuelve 0 si no cabe.
        public int Telegraph(Vector2 center, float radius, float duration, Color color)
        {
            if (free.Count < 2 || duration <= 0f) return 0;

            var ring = Take(ringSprite, ringSize, center, radius, sortingOrder + 1);
            var fill = Take(circleSprite, circleSize, center, 0f, sortingOrder);
            int id = nextId++;
            active.Add(new Flash { Id = id, Kind = Kind.Telegraph, Main = ring, Fill = fill, Color = color, Radius = radius, Duration = duration });
            SetAlpha(ring, color, telegraphRingAlpha);
            SetAlpha(fill, color, telegraphFillAlpha);
            return id;
        }

        // Segmento recto que se desvanece.
        public void Beam(Vector2 from, Vector2 to, Color color)
        {
            if (freeLines.Count < 1) return;

            var line = freeLines.Pop();
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            SetAlpha(line, color, 1f);
            line.gameObject.SetActive(true);
            active.Add(new Flash { Id = nextId++, Kind = Kind.Beam, Line = line, Color = color, Duration = beamDuration });
        }

        public void Cancel(int id)
        {
            if (id == 0) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].Id != id) continue;
                Release(i);
                return;
            }
        }

        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                Release(i);
        }

        void Update()
        {
            float dt = clock != null && clock.Paused ? 0f : Time.deltaTime;
            if (dt <= 0f) return;

            // Hacia atrás: Release intercambia con el último y no se salta ninguno.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var f = active[i];
                f.Timer += dt;
                float t = f.Timer / f.Duration;
                if (t >= 1f)
                {
                    Release(i);
                    continue;
                }

                if (f.Kind == Kind.Area)
                    SetAlpha(f.Main, f.Color, areaAlpha * (1f - t));
                else if (f.Kind == Kind.Telegraph)
                    SetRadius(f.Fill, circleSize, f.Radius * t);
                else
                    SetAlpha(f.Line, f.Color, 1f - t);

                active[i] = f;
            }
        }

        SpriteRenderer Take(Sprite sprite, float spriteSize, Vector2 center, float radius, int order)
        {
            var r = free.Pop();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.transform.position = center;
            SetRadius(r, spriteSize, radius);
            r.gameObject.SetActive(true);
            return r;
        }

        // Quita en O(1) intercambiando con el último y devuelve los sprites al pool.
        void Release(int index)
        {
            var f = active[index];
            if (f.Main != null) Return(f.Main);
            if (f.Fill != null) Return(f.Fill);
            if (f.Line != null)
            {
                f.Line.gameObject.SetActive(false);
                freeLines.Push(f.Line);
            }

            int last = active.Count - 1;
            if (index != last) active[index] = active[last];
            active.RemoveAt(last);
        }

        void Return(SpriteRenderer r)
        {
            r.gameObject.SetActive(false);
            free.Push(r);
        }

        static void SetRadius(SpriteRenderer r, float spriteSize, float radius)
        {
            float scale = radius * 2f / spriteSize;
            r.transform.localScale = new Vector3(scale, scale, 1f);
        }

        static void SetAlpha(SpriteRenderer r, Color color, float alpha)
        {
            color.a = alpha;
            r.color = color;
        }

        static void SetAlpha(LineRenderer line, Color color, float alpha)
        {
            color.a = alpha;
            line.startColor = color;
            line.endColor = color;
        }

        SpriteRenderer Create()
        {
            var go = new GameObject($"Flash_{InstanceCount:000}");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            if (spriteMaterial != null) r.sharedMaterial = spriteMaterial;
            go.SetActive(false);
            InstanceCount++;
            return r;
        }

        LineRenderer CreateLine(int index)
        {
            var go = new GameObject($"Beam_{index:00}");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial != null ? lineMaterial : spriteMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = beamWidth;
            line.endWidth = beamWidth;
            line.numCapVertices = 2;
            line.sortingOrder = beamSortingOrder;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            go.SetActive(false);
            return line;
        }
    }
}
