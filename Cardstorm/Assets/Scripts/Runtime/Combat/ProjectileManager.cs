using System.Collections.Generic;
using Cardstorm.Core;
using Cardstorm.Enemies;
using Cardstorm.Player;
using UnityEngine;
using UnityEngine.Pool;

namespace Cardstorm.Combat
{
    // Mueve todos los proyectiles en un único Update y los saca de un pool precalentado.
    public class ProjectileManager : MonoBehaviour
    {
        [SerializeField] Projectile prefab;
        [SerializeField] GameClock clock;

        [Tooltip("Instancias creadas al empezar la partida.")]
        [SerializeField, Min(0)] int prewarm = 64;
        [SerializeField, Min(1)] int maxSize = 256;

        readonly List<Projectile> active = new(256);
        readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        ObjectPool<Projectile> pool;
        ContactFilter2D filter;
        ContactFilter2D hostileFilter;

        public int ActiveCount => active.Count;

        // Instancias creadas en total (activas + en el pool). No debe crecer tras el precalentamiento.
        public int InstanceCount { get; private set; }

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();

            filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Layers.EnemyMask | Layers.ObstacleMask);
            hostileFilter = new ContactFilter2D { useTriggers = false };
            hostileFilter.SetLayerMask(Layers.PlayerMask | Layers.ObstacleMask);

            pool = new ObjectPool<Projectile>(
                createFunc: Create,
                actionOnGet: null,                         // Projectile.Launch configura y activa la instancia
                actionOnRelease: p => p.Deactivate(),
                actionOnDestroy: p => { InstanceCount--; Destroy(p.gameObject); },
                collectionCheck: Application.isEditor,
                defaultCapacity: Mathf.Max(1, prewarm),
                maxSize: maxSize);

            var buffer = new List<Projectile>(prewarm);
            for (int i = 0; i < prewarm; i++) buffer.Add(pool.Get());
            foreach (var p in buffer) pool.Release(p);
        }

        public void Launch(Vector2 origin, Vector2 dir, float speed, float maxDistance, float radius, float damage, int pierce, Color color)
            => Launch(origin, dir, speed, maxDistance, radius, damage, pierce, color, false);

        // Proyectil enemigo: daña al jugador y se destruye contra obstáculos.
        public void LaunchHostile(Vector2 origin, Vector2 dir, float speed, float maxDistance, float radius, float damage, Color color)
            => Launch(origin, dir, speed, maxDistance, radius, damage, 0, color, true);

        void Launch(Vector2 origin, Vector2 dir, float speed, float maxDistance, float radius, float damage, int pierce, Color color, bool hostile)
        {
            if (dir.sqrMagnitude < 1e-6f || speed <= 0f || maxDistance <= 0f) return;

            var projectile = pool.Get();
            projectile.Launch(origin, dir.normalized, speed, maxDistance, radius, damage, pierce, color, hostile);
            projectile.ActiveIndex = active.Count;
            active.Add(projectile);
        }

        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                Release(active[i]);
        }

        void Update()
        {
            float dt = clock != null && clock.Paused ? 0f : Time.deltaTime;
            if (dt <= 0f) return;

            // Hacia atrás: Release intercambia con el último y no se salta ninguno.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var projectile = active[i];
                if (!Step(projectile, dt)) Release(projectile);
            }
        }

        // Devuelve false si el proyectil ha terminado.
        bool Step(Projectile p, float dt)
        {
            float step = Mathf.Min(p.Speed * dt, p.MaxDistance - p.Travelled);
            int count = Physics2D.CircleCast(p.Position, p.Radius, p.Direction, p.Hostile ? hostileFilter : filter, hits, step);
            SortByDistance(count);

            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider.gameObject.layer == Layers.Obstacle) return false;

                if (p.Hostile)
                {
                    // Se consume aunque el jugador sea invulnerable en ese momento.
                    if (!collider.TryGetComponent(out PlayerHealth player)) continue;
                    player.TakeDamage(p.Damage);
                    return false;
                }

                if (!collider.TryGetComponent(out Enemy enemy) || !enemy.IsAlive || p.Hit.Contains(enemy)) continue;

                p.Hit.Add(enemy);
                enemy.TakeDamage(p.Damage);
                p.PierceLeft--;
                if (p.PierceLeft < 0) return false;
            }

            p.Position += p.Direction * step;
            p.Travelled += step;
            p.transform.position = p.Position;
            return p.Travelled < p.MaxDistance;
        }

        // Inserción.
        void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                var hit = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > hit.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }
                hits[j + 1] = hit;
            }
        }

        // Quita en O(1) intercambiando con el último y devuelve la instancia al pool.
        void Release(Projectile projectile)
        {
            int index = projectile.ActiveIndex;
            if (index < 0 || index >= active.Count || active[index] != projectile) return;

            int last = active.Count - 1;
            if (index != last)
            {
                active[index] = active[last];
                active[index].ActiveIndex = index;
            }
            active.RemoveAt(last);
            pool.Release(projectile);
        }

        Projectile Create()
        {
            var projectile = Instantiate(prefab, transform);
            projectile.name = $"{prefab.name}_{InstanceCount:000}";
            projectile.gameObject.SetActive(false);
            InstanceCount++;
            return projectile;
        }
    }
}
