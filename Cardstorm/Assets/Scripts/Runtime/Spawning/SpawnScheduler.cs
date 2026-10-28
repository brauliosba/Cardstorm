using System;
using System.Collections.Generic;

namespace Cardstorm.Spawning
{
    public enum SpawnSource { Segment, Wave }

    // Petición de aparición: Count enemigos del tipo TypeIndex. El spawner decide las posiciones.
    public readonly struct SpawnRequest
    {
        public readonly int TypeIndex;
        public readonly int Count;
        public readonly float Reinforcement;
        public readonly SpawnSource Source;
        public readonly SpawnPattern Pattern;      // Solo en tramos
        public readonly WaveFormation Formation;   // Solo en oleadas
        public readonly int WaveIndex;             // -1 en tramos
        public readonly int PulseId;               // Mismo valor para todos los tipos de un pulso; -1 en tramos
        public readonly double Time;

        public SpawnRequest(int typeIndex, int count, float reinforcement, SpawnSource source,
            SpawnPattern pattern, WaveFormation formation, int waveIndex, int pulseId, double time)
        {
            TypeIndex = typeIndex;
            Count = count;
            Reinforcement = reinforcement;
            Source = source;
            Pattern = pattern;
            Formation = formation;
            WaveIndex = waveIndex;
            PulseId = pulseId;
            Time = time;
        }
    }

    // Planificador de apariciones, sin dependencias de escena. Se avanza con Tick(dt) usando el tiempo de juego.
    // - Tramos: cada tipo acumula ritmo × tiempo / 60; cada groupInterval sale un grupo con la parte entera
    //   y el resto se conserva (así los ritmos bajos salen exactos a la larga).
    // - Oleadas: los extras se reparten en pulsos equiespaciados; el resto de la división va en el primero.
    // La integración es exacta por intervalos, así que el resultado no depende de dt.
    public sealed class SpawnScheduler
    {
        const double Epsilon = 1e-6;

        readonly int typeCount;
        readonly double groupInterval;
        readonly IReadOnlyList<SpawnSegment> segments;
        readonly IReadOnlyList<WaveEvent> waves;
        readonly IReadOnlyList<BossEvent> bosses;
        readonly Action<SpawnRequest> onSpawn;

        readonly double[] accumulators;
        readonly List<Pulse> pulses = new();

        double nextGroupTime;
        int segmentIndex;
        int nextPulse;
        int nextBoss;
        int nextWave;

        readonly struct Pulse
        {
            public readonly double Time;
            public readonly int WaveIndex;
            public readonly int PulseIndex;
            public readonly int PulseId;

            public Pulse(double time, int waveIndex, int pulseIndex, int pulseId)
            {
                Time = time;
                WaveIndex = waveIndex;
                PulseIndex = pulseIndex;
                PulseId = pulseId;
            }
        }

        public event Action<BossEvent> BossTimeReached;

        public double Time { get; private set; }

        // Pausa de las apariciones (truco de depuración): el tiempo avanza, pero no se acumula ni se emite nada
        // y las oleadas que pasen mientras tanto se pierden.
        public bool SpawningPaused { get; set; }

        public int CurrentSegmentIndex => segmentIndex;
        public SpawnSegment CurrentSegment => segments.Count > 0 ? segments[segmentIndex] : null;
        public float CurrentReinforcement => CurrentSegment?.reinforcement ?? 1f;

        public WaveEvent NextWave => nextWave < waves.Count ? waves[nextWave] : null;
        public double TimeToNextWave => NextWave != null ? Math.Max(0.0, NextWave.startTime - Time) : double.PositiveInfinity;

        public BossEvent NextBoss => nextBoss < bosses.Count ? bosses[nextBoss] : null;

        public SpawnScheduler(SpawnCurve curve, Action<SpawnRequest> onSpawn)
            : this(curve.TypeCount, curve.groupInterval, curve.segments, curve.waves, curve.bosses, onSpawn)
        {
        }

        public SpawnScheduler(int typeCount, float groupInterval,
            IReadOnlyList<SpawnSegment> segments, IReadOnlyList<WaveEvent> waves, IReadOnlyList<BossEvent> bosses,
            Action<SpawnRequest> onSpawn)
        {
            this.typeCount = typeCount;
            this.groupInterval = groupInterval;
            this.segments = segments ?? Array.Empty<SpawnSegment>();
            this.waves = waves ?? Array.Empty<WaveEvent>();
            this.bosses = bosses ?? Array.Empty<BossEvent>();
            this.onSpawn = onSpawn;
            accumulators = new double[typeCount];

            int pulseId = 0;
            for (int w = 0; w < this.waves.Count; w++)
            {
                var wave = this.waves[w];
                int count = Math.Max(1, wave.pulses);
                for (int p = 0; p < count; p++)
                    pulses.Add(new Pulse(wave.startTime + p * (double)wave.duration / count, w, p, pulseId++));
            }
            pulses.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.PulseId.CompareTo(b.PulseId));

            Reset();
        }

        public void Reset() => JumpTo(0f);

        // Salta a un momento de la partida sin emitir lo que se ha saltado: reinicia los acumuladores
        // y da por pasadas las oleadas ya empezadas y los jefes anteriores. Lo que empieza justo en seconds sí sale.
        public void JumpTo(float seconds)
        {
            Time = Math.Max(0.0, seconds);
            Array.Clear(accumulators, 0, accumulators.Length);
            nextGroupTime = (Math.Floor(Time / groupInterval + Epsilon) + 1) * groupInterval;

            segmentIndex = 0;
            AdvanceSegment();

            nextWave = 0;
            while (nextWave < waves.Count && waves[nextWave].startTime < Time - Epsilon)
                nextWave++;
            nextPulse = 0;
            while (nextPulse < pulses.Count && waves[pulses[nextPulse].WaveIndex].startTime < Time - Epsilon)
                nextPulse++;
            nextBoss = 0;
            while (nextBoss < bosses.Count && bosses[nextBoss].time < Time - Epsilon)
                nextBoss++;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            double target = Time + deltaTime;

            while (true)
            {
                FireDueEvents();
                if (Time >= target - Epsilon) break;

                double next = Math.Min(target, nextGroupTime);
                if (segmentIndex < segments.Count - 1) next = Math.Min(next, segments[segmentIndex].endTime);
                if (nextPulse < pulses.Count) next = Math.Min(next, pulses[nextPulse].Time);
                if (nextBoss < bosses.Count) next = Math.Min(next, bosses[nextBoss].time);
                next = Math.Max(next, Time);

                Accumulate(next - Time);
                Time = next;
                AdvanceSegment();

                if (Time >= nextGroupTime - Epsilon)
                {
                    EmitGroups();
                    nextGroupTime += groupInterval;
                }
            }
            Time = target;
        }

        void AdvanceSegment()
        {
            while (segmentIndex < segments.Count - 1 && Time >= segments[segmentIndex].endTime - Epsilon)
                segmentIndex++;
        }

        void Accumulate(double seconds)
        {
            if (SpawningPaused || seconds <= 0.0 || segments.Count == 0) return;
            var rates = segments[segmentIndex].ratePerMinute;
            int n = Math.Min(typeCount, rates.Length);
            for (int i = 0; i < n; i++)
                accumulators[i] += rates[i] * seconds / 60.0;
        }

        void EmitGroups()
        {
            var segment = CurrentSegment;
            for (int i = 0; i < typeCount; i++)
            {
                int n = (int)Math.Floor(accumulators[i] + Epsilon);
                if (n < 1) continue;
                accumulators[i] = Math.Max(0.0, accumulators[i] - n);
                onSpawn?.Invoke(new SpawnRequest(i, n, segment?.reinforcement ?? 1f, SpawnSource.Segment,
                    segment?.pattern ?? SpawnPattern.ScreenEdgeGroups, default, -1, -1, nextGroupTime));
            }
        }

        void FireDueEvents()
        {
            while (nextPulse < pulses.Count && pulses[nextPulse].Time <= Time + Epsilon)
            {
                if (!SpawningPaused)
                    EmitPulse(pulses[nextPulse]);
                nextPulse++;
            }
            while (nextWave < waves.Count && waves[nextWave].startTime <= Time + Epsilon)
                nextWave++;
            while (nextBoss < bosses.Count && bosses[nextBoss].time <= Time + Epsilon)
            {
                BossTimeReached?.Invoke(bosses[nextBoss]);
                nextBoss++;
            }
        }

        void EmitPulse(Pulse pulse)
        {
            var wave = waves[pulse.WaveIndex];
            int pulseCount = Math.Max(1, wave.pulses);
            float reinforcement = CurrentReinforcement;
            int n = Math.Min(typeCount, wave.extraCount.Length);
            for (int i = 0; i < n; i++)
            {
                int total = wave.extraCount[i];
                int count = total / pulseCount + (pulse.PulseIndex == 0 ? total % pulseCount : 0);
                if (count < 1) continue;
                onSpawn?.Invoke(new SpawnRequest(i, count, reinforcement, SpawnSource.Wave,
                    default, wave.formation, pulse.WaveIndex, pulse.PulseId, pulse.Time));
            }
        }
    }
}
