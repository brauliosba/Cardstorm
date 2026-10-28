using System;
using System.Collections.Generic;
using Cardstorm.CameraRig;
using Cardstorm.Core;
using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.Spawning
{
    // Une reloj, planificador, formaciones y EnemyManager: avanza el SpawnScheduler con el tiempo de juego
    // y convierte cada petición en enemigos colocados.
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] SpawnCurve curve;
        [SerializeField] GameClock clock;
        [SerializeField] EnemyManager enemies;
        [SerializeField] MapBounds map;

        [Header("Posiciones")]
        [Tooltip("Distancia fuera de la vista a la que aparecen los enemigos.")]
        [SerializeField, Min(0f)] float spawnMargin = 2f;
        [Tooltip("Distancia extra del Cerco sobre la media diagonal de la vista.")]
        [SerializeField, Min(0f)] float ringExtra = 1f;
        [SerializeField, Min(1)] int stampedeWidth = 3;
        [SerializeField, Min(0.1f)] float stampedeSpacing = 0.9f;

        [Header("Formaciones")]
        [Tooltip("Tipos que en la Pinza forman una línea sobre uno de los dos bordes (los tiradores). El resto se reparte entre ambos.")]
        [SerializeField] EnemyDefinition[] pincerLineTypes = Array.Empty<EnemyDefinition>();

        SpawnScheduler scheduler;
        SpawnPositionPicker picker;
        Camera cam;

        readonly List<SpawnRequest> pending = new(16);
        readonly List<SpawnRequest> pulse = new(8);
        readonly List<Vector2> points = new(64);
        readonly List<ViewSide> sides = new(4);

        public SpawnScheduler Scheduler => scheduler;
        public SpawnCurve Curve => curve;
        public SpawnSegment CurrentSegment => scheduler?.CurrentSegment;
        public WaveEvent NextWave => scheduler?.NextWave;
        public double TimeToNextWave => scheduler?.TimeToNextWave ?? double.PositiveInfinity;
        public BossEvent LastBoss { get; private set; }

        public bool SpawningPaused
        {
            get => scheduler != null && scheduler.SpawningPaused;
            set { if (scheduler != null) scheduler.SpawningPaused = value; }
        }

        public event Action<BossEvent> BossTimeReached;

        void Awake()
        {
            foreach (var issue in curve.Validate())
                Debug.LogWarning($"[WaveSpawner] {curve.name}: {issue}", curve);

            scheduler = new SpawnScheduler(curve, pending.Add);
            scheduler.BossTimeReached += OnBossTimeReached;
            picker = new SpawnPositionPicker(map.Rect, Layers.ObstacleMask);
        }

        void OnEnable() => clock.Jumped += OnClockJumped;
        void OnDisable() => clock.Jumped -= OnClockJumped;

        void Start() => cam = Camera.main;

        void Update()
        {
            scheduler.Tick(clock.DeltaTime);
            if (pending.Count == 0) return;

            Rect view = CameraMath.ViewRect(cam.transform.position, cam.orthographicSize, cam.aspect);
            Vector2 playerPos = enemies.Player.position;

            // Las peticiones de un mismo pulso llegan seguidas: se procesan juntas para colocar la formación entera.
            for (int i = 0; i < pending.Count;)
            {
                var request = pending[i];
                if (request.Source == SpawnSource.Segment)
                {
                    SpawnSegmentGroup(request, view, playerPos);
                    i++;
                    continue;
                }

                pulse.Clear();
                int id = request.PulseId;
                while (i < pending.Count && pending[i].PulseId == id)
                    pulse.Add(pending[i++]);
                SpawnWavePulse(view, playerPos);
            }
            pending.Clear();
        }

        // Grupo de los tramos: entra por un borde de la vista, fuera de pantalla.
        void SpawnSegmentGroup(SpawnRequest request, Rect view, Vector2 playerPos)
        {
            var definition = curve.enemyTypes[request.TypeIndex];
            Vector2 anchor = picker.PickEdgeAnchor(view, spawnMargin, curve.groupSpread + definition.radius);
            FormationMath.Cluster(anchor, request.Count, curve.groupSpread, points);
            SpawnAt(definition, request.Reinforcement, playerPos);
        }

        void SpawnWavePulse(Rect view, Vector2 playerPos)
        {
            switch (pulse[0].Formation)
            {
                case WaveFormation.Ring: SpawnRing(view, playerPos); break;
                case WaveFormation.Pincer: SpawnPincer(view, playerPos); break;
                case WaveFormation.Stampede: SpawnStampede(view, playerPos); break;
                default: SpawnMixed(view, playerPos); break;
            }
        }

        // Cerco: anillo alrededor del jugador que se cierra porque todos lo persiguen.
        void SpawnRing(Rect view, Vector2 playerPos)
        {
            float radius = FormationMath.RingRadius(view, ringExtra);
            float startAngle = UnityEngine.Random.value * Mathf.PI * 2f;
            for (int t = 0; t < pulse.Count; t++)
            {
                var request = pulse[t];
                // Cada tipo desfasado medio hueco para que no coincidan en el mismo ángulo.
                float offset = t * Mathf.PI / Mathf.Max(1, request.Count);
                FormationMath.Ring(playerPos, request.Count, radius, startAngle + offset, points);
                SpawnAt(curve.enemyTypes[request.TypeIndex], request.Reinforcement, playerPos);
            }
        }

        // Pinza: los rápidos por dos bordes opuestos y los tiradores en línea sobre uno de ellos.
        void SpawnPincer(Rect view, Vector2 playerPos)
        {
            bool horizontal = UnityEngine.Random.value < 0.5f;
            ViewSide lineSide = horizontal
                ? (UnityEngine.Random.value < 0.5f ? ViewSide.Left : ViewSide.Right)
                : (UnityEngine.Random.value < 0.5f ? ViewSide.Top : ViewSide.Bottom);

            foreach (var request in pulse)
            {
                var definition = curve.enemyTypes[request.TypeIndex];
                if (Array.IndexOf(pincerLineTypes, definition) >= 0)
                    FormationMath.EdgeLine(view, lineSide, request.Count, spawnMargin, points);
                else
                    FormationMath.Pincer(view, horizontal, request.Count, spawnMargin, points);
                SpawnAt(definition, request.Reinforcement, playerPos);
            }
        }

        // Estampida: columna densa desde un borde, con el tipo más grande al frente.
        void SpawnStampede(Rect view, Vector2 playerPos)
        {
            ViewSide side = picker.PickSide(view, spawnMargin);

            int frontType = -1;
            float frontRadius = 0f;
            foreach (var request in pulse)
            {
                float r = curve.enemyTypes[request.TypeIndex].radius;
                if (r > frontRadius)
                {
                    frontRadius = r;
                    frontType = request.TypeIndex;
                }
            }

            float behind = pulse.Count > 1 ? frontRadius * 2f + stampedeSpacing : 0f;
            foreach (var request in pulse)
            {
                var definition = curve.enemyTypes[request.TypeIndex];
                float frontOffset = request.TypeIndex == frontType ? 0f : behind;
                float spacing = Mathf.Max(stampedeSpacing, definition.radius * 2.2f);
                FormationMath.Column(view, side, request.Count, stampedeWidth, spacing, spawnMargin, frontOffset, points);
                SpawnAt(definition, request.Reinforcement, playerPos);
            }
        }

        // Tormenta: cada tipo desde un borde distinto.
        void SpawnMixed(Rect view, Vector2 playerPos)
        {
            picker.ShuffledSides(sides);
            for (int t = 0; t < pulse.Count; t++)
            {
                var request = pulse[t];
                FormationMath.EdgeLine(view, sides[t % sides.Count], request.Count, spawnMargin, points);
                SpawnAt(curve.enemyTypes[request.TypeIndex], request.Reinforcement, playerPos);
            }
        }

        void SpawnAt(EnemyDefinition definition, float reinforcement, Vector2 playerPos)
        {
            picker.ResolveAll(points, definition.radius, playerPos);
            foreach (var p in points)
                enemies.Spawn(definition, p, reinforcement);
        }

        // Para probar en Play desde el inspector (menú ⋮ del componente) hasta que existan las teclas de depuración.
        [ContextMenu("Saltar a la siguiente oleada")]
        void JumpToNextWave()
        {
            if (NextWave != null) clock.JumpTo(NextWave.startTime);
        }

        void OnClockJumped(float seconds)
        {
            pending.Clear();
            scheduler.JumpTo(seconds);
        }

        void OnBossTimeReached(BossEvent boss)
        {
            LastBoss = boss;
            Debug.Log($"[WaveSpawner] {GameClock.Format(scheduler.Time)} · {boss.name} (pendiente de implementar)");
            BossTimeReached?.Invoke(boss);
        }
    }
}
