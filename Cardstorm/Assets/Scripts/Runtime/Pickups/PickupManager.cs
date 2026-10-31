using System.Collections.Generic;
using Cardstorm.Core;
using Cardstorm.Enemies;
using Cardstorm.Progression;
using UnityEngine;

namespace Cardstorm.Pickups
{
    public enum PickupKind { Xp, Gold, Chest }

    // Orbes de experiencia, monedas y cofres. Sin física: un único Update los atrae hacia el jugador y los
    // recoge por distancia. Las instancias salen de pools precalentados y avanzan con GameClock.DeltaTime.
    public class PickupManager : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] EnemyManager enemies;
        [SerializeField] GameClock clock;
        [Tooltip("Por defecto, el jugador de EnemyManager.")]
        [SerializeField] Transform player;

        [Header("Aspecto")]
        [SerializeField] Sprite xpSprite;
        [SerializeField] Sprite coinSprite;
        [SerializeField] Sprite chestSprite;
        [Tooltip("Sprite-Unlit-Default, como el resto del greybox.")]
        [SerializeField] Material spriteMaterial;
        [SerializeField] Color xpColor = new(0.55f, 0.85f, 1f);
        [SerializeField] Color coinColor = new(1f, 0.82f, 0.15f);
        [SerializeField] Color chestColor = new(0.55f, 0.33f, 0.15f);
        [SerializeField, Min(0.05f)] float xpSize = 0.35f;
        [SerializeField, Min(0.05f)] float coinSize = 0.4f;
        [SerializeField, Min(0.05f)] float chestSize = 1f;
        [Tooltip("Por debajo de los enemigos y por encima del suelo.")]
        [SerializeField] int sortingOrder = -5;

        [Header("Pools")]
        [SerializeField, Min(0)] int xpPrewarm = 400;
        [SerializeField, Min(0)] int coinPrewarm = 100;
        [SerializeField, Min(0)] int chestPrewarm = 4;

        [Header("Atracción y recogida")]
        [SerializeField, Min(0f)] float attractStartSpeed = 6f;
        [SerializeField, Min(0f)] float attractMaxSpeed = 18f;
        [Tooltip("Segundos que tarda en pasar de la velocidad inicial a la máxima.")]
        [SerializeField, Min(0.01f)] float attractRampTime = 0.3f;
        [SerializeField, Min(0f)] float collectDistance = 0.4f;
        [Tooltip("Los cofres no se atraen: se recogen al tocarlos.")]
        [SerializeField, Min(0f)] float chestCollectDistance = 0.8f;

        [Header("Fusión de orbes")]
        [Tooltip("Con más orbes activos que este número, uno nuevo se suma al orbe cercano.")]
        [SerializeField, Min(0)] int mergeAbove = 300;
        [SerializeField, Min(0f)] float mergeDistance = 1.5f;

        [Tooltip("Separación entre el orbe y la moneda que suelta un mismo enemigo.")]
        [SerializeField, Min(0f)] float coinOffset = 0.3f;

        sealed class Pickup
        {
            public PickupKind Kind;
            public Transform Transform;
            public Vector2 Position;
            public float Value;
            public GoldSource Source;
            public bool Attracted;
            public float AttractTime;
            public int ActiveIndex = -1;
        }

        const int KindCount = 3;

        readonly List<Pickup> active = new(512);
        readonly Stack<Pickup>[] pools = { new(400), new(100), new(4) };
        readonly int[] activeByKind = new int[KindCount];

        public int ActiveCount => active.Count;

        // Instancias creadas en total (activas + en los pools). No debe crecer tras el precalentamiento.
        public int InstanceCount { get; private set; }

        public int ActiveOf(PickupKind kind) => activeByKind[(int)kind];

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (run == null) run = FindFirstObjectByType<RunManager>();
            if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
            if (player == null && enemies != null) player = enemies.Player;

            Prewarm(PickupKind.Xp, xpPrewarm);
            Prewarm(PickupKind.Gold, coinPrewarm);
            Prewarm(PickupKind.Chest, chestPrewarm);
        }

        void OnEnable()
        {
            if (enemies != null) enemies.EnemyKilled += OnEnemyKilled;
        }

        void OnDisable()
        {
            if (enemies != null) enemies.EnemyKilled -= OnEnemyKilled;
        }

        public void SpawnXp(Vector2 position, float xp)
        {
            if (xp <= 0f) return;

            if (activeByKind[(int)PickupKind.Xp] > mergeAbove)
            {
                var nearest = NearestXp(position, mergeDistance);
                if (nearest != null)
                {
                    nearest.Value += xp;
                    return;
                }
            }
            Spawn(PickupKind.Xp, position, xp, GoldSource.Enemy);
        }

        public void SpawnGold(Vector2 position, int gold, GoldSource source)
        {
            if (gold > 0) Spawn(PickupKind.Gold, position, gold, source);
        }

        public void SpawnChest(Vector2 position, int gold)
        {
            if (gold > 0) Spawn(PickupKind.Chest, position, gold, GoldSource.Chest);
        }

        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                Release(active[i]);
        }

        void OnEnemyKilled(Enemy enemy, Vector2 position)
        {
            var definition = enemy.Definition;
            var state = run != null ? run.State : null;
            if (definition == null || state == null) return;

            SpawnXp(position, DropRoller.Xp(definition.xp, enemy.XpMultiplier));

            int gold = DropRoller.RollGold(definition.goldAverage, definition.goldChance, state.Random);
            if (gold > 0)
            {
                float angle = (float)(state.Random.NextDouble() * Mathf.PI * 2.0);
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * coinOffset;
                SpawnGold(position + offset, gold, GoldSource.Enemy);
            }
        }

        void Update()
        {
            var state = run != null ? run.State : null;
            if (state == null || player == null) return;

            float dt = clock != null ? clock.DeltaTime : Time.deltaTime;
            if (dt <= 0f) return;

            Vector2 playerPos = player.position;
            float radius = state.Stats.Value(StatType.PickupRadius);
            float radiusSqr = radius * radius;
            float chestSqr = chestCollectDistance * chestCollectDistance;

            // Hacia atrás: Release intercambia con el último y no se salta ninguno.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i];
                Vector2 toPlayer = playerPos - p.Position;
                float distSqr = toPlayer.sqrMagnitude;

                if (p.Kind == PickupKind.Chest)
                {
                    if (distSqr <= chestSqr) Collect(p);
                    continue;
                }

                if (!p.Attracted)
                {
                    if (distSqr > radiusSqr) continue;
                    p.Attracted = true;
                    p.AttractTime = 0f;
                }

                p.AttractTime += dt;
                float speed = Mathf.Lerp(attractStartSpeed, attractMaxSpeed, p.AttractTime / attractRampTime);
                float dist = Mathf.Sqrt(distSqr);
                float step = speed * dt;
                if (dist - step <= collectDistance)
                {
                    Collect(p);
                    continue;
                }

                p.Position += toPlayer * (step / dist);
                p.Transform.position = p.Position;
            }
        }

        void Collect(Pickup p)
        {
            if (p.Kind == PickupKind.Xp) run.AddXp(p.Value);
            else run.AddGold(Mathf.RoundToInt(p.Value), p.Source);
            Release(p);
        }

        Pickup NearestXp(Vector2 position, float maxDistance)
        {
            Pickup nearest = null;
            float bestSqr = maxDistance * maxDistance;
            for (int i = 0; i < active.Count; i++)
            {
                var p = active[i];
                if (p.Kind != PickupKind.Xp) continue;
                float d = (p.Position - position).sqrMagnitude;
                if (d <= bestSqr)
                {
                    bestSqr = d;
                    nearest = p;
                }
            }
            return nearest;
        }

        void Spawn(PickupKind kind, Vector2 position, float value, GoldSource source)
        {
            var pool = pools[(int)kind];
            var p = pool.Count > 0 ? pool.Pop() : Create(kind);

            p.Position = position;
            p.Value = value;
            p.Source = source;
            p.Attracted = false;
            p.AttractTime = 0f;
            p.Transform.position = position;
            p.Transform.gameObject.SetActive(true);

            p.ActiveIndex = active.Count;
            active.Add(p);
            activeByKind[(int)kind]++;
        }

        // Quita en O(1) intercambiando con el último y devuelve la instancia a su pool.
        void Release(Pickup p)
        {
            int index = p.ActiveIndex;
            if (index < 0 || index >= active.Count || active[index] != p) return;

            int last = active.Count - 1;
            if (index != last)
            {
                active[index] = active[last];
                active[index].ActiveIndex = index;
            }
            active.RemoveAt(last);
            activeByKind[(int)p.Kind]--;

            p.ActiveIndex = -1;
            p.Transform.gameObject.SetActive(false);
            pools[(int)p.Kind].Push(p);
        }

        void Prewarm(PickupKind kind, int count)
        {
            var pool = pools[(int)kind];
            for (int i = 0; i < count; i++) pool.Push(Create(kind));
        }

        Pickup Create(PickupKind kind)
        {
            var go = new GameObject($"{kind}_{InstanceCount:000}") { layer = Layers.Pickup };
            go.transform.SetParent(transform, false);
            float size = kind switch { PickupKind.Gold => coinSize, PickupKind.Chest => chestSize, _ => xpSize };
            go.transform.localScale = new Vector3(size, size, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = kind switch { PickupKind.Gold => coinSprite, PickupKind.Chest => chestSprite, _ => xpSprite };
            renderer.color = kind switch { PickupKind.Gold => coinColor, PickupKind.Chest => chestColor, _ => xpColor };
            renderer.sortingOrder = kind == PickupKind.Chest ? sortingOrder - 1 : sortingOrder;
            if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;

            go.SetActive(false);
            InstanceCount++;
            return new Pickup { Kind = kind, Transform = go.transform };
        }
    }
}
