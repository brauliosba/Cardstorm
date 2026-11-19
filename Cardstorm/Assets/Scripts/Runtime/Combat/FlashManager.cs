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
        [Tooltip("Anillo del aviso en pixel art. Si se deja vacío, se usa el anillo general.")]
        [SerializeField] Sprite telegraphRingSprite;
        [Tooltip("Círculo que crece dentro del aviso. Si se deja vacío, se usa el círculo general.")]
        [SerializeField] Sprite telegraphFillSprite;
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

        [Header("Rayo quebrado")]
        [Tooltip("Un material por frame, con el perfil del rayo a lo ancho y repetido a lo largo. Vacío = segmento recto.")]
        [SerializeField] Material[] boltMaterials;
        [SerializeField, Min(0.01f)] float boltFrameTime = 1f / 30f;
        [Tooltip("Ancho de las texturas.")]
        [SerializeField, Min(0.01f)] float boltWidth = 0.3125f;
        [Tooltip("Largo de las texturas: se repiten cada tanto.")]
        [SerializeField, Min(0.01f)] float boltTile = 0.75f;
        [Tooltip("Distancia media entre los quiebros.")]
        [SerializeField, Min(0.05f)] float boltStep = 0.375f;
        [Tooltip("Desvío máximo de cada quiebro; alternan de lado.")]
        [SerializeField, Min(0f)] float boltJitter = 0.25f;

        const int MaxBoltPoints = 32;
        const float PixelsPerUnit = 16f;
        static readonly Vector3[] BoltPoints = new Vector3[MaxBoltPoints];

        enum Kind { Area, Telegraph, Beam, Bolt }

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
            public int Frame;          // Solo en los rayos quebrados
            public Vector2 From, To;
        }

        Stack<SpriteRenderer> free;
        Stack<LineRenderer> freeLines;
        List<Flash> active;
        float circleSize = 1f;
        float ringSize = 1f;
        float telegraphRingSize = 1f;
        float telegraphFillSize = 1f;
        int nextId = 1;

        public int ActiveCount => active.Count;
        public int InstanceCount { get; private set; }

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (circleSprite != null) circleSize = circleSprite.bounds.size.x;
            if (ringSprite != null) ringSize = ringSprite.bounds.size.x;
            if (telegraphRingSprite == null) telegraphRingSprite = ringSprite;
            if (telegraphFillSprite == null) telegraphFillSprite = circleSprite;
            if (telegraphRingSprite != null) telegraphRingSize = telegraphRingSprite.bounds.size.x;
            if (telegraphFillSprite != null) telegraphFillSize = telegraphFillSprite.bounds.size.x;

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
            SetAlpha(main, color, areaAlpha * GameSettings.Current.FlashScale);
        }

        // Anillo del tamaño final con un círculo que crece hasta llenarlo cuando acaba el aviso.
        // Devuelve 0 si no cabe.
        public int Telegraph(Vector2 center, float radius, float duration, Color color)
        {
            if (free.Count < 2 || duration <= 0f) return 0;

            var ring = Take(telegraphRingSprite, telegraphRingSize, center, radius, sortingOrder + 1);
            var fill = Take(telegraphFillSprite, telegraphFillSize, center, 0f, sortingOrder);
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

            // El pool es compartido con los rayos quebrados: se deja como lo crea CreateLine.
            var line = freeLines.Pop();
            line.sharedMaterial = lineMaterial != null ? lineMaterial : spriteMaterial;
            line.textureMode = LineTextureMode.Stretch;
            line.startWidth = beamWidth;
            line.endWidth = beamWidth;
            line.numCapVertices = 2;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            SetAlpha(line, color, GameSettings.Current.FlashScale);
            line.gameObject.SetActive(true);
            active.Add(new Flash { Id = nextId++, Kind = Kind.Beam, Line = line, Color = color, Duration = beamDuration });
        }

        // Rayo en zigzag que cambia de forma en cada frame. Sin texturas, es un segmento recto del color.
        public void Bolt(Vector2 from, Vector2 to, Color color)
        {
            if (boltMaterials == null || boltMaterials.Length == 0)
            {
                Beam(from, to, color);
                return;
            }
            if (freeLines.Count < 1) return;

            var line = freeLines.Pop();
            line.textureMode = LineTextureMode.Tile;
            line.textureScale = new Vector2(1f / boltTile, 1f);
            line.startWidth = boltWidth;
            line.endWidth = boltWidth;
            line.numCapVertices = 0;
            SetAlpha(line, Color.white, GameSettings.Current.FlashScale);
            SetBoltFrame(line, 0, from, to);
            line.gameObject.SetActive(true);
            active.Add(new Flash
            {
                Id = nextId++, Kind = Kind.Bolt, Line = line, Color = Color.white,
                Duration = boltMaterials.Length * boltFrameTime, From = from, To = to,
            });
        }

        // Un material por frame y no un MaterialPropertyBlock: con un bloque, el shader de sprites
        // pierde el color y el volteo que pone el motor, y la línea no se ve.
        void SetBoltFrame(LineRenderer line, int frame, Vector2 from, Vector2 to)
        {
            line.sharedMaterial = boltMaterials[frame];

            Vector2 delta = to - from;
            float length = delta.magnitude;
            int n = Mathf.Clamp(Mathf.RoundToInt(length / boltStep), 1, MaxBoltPoints - 1);
            Vector2 normal = length > 1e-4f ? new Vector2(-delta.y, delta.x) / length : Vector2.zero;
            float minJitter = Mathf.Min(1f / PixelsPerUnit, boltJitter);
            for (int i = 0; i <= n; i++)
            {
                Vector2 p = to;
                if (i == 0) p = from;
                else if (i < n)
                {
                    float t = (i + Random.Range(-0.35f, 0.35f)) / n;
                    float side = (i & 1) == 0 ? -1f : 1f;
                    p = from + delta * t + normal * (side * Random.Range(minJitter, boltJitter));
                    // En la rejilla de píxeles, para que los quiebros salgan nítidos.
                    p.x = Mathf.Round(p.x * PixelsPerUnit) / PixelsPerUnit;
                    p.y = Mathf.Round(p.y * PixelsPerUnit) / PixelsPerUnit;
                }
                BoltPoints[i] = p;
            }
            line.positionCount = n + 1;
            for (int i = 0; i <= n; i++) line.SetPosition(i, BoltPoints[i]);
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

            // Los avisos no se atenúan con menos destellos: son información.
            float scale = GameSettings.Current.FlashScale;

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
                    SetAlpha(f.Main, f.Color, areaAlpha * scale * (1f - t));
                else if (f.Kind == Kind.Telegraph)
                    SetRadius(f.Fill, telegraphFillSize, f.Radius * t);
                else if (f.Kind == Kind.Bolt)
                {
                    // Se apaga con sus frames, no con el alfa.
                    int frame = Mathf.Min((int)(f.Timer / boltFrameTime), boltMaterials.Length - 1);
                    if (frame != f.Frame)
                    {
                        f.Frame = frame;
                        SetBoltFrame(f.Line, frame, f.From, f.To);
                    }
                }
                else
                    SetAlpha(f.Line, f.Color, scale * (1f - t));

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
