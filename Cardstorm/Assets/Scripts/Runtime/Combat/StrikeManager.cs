using System.Collections.Generic;
using Cardstorm.CameraRig;
using Cardstorm.Core;
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

        readonly List<Strike> pending = new(32);

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
        }

        void Update()
        {
            float dt = clock != null && clock.Paused ? 0f : Time.deltaTime;
            if (dt <= 0f) return;

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
