using System.Collections.Generic;
using Cardstorm.Core;
using UnityEngine;

namespace Cardstorm.Combat
{
    // Una sola instancia por prefab de partículas, en espacio del mundo: cada efecto se emite
    // a mano en su posición, así que no hay que instanciar nada durante la partida.
    public class VfxManager : MonoBehaviour
    {
        [SerializeField] GameClock clock;

        sealed class Instance
        {
            public ParticleSystem Root;
            public ParticleSystem[] Systems;
            public int[] Counts;
            public float Spacing;
        }

        readonly Dictionary<ParticleSystem, Instance> instances = new();
        bool paused;

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
        }

        void Update()
        {
            bool nowPaused = clock != null && clock.Paused;
            if (nowPaused == paused) return;
            paused = nowPaused;
            foreach (var i in instances.Values)
            {
                if (paused) i.Root.Pause(true);
                else i.Root.Play(true);
            }
        }

        // Emite el burst de cada sistema del prefab (el raíz y sus hijos).
        public void Burst(ParticleSystem prefab, Vector2 position, Vector2 direction = default)
        {
            if (prefab == null) return;
            var inst = Get(prefab);

            float angle = direction.sqrMagnitude > 1e-6f ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg : 0f;
            inst.Root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));

            var p = new ParticleSystem.EmitParams { startColor = FlashColor() };
            for (int s = 0; s < inst.Systems.Length; s++)
                inst.Systems[s].Emit(p, inst.Counts[s]);
        }

        // Deja partículas a lo largo del segmento. carry guarda la distancia sobrante entre llamadas.
        public void Trail(ParticleSystem prefab, Vector2 from, Vector2 to, ref float carry)
        {
            if (prefab == null) return;
            var inst = Get(prefab);
            if (inst.Spacing <= 0f) return;

            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0f) return;
            Vector2 dir = delta / length;

            var p = new ParticleSystem.EmitParams { startColor = FlashColor(), applyShapeToPosition = true };
            float d = inst.Spacing - carry;
            for (; d <= length; d += inst.Spacing)
            {
                p.position = from + dir * d;
                inst.Root.Emit(p, 1);
            }
            carry = length - (d - inst.Spacing);
        }

        public void ClearAll()
        {
            foreach (var i in instances.Values) i.Root.Clear(true);
        }

        // Con menos destellos, las partículas salen más transparentes.
        static Color32 FlashColor()
        {
            Color c = Color.white;
            c.a = GameSettings.Current.FlashScale;
            return c;
        }

        Instance Get(ParticleSystem prefab)
        {
            if (instances.TryGetValue(prefab, out var inst)) return inst;

            var root = Instantiate(prefab, transform);
            root.name = prefab.name;
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            var counts = new int[systems.Length];
            for (int s = 0; s < systems.Length; s++)
            {
                var emission = systems[s].emission;
                counts[s] = emission.burstCount > 0 ? Mathf.Max(1, (int)emission.GetBurst(0).count.constant) : 1;
            }

            float rate = root.emission.rateOverDistance.constant;
            inst = new Instance { Root = root, Systems = systems, Counts = counts, Spacing = rate > 0f ? 1f / rate : 0f };
            instances.Add(prefab, inst);
            if (paused) root.Pause(true);
            return inst;
        }
    }
}
