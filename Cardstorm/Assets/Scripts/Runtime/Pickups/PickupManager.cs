using System.Collections.Generic;
using Cardstorm.Cards;
using Cardstorm.Combat;
using Cardstorm.Core;
using Cardstorm.Enemies;
using Cardstorm.Progression;
using Cardstorm.UI;
using UnityEngine;

namespace Cardstorm.Pickups
{
    public enum PickupKind { Xp, Gold, Chest, Energy }

    // Orbes de experiencia y de energía, monedas y cofres. Sin física: un único Update los atrae hacia el jugador y los
    // recoge por distancia. Las instancias salen de pools precalentados y avanzan con GameClock.DeltaTime.
    public class PickupManager : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] EnemyManager enemies;
        [SerializeField] GameClock clock;
        [SerializeField] PlayerCards cards;
        [Tooltip("Por defecto, el jugador de EnemyManager.")]
        [SerializeField] Transform player;

        [Header("Aspecto")]
        [SerializeField] Sprite xpSprite;
        [SerializeField] Sprite coinSprite;
        [SerializeField] Sprite chestSprite;
        [SerializeField] Sprite energySprite;
        [Tooltip("Sprite-Unlit-Default, como el resto del greybox.")]
        [SerializeField] Material spriteMaterial;
        [SerializeField] Color xpColor = new(0.55f, 0.85f, 1f);
        [SerializeField] Color coinColor = new(1f, 0.82f, 0.15f);
        [SerializeField] Color chestColor = new(0.55f, 0.33f, 0.15f);
        [Tooltip("Por defecto, el de la barra de energía.")]
        [SerializeField] Color energyColor = HudColors.Energy;
        [SerializeField, Min(0.05f)] float xpSize = 0.35f;
        [SerializeField, Min(0.05f)] float coinSize = 0.4f;
        [Tooltip("Sprite del dime (moneda que vale el doble). Sin él, se usa la moneda escalada con dimeScale.")]
        [SerializeField] Sprite dimeSprite;
        [Tooltip("Escala del dime respecto a la moneda normal, si no tiene sprite propio.")]
        [SerializeField, Min(1f)] float dimeScale = 1.35f;
        [SerializeField, Min(0.05f)] float chestSize = 1f;
        [SerializeField, Min(0.05f)] float energySize = 0.4f;
        [Tooltip("Por debajo de los enemigos y por encima del suelo.")]
        [SerializeField] int sortingOrder = -5;

        [Header("Brillo")]
        [Tooltip("Frames del brillo, después del sprite base. Vacío: sin brillo.")]
        [SerializeField] Sprite[] xpGlint;
        [SerializeField] Sprite[] coinGlint;
        [SerializeField] Sprite[] dimeGlint;
        [SerializeField] Sprite[] energyGlint;
        [SerializeField] Sprite[] chestGlint;
        [Tooltip("Segundos entre dos brillos del mismo recogible. Cada uno empieza desfasado.")]
        [SerializeField, Min(0.1f)] float glintPeriod = 1.5f;
        [SerializeField, Min(0.01f)] float glintFrameTime = 1f / 30f;

        [Header("VFX")]
        [Tooltip("Por defecto, el VfxManager de la escena.")]
        [SerializeField] VfxManager vfx;
        [SerializeField] ParticleSystem xpCollectVfx;
        [SerializeField] ParticleSystem goldCollectVfx;
        [SerializeField] ParticleSystem energyCollectVfx;
        [Tooltip("Polvo al aparecer el cofre.")]
        [SerializeField] ParticleSystem chestSpawnVfx;
        [Tooltip("El cofre se abre y desaparece: lleva dentro el sprite del cofre.")]
        [SerializeField] ParticleSystem chestOpenVfx;

        [Header("Pools")]
        [SerializeField, Min(0)] int xpPrewarm = 400;
        [SerializeField, Min(0)] int coinPrewarm = 400;
        [SerializeField, Min(0)] int chestPrewarm = 4;
        [SerializeField, Min(0)] int energyPrewarm = 60;

        [Header("Energía")]
        [Tooltip("Energía que da cada orbe.")]
        [SerializeField, Min(0f)] float energyPerOrb = 1f;

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

        [Tooltip("Separación entre el orbe de experiencia y la moneda o el orbe de energía que suelta un mismo enemigo.")]
        [SerializeField, Min(0f)] float coinOffset = 0.3f;

        sealed class Pickup
        {
            public PickupKind Kind;
            public Transform Transform;
            public SpriteRenderer Renderer;
            public bool Dime;
            public float GlintPhase;
            public int GlintFrame;
            public Vector2 Position;
            public float Value;
            public GoldSource Source;
            public bool Attracted;
            public float AttractTime;
            public int ActiveIndex = -1;
        }

        const int KindCount = 4;

        readonly List<Pickup> active = new(512);
        readonly Stack<Pickup>[] pools = { new(400), new(400), new(4), new(60) };
        readonly int[] activeByKind = new int[KindCount];

        public int ActiveCount => active.Count;

        // Instancias creadas en total (activas + en los pools). No debe crecer tras el precalentamiento.
        public int InstanceCount { get; private set; }

        public int ActiveOf(PickupKind kind) => activeByKind[(int)kind];

        float glintTime;

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (run == null) run = FindFirstObjectByType<RunManager>();
            if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
            if (player == null && enemies != null) player = enemies.Player;
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
            if (vfx == null) vfx = FindFirstObjectByType<VfxManager>();
            if (energySprite == null) Debug.LogWarning("PickupManager no tiene sprite de energía: usa el de la experiencia.", this);

            Prewarm(PickupKind.Xp, xpPrewarm);
            Prewarm(PickupKind.Gold, coinPrewarm);
            Prewarm(PickupKind.Chest, chestPrewarm);
            Prewarm(PickupKind.Energy, energyPrewarm);
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
            if (gold <= 0) return;
            Spawn(PickupKind.Chest, position, gold, GoldSource.Chest);
            if (vfx != null && chestSpawnVfx != null) vfx.Burst(chestSpawnVfx, position);
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

            int gold = DropRoller.RollGold(definition.coinValue, definition.doubleCoinChance, state.Random);
            if (gold > 0)
            {
                float angle = (float)(state.Random.NextDouble() * Mathf.PI * 2.0);
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * coinOffset;
                var source = definition.isBoss ? GoldSource.Boss : GoldSource.Enemy;
                var coin = Spawn(PickupKind.Gold, position + offset, gold, source);
                if (gold > definition.coinValue) SetDime(coin);
            }

            if (energyPerOrb > 0f && DropRoller.RollEnergy(definition.energyChance, state.Random))
            {
                float angle = (float)(state.Random.NextDouble() * Mathf.PI * 2.0);
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * coinOffset;
                Spawn(PickupKind.Energy, position + offset, energyPerOrb, GoldSource.Enemy);
            }
        }

        void Update()
        {
            var state = run != null ? run.State : null;
            if (state == null || player == null) return;

            float dt = clock != null ? clock.DeltaTime : Time.deltaTime;
            if (dt <= 0f) return;

            glintTime = (glintTime + dt) % glintPeriod;
            Vector2 playerPos = player.position;
            float radius = state.Stats.Value(StatType.PickupRadius);
            float radiusSqr = radius * radius;
            float chestSqr = chestCollectDistance * chestCollectDistance;
            // Con la barra llena, los orbes de energía se quedan en el suelo hasta que haya sitio.
            bool energyFull = cards == null || cards.Energy == null || cards.Energy.IsFull;

            // Hacia atrás: Release intercambia con el último y no se salta ninguno.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i];
                UpdateGlint(p);
                Vector2 toPlayer = playerPos - p.Position;
                float distSqr = toPlayer.sqrMagnitude;

                if (p.Kind == PickupKind.Chest)
                {
                    if (distSqr <= chestSqr) Collect(p);
                    continue;
                }

                if (p.Kind == PickupKind.Energy && energyFull)
                {
                    p.Attracted = false;
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
            else if (p.Kind == PickupKind.Energy) cards.Energy.Add(p.Value);
            else run.AddGold(Mathf.RoundToInt(p.Value), p.Source);

            var collectVfx = p.Kind switch
            {
                PickupKind.Xp => xpCollectVfx,
                PickupKind.Gold => goldCollectVfx,
                PickupKind.Energy => energyCollectVfx,
                _ => chestOpenVfx,
            };
            if (vfx != null && collectVfx != null) vfx.Burst(collectVfx, p.Position);
            Release(p);
        }

        Sprite BaseSprite(Pickup p) => p.Kind switch
        {
            PickupKind.Gold => p.Dime && dimeSprite != null ? dimeSprite : coinSprite,
            PickupKind.Chest => chestSprite,
            PickupKind.Energy when energySprite != null => energySprite,
            _ => xpSprite,
        };

        Sprite[] Glint(Pickup p) => p.Kind switch
        {
            PickupKind.Gold => p.Dime && dimeSprite != null ? dimeGlint : coinGlint,
            PickupKind.Chest => chestGlint,
            PickupKind.Energy => energyGlint,
            _ => xpGlint,
        };

        // Frame 0 es el sprite base; del 1 al N, los del brillo. Solo cambia el sprite cuando cambia el frame.
        void UpdateGlint(Pickup p)
        {
            var glint = Glint(p);
            if (glint == null || glint.Length == 0) return;
            float t = glintTime + p.GlintPhase;
            if (t >= glintPeriod) t -= glintPeriod;
            int frame = (int)(t / glintFrameTime) + 1;
            if (frame > glint.Length) frame = 0;
            if (frame == p.GlintFrame) return;
            p.GlintFrame = frame;
            p.Renderer.sprite = frame == 0 ? BaseSprite(p) : glint[frame - 1];
        }

        void SetDime(Pickup p)
        {
            p.Dime = true;
            if (dimeSprite != null) p.Renderer.sprite = dimeSprite;
            else SetSize(p, coinSize * dimeScale);
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

        Pickup Spawn(PickupKind kind, Vector2 position, float value, GoldSource source)
        {
            var pool = pools[(int)kind];
            var p = pool.Count > 0 ? pool.Pop() : Create(kind);

            p.Position = position;
            p.Value = value;
            p.Source = source;
            p.Attracted = false;
            p.AttractTime = 0f;
            p.Transform.position = position;
            p.Dime = false;
            p.GlintPhase = Random.value * glintPeriod;
            p.GlintFrame = 0;
            p.Renderer.sprite = BaseSprite(p);
            if (kind == PickupKind.Gold) SetSize(p, coinSize);
            p.Transform.gameObject.SetActive(true);

            p.ActiveIndex = active.Count;
            active.Add(p);
            activeByKind[(int)kind]++;
            return p;
        }

        static void SetSize(Pickup p, float size) => p.Transform.localScale = new Vector3(size, size, 1f);

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
            float size = kind switch { PickupKind.Gold => coinSize, PickupKind.Chest => chestSize, PickupKind.Energy => energySize, _ => xpSize };
            go.transform.localScale = new Vector3(size, size, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            var p = new Pickup { Kind = kind, Transform = go.transform, Renderer = renderer };
            renderer.sprite = BaseSprite(p);
            renderer.color = kind switch { PickupKind.Gold => coinColor, PickupKind.Chest => chestColor, PickupKind.Energy => energyColor, _ => xpColor };
            renderer.sortingOrder = kind == PickupKind.Chest ? sortingOrder - 1 : sortingOrder;
            if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;

            go.SetActive(false);
            InstanceCount++;
            return p;
        }
    }
}
