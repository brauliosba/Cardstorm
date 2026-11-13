using System;
using System.Collections.Generic;
using Cardstorm.Combat;
using Cardstorm.Core;
using Cardstorm.Navigation;
using Cardstorm.Pooling;
using Cardstorm.Spawning;
using UnityEngine;

namespace Cardstorm.Enemies
{
    // Gestor central: mueve a todos los enemigos activos en un único FixedUpdate y lleva los recuentos.
    public class EnemyManager : MonoBehaviour
    {
        [SerializeField] Transform player;
        [SerializeField] EnemyPool pool;
        [SerializeField] FlowFieldProvider flowField;
        [Tooltip("Para los disparos de los tiradores. Si se deja vacío, se busca en la escena.")]
        [SerializeField] ProjectileManager projectiles;
        [Tooltip("Para que las invocaciones no caigan en obstáculos. Si se deja vacío, se busca en la escena.")]
        [SerializeField] MapBounds map;

        [Header("Despawn de seguridad")]
        [Tooltip("Si un enemigo pasa más de despawnTime segundos a más de esta distancia del jugador, se recicla.")]
        [SerializeField, Min(1f)] float despawnDistance = 60f;
        [SerializeField, Min(0f)] float despawnTime = 5f;

        // Segundos entre parpadeos durante el aviso de la embestida.
        const float ChargeBlinkInterval = 0.1f;

        readonly List<Enemy> active = new(600);
        readonly Dictionary<EnemyDefinition, int> aliveByType = new();
        SpawnPositionPicker picker;

        public event Action<Enemy> EnemySpawned;
        public event Action<Enemy, Vector2> EnemyKilled;
        public event Action<Enemy> EnemyDespawned;

        public int AliveCount => active.Count;
        public int TotalSpawned { get; private set; }
        public int TotalKilled { get; private set; }
        public int TotalDespawned { get; private set; }
        public IReadOnlyDictionary<EnemyDefinition, int> AliveByType => aliveByType;
        public IReadOnlyList<Enemy> Active => active;
        public EnemyPool Pool => pool;
        public Transform Player => player;

        void Awake()
        {
            if (projectiles == null) projectiles = FindFirstObjectByType<ProjectileManager>();
            if (map == null) map = FindFirstObjectByType<MapBounds>();
        }

        void Start()
        {
            if (map != null) picker = new SpawnPositionPicker(map.Rect, Layers.ObstacleMask);
        }

        public int AliveOf(EnemyDefinition definition) => aliveByType.TryGetValue(definition, out int n) ? n : 0;

        public Enemy Spawn(EnemyDefinition definition, Vector2 position, float reinforcement = 1f)
        {
            var enemy = pool.Get(definition);
            enemy.Activate(definition, position, reinforcement);
            enemy.Manager = this;
            enemy.ActiveIndex = active.Count;
            active.Add(enemy);
            aliveByType[definition] = AliveOf(definition) + 1;
            TotalSpawned++;
            EnemySpawned?.Invoke(enemy);
            return enemy;
        }

        // Lo llama Enemy al llegar a 0 de vida.
        internal void NotifyKilled(Enemy enemy)
        {
            Vector2 position = enemy.Body.position;
            TotalKilled++;
            EnemyKilled?.Invoke(enemy, position);
            Remove(enemy);
        }

        // Recicla sin contar como muerte.
        public void Despawn(Enemy enemy)
        {
            TotalDespawned++;
            EnemyDespawned?.Invoke(enemy);
            Remove(enemy);
        }

        public void DespawnAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                Despawn(active[i]);
        }

        void FixedUpdate()
        {
            if (player == null) return;

            Vector2 playerPos = player.position;
            float dt = Time.fixedDeltaTime;
            float despawnDistanceSqr = despawnDistance * despawnDistance;

            // Hacia atrás: Despawn intercambia con el último y no se salta ninguno.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var enemy = active[i];
                var body = enemy.Body;
                Vector2 pos = body.position;

                bool boss = enemy.IsBoss;
                if (!boss && (playerPos - pos).sqrMagnitude > despawnDistanceSqr)
                {
                    enemy.FarTimer += dt;
                    if (enemy.FarTimer > despawnTime)
                    {
                        Despawn(enemy);
                        continue;
                    }
                }
                else
                {
                    enemy.FarTimer = 0f;
                }

                if (enemy.FlashTimer > 0f)
                {
                    enemy.FlashTimer -= dt;
                    if (enemy.FlashTimer <= 0f) enemy.RestoreColor();
                }

                enemy.TickSlow(dt);

                if (enemy.IsRetreating)
                {
                    if (Retreat(enemy, pos, playerPos, dt)) Despawn(enemy);
                    continue;
                }

                Vector2 chaseDir = flowField != null && !boss ? flowField.GetDirection(pos) : (playerPos - pos).normalized;

                var behaviour = enemy.Definition.behaviour;
                if (behaviour == EnemyBehaviour.KeepDistance)
                    body.linearVelocity = KeepDistance(enemy, pos, playerPos, chaseDir, dt);
                else if (behaviour == EnemyBehaviour.Charge)
                    body.linearVelocity = Charge(enemy, pos, playerPos, chaseDir, dt);
                else if (behaviour == EnemyBehaviour.Patterns)
                    body.linearVelocity = Patterns(enemy, pos, playerPos, chaseDir, dt);
                else
                    body.linearVelocity = chaseDir * enemy.CurrentSpeed;
            }
        }

        // Se aleja del jugador y se desvanece. Devuelve true al terminar.
        bool Retreat(Enemy enemy, Vector2 pos, Vector2 playerPos, float dt)
        {
            enemy.RetreatTimer += dt;
            enemy.SetFade(1f - enemy.RetreatTimer / enemy.RetreatDuration);

            Vector2 away = pos - playerPos;
            float distance = away.magnitude;
            enemy.Body.linearVelocity = distance > 1e-4f ? away / distance * enemy.RetreatSpeed : Vector2.zero;
            return enemy.RetreatFinished;
        }

        // Persigue y alterna invocar, embestir y disparar un anillo. Quieto y parpadeando durante cada aviso.
        Vector2 Patterns(Enemy enemy, Vector2 pos, Vector2 playerPos, Vector2 chaseDir, float dt)
        {
            var def = enemy.Definition;
            var patterns = enemy.Patterns;
            var settings = def.PatternSettings;

            // La embestida del patrón solo avanza mientras dura: en Approach empezaría sola.
            bool chargeFinished = enemy.Charge.State == ChargeState.Approach;
            if (patterns.State == PatternState.Busy && !chargeFinished)
                return Charge(enemy, pos, playerPos, chaseDir, dt);

            switch (patterns.Step(dt, chargeFinished, settings))
            {
                case PatternAction.StartCharge:
                    Vector2 toPlayer = playerPos - pos;
                    float distance = toPlayer.magnitude;
                    enemy.ChargeDirection = distance > 1e-4f ? toPlayer / distance : Vector2.right;
                    enemy.Charge.Begin();
                    enemy.ShowChargeTelegraph(enemy.ChargeDirection, def.chargeDistance + def.radius, 0f);
                    enemy.SetBlink(true);
                    return Vector2.zero;

                case PatternAction.Fire:
                    enemy.SetBlink(false);
                    if (patterns.Current == BossPattern.Summon) Summon(enemy, pos, playerPos);
                    else if (patterns.Current == BossPattern.Ring) Ring(enemy, pos);
                    return Vector2.zero;
            }

            if (patterns.State == PatternState.Windup)
            {
                float elapsed = patterns.WindupProgress * settings.WindupOf(patterns.Current);
                enemy.SetBlink((int)(elapsed / ChargeBlinkInterval) % 2 == 0);
                return Vector2.zero;
            }
            return chaseDir * enemy.CurrentSpeed;
        }

        // Enemigos en círculo alrededor del jefe, con un desfase al azar y fuera de los obstáculos.
        // Se añaden al final de active: el bucle hacia atrás no los mueve en este paso.
        void Summon(Enemy boss, Vector2 pos, Vector2 playerPos)
        {
            var def = boss.Definition;
            var summon = def.summonDefinition;
            if (summon == null || def.summonCount <= 0) return;

            float distance = def.radius + def.summonRadius;
            float step = 2f * Mathf.PI / def.summonCount;
            float phase = UnityEngine.Random.value * step;
            for (int i = 0; i < def.summonCount; i++)
            {
                float angle = phase + i * step;
                Vector2 point = pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (picker != null) point = picker.Resolve(point, summon.radius, playerPos);
                Spawn(summon, point);
            }
        }

        // Proyectiles repartidos en 360° desde el borde. Cada anillo gira medio hueco respecto al anterior.
        void Ring(Enemy boss, Vector2 pos)
        {
            var def = boss.Definition;
            if (projectiles == null || def.ringCount <= 0) return;

            float step = 2f * Mathf.PI / def.ringCount;
            float start = boss.RingTurn;
            boss.RingTurn = (start + step * 0.5f) % step;
            for (int i = 0; i < def.ringCount; i++)
            {
                float angle = start + i * step;
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
                projectiles.LaunchHostile(pos + dir * (def.radius + def.ringRadius), dir, def.ringSpeed,
                    def.ringRange, def.ringRadius, def.ringDamage, def.ringColor);
            }
        }

        // Persigue; cerca del jugador y con línea de visión se para, avisa y embiste en línea recta.
        Vector2 Charge(Enemy enemy, Vector2 pos, Vector2 playerPos, Vector2 chaseDir, float dt)
        {
            var def = enemy.Definition;
            var charge = enemy.Charge;
            var settings = def.ChargeSettings;
            Vector2 toPlayer = playerPos - pos;
            float distance = toPlayer.magnitude;
            var before = charge.State;
            float chargeSpeed = settings.Speed * enemy.SlowFactor;

            // La línea de visión solo se comprueba cuando puede empezar el aviso.
            bool clearPath = before == ChargeState.Approach && charge.Cooldown <= 0f
                && distance <= settings.TriggerRange
                && !Physics2D.Linecast(pos, playerPos, Layers.ObstacleMask);

            // Mira el tramo de este paso: si hay un obstáculo delante, se para contra él.
            bool blocked = before == ChargeState.Charging && !enemy.IsBoss
                && Physics2D.CircleCast(pos, def.radius, enemy.ChargeDirection, chargeSpeed * dt, Layers.ObstacleMask);

            var state = charge.Step(dt, distance, clearPath, blocked, settings, enemy.SlowFactor);

            if (state == ChargeState.Windup && before != ChargeState.Windup)
                enemy.ChargeDirection = distance > 1e-4f ? toPlayer / distance : Vector2.right;

            switch (state)
            {
                case ChargeState.Windup:
                    enemy.ShowChargeTelegraph(enemy.ChargeDirection, def.chargeDistance + def.radius,
                        charge.WindupProgress(settings));
                    enemy.SetBlink((int)(charge.Timer / ChargeBlinkInterval) % 2 == 0);
                    return Vector2.zero;

                case ChargeState.Charging:
                    if (before == ChargeState.Windup)
                    {
                        enemy.HideChargeTelegraph();
                        enemy.SetBlink(false);
                    }
                    return enemy.ChargeDirection * chargeSpeed;

                case ChargeState.Recover:
                    return Vector2.zero;

                default:
                    return chaseDir * enemy.CurrentSpeed;
            }
        }

        // Se acerca hasta la distancia preferida, retrocede si el jugador se acerca demasiado y dispara con línea de visión.
        Vector2 KeepDistance(Enemy enemy, Vector2 pos, Vector2 playerPos, Vector2 chaseDir, float dt)
        {
            var def = enemy.Definition;
            Vector2 toPlayer = playerPos - pos;
            float distance = toPlayer.magnitude;
            Vector2 aim = distance > 1e-4f ? toPlayer / distance : Vector2.zero;

            // La línea de visión solo se comprueba cuando importa: dentro del alcance o de la zona de espera.
            bool inRange = distance <= Mathf.Max(def.fireRange, def.preferredDistance + def.distanceTolerance);
            bool clearShot = inRange && !Physics2D.Linecast(pos, playerPos, Layers.ObstacleMask);

            enemy.FireTimer -= dt;
            if (enemy.FireTimer <= 0f)
            {
                if (clearShot && distance <= def.fireRange && projectiles != null)
                {
                    Vector2 origin = pos + aim * (def.radius + def.projectileRadius);
                    projectiles.LaunchHostile(origin, aim, def.projectileSpeed, def.projectileRange,
                        def.projectileRadius, def.projectileDamage, def.projectileColor);
                    enemy.FireTimer = def.fireInterval;
                }
                else
                {
                    // Sin tiro: lo reintenta enseguida en lugar de esperar un intervalo completo.
                    enemy.FireTimer = 0.2f;
                }
            }

            // Sin línea de visión se acerca rodeando obstáculos con el flow field.
            if (!clearShot || distance > def.preferredDistance + def.distanceTolerance)
                return chaseDir * enemy.CurrentSpeed;
            if (distance < def.preferredDistance - def.distanceTolerance)
                return -aim * enemy.CurrentSpeed;
            return Vector2.zero;
        }

        // Quita en O(1) intercambiando con el último y devuelve la instancia al pool.
        void Remove(Enemy enemy)
        {
            int index = enemy.ActiveIndex;
            if (index < 0 || index >= active.Count || active[index] != enemy) return;

            int last = active.Count - 1;
            if (index != last)
            {
                active[index] = active[last];
                active[index].ActiveIndex = index;
            }
            active.RemoveAt(last);
            aliveByType[enemy.Definition] = AliveOf(enemy.Definition) - 1;
            pool.Release(enemy);
        }
    }
}
