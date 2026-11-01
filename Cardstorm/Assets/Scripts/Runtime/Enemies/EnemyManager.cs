using System;
using System.Collections.Generic;
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

        [Header("Despawn de seguridad")]
        [Tooltip("Si un enemigo pasa más de despawnTime segundos a más de esta distancia del jugador, se recicla.")]
        [SerializeField, Min(1f)] float despawnDistance = 60f;
        [SerializeField, Min(0f)] float despawnTime = 5f;

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

                // En la greybox todos persiguen; los demás comportamientos se enchufan aquí.
                Vector2 dir = flowField != null && !boss ? flowField.GetDirection(pos) : (playerPos - pos).normalized;
                body.linearVelocity = dir * enemy.Speed;
            }
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
