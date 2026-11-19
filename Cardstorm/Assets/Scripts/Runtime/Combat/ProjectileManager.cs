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
        [SerializeField] VfxManager vfx;

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
            if (vfx == null) vfx = FindFirstObjectByType<VfxManager>();

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

        // fullHits: impactos con el daño completo (por defecto, todos); los siguientes hacen damage · extraHitDamage.
        public void Launch(Vector2 origin, Vector2 dir, float speed, float maxDistance, float radius, float damage, int pierce, Color color,
            in DamageSource source, int fullHits = -1, float extraHitDamage = 1f, ProjectileVfx look = null)
            => Spawn(origin, dir, speed, maxDistance, radius, damage, pierce, color, false, fullHits, extraHitDamage, in source, null, look);

        // Proyectil enemigo: daña al jugador y se destruye contra obstáculos. sourceId: id del enemigo que dispara.
        public void LaunchHostile(Vector2 origin, Vector2 dir, float speed, float maxDistance, float radius, float damage, Color color, string sourceId)
            => Spawn(origin, dir, speed, maxDistance, radius, damage, 0, color, true, -1, 1f, DamageSource.None, sourceId, null);

        void Spawn(Vector2 origin, Vector2 dir, float speed, float maxDistance, float radius, float damage, int pierce, Color color, bool hostile,
            int fullHits, float extraHitDamage, in DamageSource source, string hostileSourceId, ProjectileVfx look)
        {
            if (dir.sqrMagnitude < 1e-6f || speed <= 0f || maxDistance <= 0f) return;

            var projectile = pool.Get();
            projectile.Launch(origin, dir.normalized, speed, maxDistance, radius, damage, pierce, color, hostile, fullHits, extraHitDamage, in source, hostileSourceId, look);
            if (look != null && vfx != null) vfx.Burst(look.muzzle, origin, dir);
            projectile.ActiveIndex = active.Count;
            active.Add(projectile);
        }

        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                Release(active[i]);
            if (vfx != null) vfx.ClearAll();
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
                Vector2 at = p.Position + p.Direction * hits[i].distance;
                if (collider.gameObject.layer == Layers.Obstacle)
                {
                    Impact(p, at);
                    return false;
                }

                if (p.Hostile)
                {
                    // Se consume aunque el jugador sea invulnerable en ese momento.
                    if (!collider.TryGetComponent(out PlayerHealth player)) continue;
                    player.TakeDamage(p.Damage, p.HostileSourceId, HitKind.Projectile);
                    return false;
                }

                if (!collider.TryGetComponent(out Enemy enemy) || !enemy.IsAlive || p.Hit.Contains(enemy)) continue;

                // Los impactos que añade Perforación hacen menos daño.
                float damage = p.Hit.Count < p.FullHits ? p.Damage : p.Damage * p.ExtraHitDamage;
                p.Hit.Add(enemy);
                enemy.TakeDamage(damage, p.Source);
                Impact(p, at);
                p.PierceLeft--;
                if (p.PierceLeft < 0) return false;
            }

            Vector2 from = p.Position;
            p.Position += p.Direction * step;
            p.Travelled += step;
            if (p.Vfx != null && vfx != null)
            {
                float carry = p.TrailCarry;
                vfx.Trail(p.Vfx.trail, from, p.Position, ref carry);
                p.TrailCarry = carry;
            }
            p.transform.position = p.Position;
            return p.Travelled < p.MaxDistance;
        }

        void Impact(Projectile p, Vector2 at)
        {
            if (p.Vfx != null && vfx != null) vfx.Burst(p.Vfx.impact, at, p.Direction);
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
