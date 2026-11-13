using System.Collections.Generic;
using Cardstorm.Core;
using UnityEngine;

namespace Cardstorm.Combat
{
    // Destellos en el mundo (áreas y avisos) con un pool fijo de SpriteRenderer y un único Update.
    // Si el pool se llena, el destello no se dibuja: el daño se aplica igual.
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

        enum Kind { Area, Telegraph }

        struct Flash
        {
            public int Id;
            public Kind Kind;
            public SpriteRenderer Main;
            public SpriteRenderer Fill;  // Solo en los avisos: el círculo que crece
            public Color Color;
            public float Radius;
            public float Timer;
            public float Duration;
        }

        Stack<SpriteRenderer> free;
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
                else
                    SetRadius(f.Fill, circleSize, f.Radius * t);

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
            Return(f.Main);
            if (f.Fill != null) Return(f.Fill);

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
    }
}
