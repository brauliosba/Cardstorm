using System.Collections.Generic;
using Cardstorm.Spawning;
using NUnit.Framework;

namespace Cardstorm.Tests
{
    public class SpawnCurveTests
    {
        const int Types = 4;

        static SpawnSegment Segment(float start, float end, float reinforcement = 1f) => new()
        {
            name = $"{start}-{end}",
            startTime = start,
            endTime = end,
            ratePerMinute = new float[Types],
            reinforcement = reinforcement,
        };

        static WaveEvent Wave(float start) => new()
        {
            name = $"Oleada {start}",
            startTime = start,
            extraCount = new int[Types],
        };

        static List<string> Validate(List<SpawnSegment> segments, List<WaveEvent> waves = null) =>
            SpawnCurve.Validate(Types, 2.5f, segments, waves ?? new List<WaveEvent>());

        [Test]
        public void Validate_ValidCurve_NoIssues()
        {
            var issues = Validate(
                new List<SpawnSegment> { Segment(0, 60), Segment(60, 120), Segment(120, 180) },
                new List<WaveEvent> { Wave(30), Wave(150) });

            CollectionAssert.IsEmpty(issues);
        }

        [Test]
        public void Validate_DetectsGap()
        {
            var issues = Validate(new List<SpawnSegment> { Segment(0, 60), Segment(70, 120) });

            Assert.IsTrue(issues.Exists(i => i.Contains("hueco")), string.Join("\n", issues));
        }

        [Test]
        public void Validate_DetectsOverlap()
        {
            var issues = Validate(new List<SpawnSegment> { Segment(0, 60), Segment(50, 120) });

            Assert.IsTrue(issues.Exists(i => i.Contains("solapa")), string.Join("\n", issues));
        }

        [Test]
        public void Validate_DetectsFirstSegmentNotAtZero()
        {
            var issues = Validate(new List<SpawnSegment> { Segment(5, 60) });

            Assert.IsNotEmpty(issues);
        }

        [Test]
        public void Validate_DetectsEmptyOrInvertedSegment()
        {
            var issues = Validate(new List<SpawnSegment> { Segment(0, 60), Segment(60, 60) });

            Assert.IsNotEmpty(issues);
        }

        [Test]
        public void Validate_DetectsArrayLengthMismatch()
        {
            var segment = Segment(0, 60);
            segment.ratePerMinute = new float[Types - 1];
            var wave = Wave(30);
            wave.extraCount = new int[Types + 1];

            var issues = Validate(new List<SpawnSegment> { segment }, new List<WaveEvent> { wave });

            Assert.AreEqual(2, issues.Count, string.Join("\n", issues));
        }

        [Test]
        public void Validate_DetectsUnsortedWavesAndWavesOutsideTheMatch()
        {
            var issues = Validate(
                new List<SpawnSegment> { Segment(0, 60) },
                new List<WaveEvent> { Wave(40), Wave(20), Wave(90) });

            Assert.AreEqual(2, issues.Count, string.Join("\n", issues));
        }

        [Test]
        public void Defaults_AreValidAndCoverFifteenMinutes()
        {
            var segments = SpawnCurveDefaults.Segments();
            var waves = SpawnCurveDefaults.Waves();

            CollectionAssert.IsEmpty(SpawnCurve.Validate(SpawnCurveDefaults.TypeCount, SpawnCurveDefaults.GroupInterval, segments, waves));
            Assert.AreEqual(17, segments.Count);
            Assert.AreEqual(4, waves.Count);
            Assert.AreEqual(900f, segments[^1].endTime);
            Assert.AreEqual(2, SpawnCurveDefaults.Bosses().Count);
        }

        [Test]
        public void Defaults_ReturnIndependentCopies()
        {
            var a = SpawnCurveDefaults.Segments();
            a[0].ratePerMinute[0] = 999f;

            Assert.AreEqual(40f, SpawnCurveDefaults.Segments()[0].ratePerMinute[0]);
        }

        [Test]
        public void SegmentIndexAt_FindsSegmentAndClampsAtTheEnd()
        {
            var segments = new List<SpawnSegment> { Segment(0, 60), Segment(60, 120) };

            Assert.AreEqual(0, SpawnCurve.SegmentIndexAt(segments, 0f));
            Assert.AreEqual(0, SpawnCurve.SegmentIndexAt(segments, 59.99f));
            Assert.AreEqual(1, SpawnCurve.SegmentIndexAt(segments, 60f));
            Assert.AreEqual(1, SpawnCurve.SegmentIndexAt(segments, 500f));
        }
    }
}
