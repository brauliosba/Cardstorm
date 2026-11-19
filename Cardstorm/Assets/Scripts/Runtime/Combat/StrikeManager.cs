using System.Collections.Generic;
using Cardstorm.CameraRig;
using Cardstorm.Core;
using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.Combat
{
    // Impactos con retraso: muestran un aviso en el suelo y dañan el área al acabar.
    // El punto es fijo: no sigue a los enemigos.
    public class StrikeManager : MonoBehaviour
    {
        [SerializeField] GameClock clock;
        [Tooltip("Para los avisos y las explosiones. Si se deja vacío, se busca en la escena.")]
        [SerializeField] FlashManager flashes;
        [Tooltip("Partículas de las cartas. Si se deja vacío, se busca en la escena.")]
        [SerializeField] VfxManager vfx;
        [Tooltip("Sacudida al impactar. Si se deja vacío, se busca en la escena.")]
        [SerializeField] CameraShake cameraShake;

        // Aspecto del impacto. Lo que quede vacío no se dibuja; sin Impact, se usa el círculo del color.
        public struct Visuals
        {
            public ParticleSystem Impact;
            // Cae sobre el punto y llega justo al impacto: se lanza cuando falta su vida para acabar el aviso.
            public ParticleSystem Fall;
            public float Scale;
            public float Shake;
            public float ShakeDuration;
        }

        struct Strike
        {
            public Vector2 Center;
            public float Radius;
            public float Damage;
            public float SlowFactor;
            public float SlowDuration;
            public float Remaining;
            public Color Color;
            public int TelegraphId;
            public DamageSource Source;
            public Visuals Visuals;
            public float FallLead;
            public bool Fell;
        }

        // Golpe a un enemigo concreto cuando le llega una onda.
        struct Hit
        {
            public Enemy Enemy;
            public int Spawn;
            public Vector2 Origin;
            public float Damage;
            public float Remaining;
            public DamageSource Source;
            public ParticleSystem Vfx;
            // Solo en los saltos de un rayo: sale del enemigo anterior, o de Origin si no lo hay.
            public bool Bolt;
            public Enemy From;
            public int FromSpawn;
            public Color Color;
        }

        readonly List<Strike> pending = new(32);
        readonly List<Hit> hits = new(64);

        public int PendingCount => pending.Count;

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (flashes == null) flashes = FindFirstObjectByType<FlashManager>();
            if (vfx == null) vfx = FindFirstObjectByType<VfxManager>();
            if (cameraShake == null) cameraShake = FindFirstObjectByType<CameraShake>();
        }

        public void Schedule(Vector2 center, float radius, float damage, float delay, Color color, in DamageSource source,
            float slowFactor = 1f, float slowDuration = 0f, in Visuals visuals = default)
        {
            if (delay <= 0f)
            {
                Detonate(center, radius, damage, slowFactor, slowDuration, color, in source, in visuals);
                return;
            }

            int telegraph = flashes != null ? flashes.Telegraph(center, radius, delay, color) : 0;
            pending.Add(new Strike
            {
                Center = center, Radius = radius, Damage = damage,
                SlowFactor = slowFactor, SlowDuration = slowDuration,
                Remaining = delay, Color = color, TelegraphId = telegraph, Source = source,
                Visuals = visuals,
                FallLead = visuals.Fall != null ? visuals.Fall.main.startLifetime.constantMax : 0f,
            });
        }

        // Daña al enemigo pasado el retraso, si sigue vivo y es el mismo (no ha vuelto al pool).
        // El efecto sale en su posición de ese momento, girado desde el origen.
        public void ScheduleHit(Enemy enemy, float damage, float delay, Vector2 origin, in DamageSource source, ParticleSystem hitVfx = null)
        {
            var hit = new Hit
            {
                Enemy = enemy, Spawn = enemy.Spawns, Origin = origin,
                Damage = damage, Remaining = delay, Source = source, Vfx = hitVfx,
            };
            if (delay <= 0f) Land(in hit);
            else hits.Add(hit);
        }

        // Salto de un rayo: pasado el retraso, dibuja el rayo desde el enemigo anterior (o desde el origen)
        // y daña al enemigo. Un enemigo muerto conserva su posición hasta que vuelve a salir del pool.
        public void ScheduleChainHit(Enemy enemy, Enemy from, Vector2 origin, float damage, float delay, Color color,
            in DamageSource source, ParticleSystem hitVfx = null)
        {
            var hit = new Hit
            {
                Enemy = enemy, Spawn = enemy.Spawns, Origin = origin,
                Damage = damage, Remaining = delay, Source = source, Vfx = hitVfx,
                Bolt = true, From = from, FromSpawn = from != null ? from.Spawns : 0, Color = color,
            };
            if (delay <= 0f) Land(in hit);
            else hits.Add(hit);
        }

        void Land(in Hit hit)
        {
            var enemy = hit.Enemy;
            if (enemy == null || enemy.Spawns != hit.Spawn) return;

            Vector2 pos = enemy.Body.position;
            Vector2 origin = hit.Origin;
            if (hit.Bolt)
            {
                // El rayo llega aunque otro golpe lo haya matado: el salto siguiente sale de aquí.
                if (hit.From != null && hit.From.Spawns == hit.FromSpawn) origin = hit.From.Body.position;
                if (flashes != null) flashes.Bolt(origin, pos, hit.Color);
            }
            if (!enemy.IsAlive) return;

            enemy.TakeDamage(hit.Damage, in hit.Source);
            if (hit.Vfx != null && vfx != null) vfx.Burst(hit.Vfx, pos, pos - origin, orient: true);
        }

        void Detonate(Vector2 center, float radius, float damage, float slowFactor, float slowDuration, Color color,
            in DamageSource source, in Visuals visuals)
        {
            AreaDamage.Apply(center, radius, damage, in source, slowFactor, slowDuration);
            if (visuals.Impact != null && vfx != null) vfx.Burst(visuals.Impact, center, scale: visuals.Scale > 0f ? visuals.Scale : 1f);
            else if (flashes != null) flashes.Area(center, radius, color);
            if (visuals.Shake > 0f && cameraShake != null) cameraShake.AddShake(visuals.Shake, visuals.ShakeDuration);
        }

        public void ReleaseAll()
        {
            if (flashes != null)
                for (int i = 0; i < pending.Count; i++) flashes.Cancel(pending[i].TelegraphId);
            pending.Clear();
            hits.Clear();
        }

        void Update()
        {
            float dt = clock != null && clock.Paused ? 0f : Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = hits.Count - 1; i >= 0; i--)
            {
                var h = hits[i];
                h.Remaining -= dt;
                if (h.Remaining > 0f)
                {
                    hits[i] = h;
                    continue;
                }

                int last = hits.Count - 1;
                if (i != last) hits[i] = hits[last];
                hits.RemoveAt(last);
                Land(in h);
            }

            // Hacia atrás: se quita intercambiando con el último y no se salta ninguno.
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var s = pending[i];
                s.Remaining -= dt;
                if (!s.Fell && s.Visuals.Fall != null && vfx != null && s.Remaining <= s.FallLead)
                {
                    vfx.Burst(s.Visuals.Fall, s.Center);
                    s.Fell = true;
                }
                if (s.Remaining > 0f)
                {
                    pending[i] = s;
                    continue;
                }

                int last = pending.Count - 1;
                if (i != last) pending[i] = pending[last];
                pending.RemoveAt(last);

                // El aviso acaba a la vez; se cancela por si el redondeo lo deja un fotograma más.
                if (flashes != null) flashes.Cancel(s.TelegraphId);
                Detonate(s.Center, s.Radius, s.Damage, s.SlowFactor, s.SlowDuration, s.Color, in s.Source, in s.Visuals);
            }
        }
    }
}
