using System.Collections.Generic;
using Cardstorm.Core;
using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.Combat
{
    // Torretas de las cartas con un pool fijo y un único Update. Cada una dispara al enemigo vivo más cercano.
    // Si no quedan libres, se recicla la más antigua.
    public class TurretManager : MonoBehaviour
    {
        [SerializeField] Turret prefab;
        [SerializeField] GameClock clock;
        [Tooltip("Si se deja vacío, se busca en la escena.")]
        [SerializeField] EnemyManager enemies;
        [Tooltip("Si se deja vacío, se busca en la escena.")]
        [SerializeField] ProjectileManager projectiles;

        [Tooltip("Torretas creadas al empezar. No crece.")]
        [SerializeField, Min(1)] int capacity = 6;
        [Tooltip("Ancho en unidades.")]
        [SerializeField, Min(0.05f)] float size = 0.6f;
        [Tooltip("Segundos que tarda en desaparecer al acabar.")]
        [SerializeField, Min(0.01f)] float fadeDuration = 0.2f;

        Stack<Turret> free;
        List<Turret> active;
        int nextOrder;

        public int ActiveCount => active.Count;

        // Instancias creadas en total (activas + en el pool). No debe crecer tras el precalentamiento.
        public int InstanceCount { get; private set; }

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
            if (projectiles == null) projectiles = FindFirstObjectByType<ProjectileManager>();

            free = new Stack<Turret>(capacity);
            active = new List<Turret>(capacity);
            for (int i = 0; i < capacity; i++) free.Push(Create());
        }

        public void Place(Vector2 position, float duration, float fireInterval, float damage, float range,
            float projectileSpeed, float projectileRadius, float projectileDistance, Color color)
        {
            if (duration <= 0f) return;
            if (free.Count == 0) Release(Oldest());

            var turret = free.Pop();
            turret.Place(position, duration, fireInterval, damage, range,
                projectileSpeed, projectileRadius, projectileDistance, color, nextOrder++);
            turret.ActiveIndex = active.Count;
            active.Add(turret);
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
                var turret = active[i];
                if (!Step(turret, dt)) Release(turret);
            }
        }

        // Devuelve false si la torreta ha terminado.
        bool Step(Turret t, float dt)
        {
            if (t.Fade >= 0f)
            {
                t.Fade += dt;
                t.SetAlpha(1f - t.Fade / fadeDuration);
                return t.Fade < fadeDuration;
            }

            t.Remaining -= dt;
            if (t.Remaining <= 0f)
            {
                t.Fade = 0f;
                return true;
            }

            t.FireTimer -= dt;
            if (t.FireTimer > 0f) return true;

            var target = Nearest(t.Position, t.Range);
            if (target == null)
            {
                // No gasta el disparo: lo vuelve a intentar en el siguiente fotograma.
                t.FireTimer = 0f;
                return true;
            }

            Vector2 dir = target.Body.position - t.Position;
            if (projectiles != null)
                projectiles.Launch(t.Position, dir, t.ProjectileSpeed, t.ProjectileDistance,
                    t.ProjectileRadius, t.Damage, 0, t.Color);
            t.FireTimer += t.FireInterval;
            return true;
        }

        Enemy Nearest(Vector2 origin, float range)
        {
            if (enemies == null) return null;

            var list = enemies.Active;
            Enemy best = null;
            float bestSqr = range * range;
            for (int i = 0; i < list.Count; i++)
            {
                var enemy = list[i];
                if (!enemy.IsAlive) continue;
                float sqr = (enemy.Body.position - origin).sqrMagnitude;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                best = enemy;
            }
            return best;
        }

        Turret Oldest()
        {
            var oldest = active[0];
            for (int i = 1; i < active.Count; i++)
                if (active[i].Order < oldest.Order) oldest = active[i];
            return oldest;
        }

        // Quita en O(1) intercambiando con el último y devuelve la instancia al pool.
        void Release(Turret turret)
        {
            int index = turret.ActiveIndex;
            if (index < 0 || index >= active.Count || active[index] != turret) return;

            int last = active.Count - 1;
            if (index != last)
            {
                active[index] = active[last];
                active[index].ActiveIndex = index;
            }
            active.RemoveAt(last);
            turret.Deactivate();
            free.Push(turret);
        }

        Turret Create()
        {
            var turret = Instantiate(prefab, transform);
            turret.name = $"{prefab.name}_{InstanceCount:00}";
            var sprite = turret.Renderer.sprite;
            float spriteSize = sprite != null ? sprite.bounds.size.x : 1f;
            turret.transform.localScale = Vector3.one * (size / spriteSize);
            turret.gameObject.SetActive(false);
            InstanceCount++;
            return turret;
        }
    }
}
