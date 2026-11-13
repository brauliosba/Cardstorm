using System.Collections.Generic;
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
        }

        readonly List<Strike> pending = new(32);

        public int PendingCount => pending.Count;

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (flashes == null) flashes = FindFirstObjectByType<FlashManager>();
        }

        public void Schedule(Vector2 center, float radius, float damage, float delay, Color color,
            float slowFactor = 1f, float slowDuration = 0f)
        {
            if (delay <= 0f)
            {
                Detonate(center, radius, damage, slowFactor, slowDuration, color);
                return;
            }

            int telegraph = flashes != null ? flashes.Telegraph(center, radius, delay, color) : 0;
            pending.Add(new Strike
            {
                Center = center, Radius = radius, Damage = damage,
                SlowFactor = slowFactor, SlowDuration = slowDuration,
                Remaining = delay, Color = color, TelegraphId = telegraph,
            });
        }

        void Detonate(Vector2 center, float radius, float damage, float slowFactor, float slowDuration, Color color)
        {
            AreaDamage.Apply(center, radius, damage, slowFactor, slowDuration);
            if (flashes != null) flashes.Area(center, radius, color);
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
                Detonate(s.Center, s.Radius, s.Damage, s.SlowFactor, s.SlowDuration, s.Color);
            }
        }
    }
}
