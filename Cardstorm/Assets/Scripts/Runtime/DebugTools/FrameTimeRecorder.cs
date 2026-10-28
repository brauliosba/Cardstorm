using System;
using System.Globalization;
using System.IO;
using System.Text;
using Cardstorm.Enemies;
using Unity.Profiling;
using UnityEngine;

namespace Cardstorm.DebugTools
{
    // Graba los tiempos de frame durante unos segundos y guarda un resumen en
    // Application.persistentDataPath/perf_yyyyMMdd_HHmm.csv: FPS medio, percentil 1 %, enemigos vivos,
    // y, si el Profiler los expone, hilo principal, memoria asignada por el GC y física 2D.
    // Solo en el editor y en development build.
    public class FrameTimeRecorder : MonoBehaviour
    {
        [SerializeField, Min(1f)] float duration = 30f;
        [SerializeField] EnemyManager enemies;

        // Hasta 30 s a 600 fps sin asignar memoria mientras se graba.
        const int MaxSamples = 18000;

        readonly float[] frameTimes = new float[MaxSamples];
        int sampleCount;
        float elapsed;
        long enemySum;
        int enemyMax;

        ProfilerRecorder mainThread;
        ProfilerRecorder gcAlloc;
        ProfilerRecorder physics2D;
        double mainThreadSumMs;
        double physicsSumMs;
        long gcBytesTotal;
        int gcFrames;
        int profilerSamples;

        public bool IsRecording { get; private set; }
        public float Duration => duration;
        public float Elapsed => elapsed;
        public string LastResultPath { get; private set; }
        public PerfStats LastStats { get; private set; }

        public event Action RecordingStarted;
        public event Action<PerfStats, string> RecordingFinished;

        void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                enabled = false;
                return;
            }
            if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
        }

        void OnDisable() => DisposeRecorders();

        public void StartRecording()
        {
            if (IsRecording) return;

            sampleCount = 0;
            elapsed = 0f;
            enemySum = 0;
            enemyMax = 0;
            mainThreadSumMs = physicsSumMs = 0;
            gcBytesTotal = 0;
            gcFrames = profilerSamples = 0;

            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            physics2D = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics2D.Simulate", 1);

            IsRecording = true;
            Debug.Log($"[FrameTimeRecorder] Grabando {duration:0} s…");
            RecordingStarted?.Invoke();
        }

        void Update()
        {
            if (!IsRecording) return;

            float dt = Time.unscaledDeltaTime;
            if (sampleCount < MaxSamples)
                frameTimes[sampleCount++] = dt;
            elapsed += dt;

            int alive = enemies != null ? enemies.AliveCount : 0;
            enemySum += alive;
            enemyMax = Mathf.Max(enemyMax, alive);

            // Los recorders dan el valor del frame anterior.
            if (profilerSamples < sampleCount)
            {
                if (mainThread.Valid) mainThreadSumMs += mainThread.LastValue * 1e-6;
                if (physics2D.Valid) physicsSumMs += physics2D.LastValue * 1e-6;
                if (gcAlloc.Valid && gcAlloc.LastValue > 0)
                {
                    gcBytesTotal += gcAlloc.LastValue;
                    gcFrames++;
                }
                profilerSamples++;
            }

            if (elapsed >= duration)
                Finish();
        }

        void Finish()
        {
            IsRecording = false;
            var stats = PerfStats.Compute(frameTimes, sampleCount);
            LastStats = stats;

            string path = Path.Combine(Application.persistentDataPath, $"perf_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            try
            {
                File.WriteAllText(path, BuildCsv(stats), Encoding.UTF8);
                LastResultPath = path;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FrameTimeRecorder] No se pudo escribir {path}: {e.Message}");
                LastResultPath = null;
            }

            Debug.Log($"[FrameTimeRecorder] {stats.Frames} frames · {stats.AverageFps:0.0} fps de media · 1 % {stats.OnePercentLowFps:0.0} fps · " +
                      $"enemigos {Average(enemySum, sampleCount):0} (máx. {enemyMax}) · GC {gcBytesTotal} B en {gcFrames} frames → {LastResultPath}");
            DisposeRecorders();
            RecordingFinished?.Invoke(stats, LastResultPath);
        }

        // Separador ';' y punto decimal. Una fila de cabecera y una de datos.
        string BuildCsv(PerfStats stats)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("fecha;editor;cpu;gpu;ram_mb;resolucion;vsync;duracion_s;frames;fps_medio;fps_1pct;ms_medio;ms_max;" +
                          "enemigos_medio;enemigos_max;main_thread_ms_medio;physics2d_ms_medio;gc_bytes_total;gc_frames");
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm", c)).Append(';')
              .Append(Application.isEditor ? "si" : "no").Append(';')
              .Append(Clean(SystemInfo.processorType)).Append(';')
              .Append(Clean(SystemInfo.graphicsDeviceName)).Append(';')
              .Append(SystemInfo.systemMemorySize).Append(';')
              .Append(Screen.width).Append('x').Append(Screen.height).Append(';')
              .Append(QualitySettings.vSyncCount).Append(';')
              .Append(stats.TotalSeconds.ToString("0.00", c)).Append(';')
              .Append(stats.Frames).Append(';')
              .Append(stats.AverageFps.ToString("0.00", c)).Append(';')
              .Append(stats.OnePercentLowFps.ToString("0.00", c)).Append(';')
              .Append(stats.AverageFrameMs.ToString("0.000", c)).Append(';')
              .Append(stats.MaxFrameMs.ToString("0.000", c)).Append(';')
              .Append(Average(enemySum, sampleCount).ToString("0.0", c)).Append(';')
              .Append(enemyMax).Append(';')
              .Append(mainThread.Valid ? Average(mainThreadSumMs, profilerSamples).ToString("0.000", c) : "").Append(';')
              .Append(physics2D.Valid ? Average(physicsSumMs, profilerSamples).ToString("0.000", c) : "").Append(';')
              .Append(gcAlloc.Valid ? gcBytesTotal.ToString(c) : "").Append(';')
              .Append(gcAlloc.Valid ? gcFrames.ToString(c) : "")
              .AppendLine();
            return sb.ToString();
        }

        static double Average(double sum, int count) => count > 0 ? sum / count : 0.0;

        static string Clean(string text) => (text ?? string.Empty).Replace(';', ',').Trim();

        void DisposeRecorders()
        {
            if (mainThread.Valid) mainThread.Dispose();
            if (gcAlloc.Valid) gcAlloc.Dispose();
            if (physics2D.Valid) physics2D.Dispose();
        }
    }
}
