using System;
using UnityEngine;

namespace Cardstorm.Core
{
    // Reloj de la partida. Las apariciones avanzan con DeltaTime (no con Time.time),
    // así que una pausa del juego (subida de nivel, menú) las detiene.
    [DefaultExecutionOrder(-100)]
    public class GameClock : MonoBehaviour
    {
        static readonly float[] DebugScales = { 1f, 2f, 4f, 8f };

        int scaleIndex;

        // Segundos de partida transcurridos.
        public double Elapsed { get; private set; }

        // Tiempo de partida de este frame (0 en pausa).
        public float DeltaTime { get; private set; }

        public bool Paused { get; set; }

        // Escala de depuración (×1, ×2, ×4, ×8).
        public float TimeScale => DebugScales[scaleIndex];

        // Se lanza al saltar a otro momento de la partida.
        public event Action<float> Jumped;

        void Update()
        {
            DeltaTime = Paused ? 0f : Time.deltaTime * TimeScale;
            Elapsed += DeltaTime;
        }

        [ContextMenu("Cambiar escala (×1, ×2, ×4, ×8)")]
        public float CycleTimeScale()
        {
            scaleIndex = (scaleIndex + 1) % DebugScales.Length;
            Debug.Log($"[GameClock] Escala ×{TimeScale}");
            return TimeScale;
        }

        public void ResetTimeScale() => scaleIndex = 0;

        public void JumpTo(float seconds)
        {
            Elapsed = Mathf.Max(0f, seconds);
            DeltaTime = 0f;
            Jumped?.Invoke((float)Elapsed);
        }

        // mm:ss
        public static string Format(double seconds)
        {
            int total = Mathf.Max(0, (int)seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
