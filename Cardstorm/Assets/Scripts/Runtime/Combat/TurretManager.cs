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
        // VFX de una carta de torreta. Lo que quede vacío no se dibuja.
        public struct TurretLook
        {
            public ProjectileVfx Projectile;
            public ParticleSystem Appear, End;
        }

        internal const float PixelsPerUnit = 16f;

        // Caída al ponerla, en píxeles por frame de 30 fps; aterriza en el último.
        static readonly float[] DropPixels = { 6f, 3f, 1f, 0f };
        const float Frame = 1f / 30f;
        static readonly float DropTime = (DropPixels.Length - 1) * Frame;
        // Boca del cañón respecto al centro del sprite, en píxeles, mirando a la derecha.
        static readonly Vector2 Muzzle = new(8f, 2.5f);
        const float RecoilTime = 2 * Frame;
        // Parpadea al final para avisar de que se acaba: 3 frames visible y 3 oculta.
        const float BlinkTime = 0.6f;
        const float BlinkHalf = 3 * Frame;

        [SerializeField] Turret prefab;
        [SerializeField] GameClock clock;
        [Tooltip("Si se deja vacío, se busca en la escena.")]
        [SerializeField] EnemyManager enemies;
        [Tooltip("Si se deja vacío, se busca en la escena.")]
        [SerializeField] ProjectileManager projectiles;
        [Tooltip("Si se deja vacío, se busca en la escena.")]
        [SerializeField] VfxManager vfx;

        [Tooltip("Torretas creadas al empezar. No crece.")]
        [SerializeField, Min(1)] int capacity = 6;

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
            if (vfx == null) vfx = FindFirstObjectByType<VfxManager>();

            free = new Stack<Turret>(capacity);
            active = new List<Turret>(capacity);
            for (int i = 0; i < capacity; i++) free.Push(Create());
        }

        public void Place(Vector2 position, float duration, float fireInterval, float damage, float range,
            float projectileSpeed, float projectileRadius, float projectileDistance, Color color, in DamageSource source,
            in TurretLook look = default)
        {
            if (duration <= 0f) return;
            if (free.Count == 0) Release(Oldest(), true);

            var turret = free.Pop();
            turret.Place(position, duration, fireInterval, damage, range,
                projectileSpeed, projectileRadius, projectileDistance, color, in source, in look, nextOrder++);
            turret.ActiveIndex = active.Count;
            active.Add(turret);
            UpdateView(turret);
        }

        // Sin nube: es para limpiar al acabar la partida.
        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                Release(active[i], false);
        }

        void Update()
        {
            float dt = clock != null && clock.Paused ? 0f : Time.deltaTime;
            if (dt <= 0f) return;

            // Hacia atrás: Release intercambia con el último y no se salta ninguno.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var turret = active[i];
                if (!Step(turret, dt)) Release(turret, true);
                else UpdateView(turret);
            }
        }

        // Devuelve false si la torreta ha terminado.
        bool Step(Turret t, float dt)
        {
            t.Age += dt;
            t.Remaining -= dt;
            if (t.Remaining <= 0f) return false;

            t.Recoil = Mathf.Max(0f, t.Recoil - dt);
            if (!t.Landed)
            {
                if (t.Age < DropTime) return true;
                t.Landed = true;
                if (vfx != null) vfx.Burst(t.Look.Appear, t.Position);
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

            Vector2 toTarget = target.Body.position - t.Position;
            t.FacingLeft = toTarget.x < 0f;
            Vector2 muzzle = t.Position + new Vector2(t.FacingLeft ? -Muzzle.x : Muzzle.x, Muzzle.y) / PixelsPerUnit;
            // Desde la boca; si el enemigo está pegado a ella, desde el centro.
            Vector2 dir = target.Body.position - muzzle;
            if (Vector2.Dot(dir, toTarget) <= 0f) dir = toTarget;

            if (projectiles != null)
                projectiles.Launch(muzzle, dir, t.ProjectileSpeed, t.ProjectileDistance,
                    t.ProjectileRadius, t.Damage, 0, t.Color, t.Source, look: t.Look.Projectile);
            t.Recoil = RecoilTime;
            t.FireTimer += t.FireInterval;
            return true;
        }

        static void UpdateView(Turret t)
        {
            float y = t.Landed ? 0f : DropPixels[Mathf.Min((int)(t.Age / Frame), DropPixels.Length - 1)];
            float x = t.Recoil > 0f ? (t.FacingLeft ? 1f : -1f) : 0f;
            bool visible = t.Remaining > BlinkTime || (int)((BlinkTime - t.Remaining) / BlinkHalf) % 2 == 0;
            t.SetView(new Vector2(x, y), visible);
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
        void Release(Turret turret, bool puff)
        {
            int index = turret.ActiveIndex;
            if (index < 0 || index >= active.Count || active[index] != turret) return;

            if (puff && vfx != null) vfx.Burst(turret.Look.End, turret.Position);

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

        // A su tamaño en píxeles: sin escalar, como el resto del pixel art.
        Turret Create()
        {
            var turret = Instantiate(prefab, transform);
            turret.name = $"{prefab.name}_{InstanceCount:00}";
            turret.gameObject.SetActive(false);
            InstanceCount++;
            return turret;
        }
    }
}
