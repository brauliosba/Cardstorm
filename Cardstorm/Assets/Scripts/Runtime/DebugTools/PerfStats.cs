using System;

namespace Cardstorm.DebugTools
{
    // Estadísticas de una serie de tiempos de frame (en segundos).
    // - FPS medio: frames / tiempo total.
    // - Percentil 1 %: FPS medio del 1 % de frames más lentos (al menos un frame).
    public readonly struct PerfStats
    {
        public readonly int Frames;
        public readonly float TotalSeconds;
        public readonly float AverageFps;
        public readonly float OnePercentLowFps;
        public readonly float AverageFrameMs;
        public readonly float MaxFrameMs;

        PerfStats(int frames, float totalSeconds, float averageFps, float onePercentLowFps, float averageFrameMs, float maxFrameMs)
        {
            Frames = frames;
            TotalSeconds = totalSeconds;
            AverageFps = averageFps;
            OnePercentLowFps = onePercentLowFps;
            AverageFrameMs = averageFrameMs;
            MaxFrameMs = maxFrameMs;
        }

        // Usa las count primeras muestras de frameTimes sin modificar el array.
        public static PerfStats Compute(float[] frameTimes, int count)
        {
            if (count <= 0) return default;

            var sorted = new float[count];
            Array.Copy(frameTimes, sorted, count);
            Array.Sort(sorted);  // De más rápido a más lento

            double total = 0;
            for (int i = 0; i < count; i++) total += sorted[i];

            int slowCount = Math.Max(1, (int)Math.Ceiling(count * 0.01));
            double slowTotal = 0;
            for (int i = count - slowCount; i < count; i++) slowTotal += sorted[i];

            return new PerfStats(
                count,
                (float)total,
                (float)(count / total),
                (float)(slowCount / slowTotal),
                (float)(total / count * 1000.0),
                sorted[count - 1] * 1000f);
        }
    }
}
