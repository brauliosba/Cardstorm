using System;
using System.Collections.Generic;
using System.Linq;
using Cardstorm.Spawning;
using NUnit.Framework;

namespace Cardstorm.Tests
{
    public class SpawnSchedulerTests
    {
        const int Rondador = 0, Corredor = 1, Tirador = 2, Coloso = 3;

        // Recuento esperado por minuto y tipo (Rondador, Corredor, Tirador, Coloso), calculado con este algoritmo.
        static readonly int[,] ExpectedPerMinute =
        {
            {  40,  0,  0, 0 }, {  54,  0,  0, 0 }, {  69, 10,  0, 0 }, {  58,  7,  0, 0 }, {  60, 10,  8, 0 },
            {  40,  6,  4, 0 }, {  54, 17,  6, 1 }, {  72, 28,  9, 2 }, {  58, 40, 16, 1 }, {  88, 38, 12, 3 },
            {  88, 38, 12, 3 }, { 111, 28, 10, 4 }, { 112, 50, 18, 4 }, { 112, 50, 18, 4 }, {  86, 33, 13, 3 },
        };

        // Totales por minuto de la Tabla 27 de la memoria (se admite ±1 por redondeo).
        static readonly int[] Table27 = { 40, 54, 79, 66, 78, 50, 78, 111, 115, 141, 141, 152, 184, 184, 135 };

        static SpawnScheduler CreateScheduler(List<SpawnRequest> requests, List<BossEvent> bosses = null)
        {
            var scheduler = new SpawnScheduler(SpawnCurveDefaults.TypeCount, SpawnCurveDefaults.GroupInterval,
                SpawnCurveDefaults.Segments(), SpawnCurveDefaults.Waves(), SpawnCurveDefaults.Bosses(), requests.Add);
            if (bosses != null) scheduler.BossTimeReached += bosses.Add;
            return scheduler;
        }

        static List<SpawnRequest> Simulate(float dt = 0.02f, float duration = 900f)
        {
            var requests = new List<SpawnRequest>();
            var scheduler = CreateScheduler(requests);
            TickUntil(scheduler, duration, dt);
            return requests;
        }

        static void TickUntil(SpawnScheduler scheduler, double until, float dt)
        {
            while (scheduler.Time < until - 1e-6)
                scheduler.Tick((float)Math.Min(dt, until - scheduler.Time));
        }

        // Un grupo de tramo cuenta en el minuto en el que se acumuló (el que sale a los 60 s cierra el minuto 1);
        // un pulso de oleada, en el minuto en el que sale.
        static int MinuteOf(SpawnRequest r)
        {
            int minute = r.Source == SpawnSource.Segment
                ? (int)Math.Ceiling(r.Time / 60.0 - 1e-9) - 1
                : (int)Math.Floor(r.Time / 60.0 + 1e-9);
            return Math.Clamp(minute, 0, 14);
        }

        static int[,] CountPerMinute(List<SpawnRequest> requests)
        {
            var counts = new int[15, 4];
            foreach (var r in requests)
                counts[MinuteOf(r), r.TypeIndex] += r.Count;
            return counts;
        }

        [Test]
        public void PerMinuteCounts_MatchExpectedTable_AndTable27()
        {
            var counts = CountPerMinute(Simulate());

            for (int m = 0; m < 15; m++)
            {
                int total = 0;
                for (int t = 0; t < 4; t++)
                {
                    Assert.AreEqual(ExpectedPerMinute[m, t], counts[m, t], $"Minuto {m + 1}, tipo {t}");
                    total += counts[m, t];
                }
                Assert.That(total, Is.InRange(Table27[m] - 1, Table27[m] + 1), $"Minuto {m + 1} frente a la Tabla 27");
            }
        }

        [TestCase(0.02f)]
        [TestCase(1f / 60f)]
        [TestCase(0.1f)]
        public void Totals_DoNotDependOnDeltaTime(float dt)
        {
            var requests = Simulate(dt);

            Assert.AreEqual(1102, requests.Where(r => r.TypeIndex == Rondador).Sum(r => r.Count));
            Assert.AreEqual(355, requests.Where(r => r.TypeIndex == Corredor).Sum(r => r.Count));
            Assert.AreEqual(126, requests.Where(r => r.TypeIndex == Tirador).Sum(r => r.Count));
            Assert.AreEqual(25, requests.Where(r => r.TypeIndex == Coloso).Sum(r => r.Count));
        }

        [Test]
        public void SegmentGroups_ArriveEveryGroupInterval_OneRequestPerType()
        {
            var requests = Simulate(duration: 60f);

            Assert.IsTrue(requests.All(r => r.Source == SpawnSource.Segment && r.TypeIndex == Rondador));
            foreach (var r in requests)
                Assert.AreEqual(0.0, Math.IEEERemainder(r.Time, SpawnCurveDefaults.GroupInterval), 1e-6);
            Assert.AreEqual(40, requests.Sum(r => r.Count));
        }

        [Test]
        public void NoSpawnsDuringCalm()
        {
            var requests = Simulate();

            Assert.IsFalse(requests.Any(r => r.Source == SpawnSource.Segment && r.Time > 870.0 + 1e-6));
        }

        [TestCase(100f, 1.0f)]
        [TestCase(250f, 1.1f)]
        [TestCase(400f, 1.2f)]
        [TestCase(600f, 1.3f)]
        [TestCase(800f, 1.4f)]
        public void ReinforcementBySegment(float time, float expected)
        {
            var requests = new List<SpawnRequest>();
            var scheduler = CreateScheduler(requests);
            TickUntil(scheduler, time, 0.02f);

            Assert.AreEqual(expected, scheduler.CurrentReinforcement, 1e-5f);
            var last = requests.Last(r => r.Source == SpawnSource.Segment);
            Assert.AreEqual(expected, last.Reinforcement, 1e-5f);
        }

        [Test]
        public void WavesFireOnce_WithTwoPulses_RemainderInFirstPulse()
        {
            var requests = Simulate();
            var waves = SpawnCurveDefaults.Waves();

            for (int w = 0; w < waves.Count; w++)
            {
                var waveRequests = requests.Where(r => r.Source == SpawnSource.Wave && r.WaveIndex == w).ToList();
                var times = waveRequests.Select(r => r.Time).Distinct().OrderBy(t => t).ToList();
                CollectionAssert.AreEqual(new[] { (double)waves[w].startTime, waves[w].startTime + 15.0 }, times, $"Oleada {w}");
                Assert.IsTrue(waveRequests.All(r => r.Formation == waves[w].formation));

                for (int t = 0; t < 4; t++)
                    Assert.AreEqual(waves[w].extraCount[t], waveRequests.Where(r => r.TypeIndex == t).Sum(r => r.Count), $"Oleada {w}, tipo {t}");
            }

            // Estampida: el coloso único va en el primer pulso.
            var colossus = requests.Single(r => r.Source == SpawnSource.Wave && r.WaveIndex == 2 && r.TypeIndex == Coloso);
            Assert.AreEqual(660.0, colossus.Time, 1e-6);
            Assert.AreEqual(1, colossus.Count);
        }

        [Test]
        public void PulseIds_GroupTheTypesOfOnePulse()
        {
            var requests = Simulate().Where(r => r.Source == SpawnSource.Wave).ToList();

            Assert.AreEqual(8, requests.Select(r => r.PulseId).Distinct().Count());
            foreach (var group in requests.GroupBy(r => r.PulseId))
                Assert.AreEqual(1, group.Select(r => r.Time).Distinct().Count());
        }

        [Test]
        public void JumpTo_DoesNotReplayPastWaves_NorAccumulatedSegments()
        {
            var requests = new List<SpawnRequest>();
            var scheduler = CreateScheduler(requests);
            scheduler.JumpTo(500f);
            TickUntil(scheduler, 900f, 0.02f);

            var waveIndices = requests.Where(r => r.Source == SpawnSource.Wave).Select(r => r.WaveIndex).Distinct().OrderBy(i => i);
            CollectionAssert.AreEqual(new[] { 2, 3 }, waveIndices);
            // El primer grupo tras el salto solo trae lo acumulado desde el salto (72 rondadores/min → 3 en 2,5 s).
            var first = requests.Where(r => r.Source == SpawnSource.Segment && r.TypeIndex == Rondador).First();
            Assert.LessOrEqual(first.Count, 3);
        }

        [Test]
        public void JumpTo_ExactWaveStart_StillFiresThatWave()
        {
            var requests = new List<SpawnRequest>();
            var scheduler = CreateScheduler(requests);
            scheduler.JumpTo(165f);
            scheduler.Tick(0.02f);

            Assert.IsTrue(requests.Any(r => r.Source == SpawnSource.Wave && r.WaveIndex == 0));
        }

        [Test]
        public void ZeroDeltaTime_EmitsNothing()
        {
            var requests = new List<SpawnRequest>();
            var scheduler = CreateScheduler(requests);
            TickUntil(scheduler, 100f, 0.02f);
            int before = requests.Count;

            for (int i = 0; i < 1000; i++)
                scheduler.Tick(0f);

            Assert.AreEqual(before, requests.Count);
            Assert.AreEqual(100.0, scheduler.Time, 1e-6);
        }

        [Test]
        public void SpawningPaused_EmitsNothing_ButTimeAdvances()
        {
            var requests = new List<SpawnRequest>();
            var scheduler = CreateScheduler(requests);
            scheduler.SpawningPaused = true;
            TickUntil(scheduler, 200f, 0.02f);

            CollectionAssert.IsEmpty(requests);
            Assert.AreEqual(200.0, scheduler.Time, 1e-6);

            // Al reanudar no sale de golpe lo que se ha saltado.
            scheduler.SpawningPaused = false;
            TickUntil(scheduler, 202.5f, 0.02f);
            Assert.LessOrEqual(requests.Sum(r => r.Count), 3);
        }

        [Test]
        public void Bosses_FireOnceAtTheirTime()
        {
            var requests = new List<SpawnRequest>();
            var bosses = new List<BossEvent>();
            var scheduler = CreateScheduler(requests, bosses);
            TickUntil(scheduler, 299.9f, 0.02f);
            Assert.IsEmpty(bosses);

            TickUntil(scheduler, 950f, 0.02f);
            CollectionAssert.AreEqual(new[] { "Heraldo", "Ojo de la tormenta" }, bosses.Select(b => b.name));
        }

        [Test]
        public void NextWave_ReportsUpcomingWaveAndTimeLeft()
        {
            var requests = new List<SpawnRequest>();
            var scheduler = CreateScheduler(requests);
            TickUntil(scheduler, 100f, 0.02f);

            Assert.AreEqual("Oleada 1: Cerco", scheduler.NextWave.name);
            Assert.AreEqual(65.0, scheduler.TimeToNextWave, 1e-4);

            TickUntil(scheduler, 870f, 0.02f);
            Assert.IsNull(scheduler.NextWave);
        }
    }
}
