using System;
using System.Collections.Generic;
using Cardstorm.Combat;
using Cardstorm.Core;
using Cardstorm.Navigation;
using Cardstorm.Pooling;
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

        [Header("Despawn de seguridad")]
        [Tooltip("Si un enemigo pasa más de despawnTime segundos a más de esta distancia del jugador, se recicla.")]
        [SerializeField, Min(1f)] float despawnDistance = 60f;
        [SerializeField, Min(0f)] float despawnTime = 5f;

        // Segundos entre parpadeos durante el aviso de la embestida.
        const float ChargeBlinkInterval = 0.1f;

        readonly List<Enemy> active = new(600);
        readonly Dictionary<EnemyDefinition, int> aliveByType = new();

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

                Vector2 chaseDir = flowField != null && !boss ? flowField.GetDirection(pos) : (playerPos - pos).normalized;

                var behaviour = enemy.Definition.behaviour;
                if (behaviour == EnemyBehaviour.KeepDistance)
                    body.linearVelocity = KeepDistance(enemy, pos, playerPos, chaseDir, dt);
                else if (behaviour == EnemyBehaviour.Charge)
                    body.linearVelocity = Charge(enemy, pos, playerPos, chaseDir, dt);
                else
                    body.linearVelocity = chaseDir * enemy.Speed;
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

            // La línea de visión solo se comprueba cuando puede empezar el aviso.
            bool clearPath = before == ChargeState.Approach && charge.Cooldown <= 0f
                && distance <= settings.TriggerRange
                && !Physics2D.Linecast(pos, playerPos, Layers.ObstacleMask);

            // Mira el tramo de este paso: si hay un obstáculo delante, se para contra él.
            bool blocked = before == ChargeState.Charging && !enemy.IsBoss
                && Physics2D.CircleCast(pos, def.radius, enemy.ChargeDirection, settings.Speed * dt, Layers.ObstacleMask);

            var state = charge.Step(dt, distance, clearPath, blocked, settings);

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
                    return enemy.ChargeDirection * settings.Speed;

                case ChargeState.Recover:
                    return Vector2.zero;

                default:
                    return chaseDir * enemy.Speed;
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
                return chaseDir * enemy.Speed;
            if (distance < def.preferredDistance - def.distanceTolerance)
                return -aim * enemy.Speed;
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
