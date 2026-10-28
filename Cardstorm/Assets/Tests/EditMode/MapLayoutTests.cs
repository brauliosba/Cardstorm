using Cardstorm.Core;
using NUnit.Framework;
using UnityEngine;

namespace Cardstorm.Tests
{
    public class MapLayoutTests
    {
        static readonly Rect Map = new Rect(-60f, -60f, 120f, 120f);

        [Test]
        public void GenerateObstacles_SameSeed_SameLayout()
        {
            var a = MapLayout.GenerateObstacles(MapLayout.DefaultSettings(Map));
            var b = MapLayout.GenerateObstacles(MapLayout.DefaultSettings(Map));

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].Size, b[i].Size);
            }
        }

        [Test]
        public void GenerateObstacles_RespectsCountAndSpacingRules()
        {
            var settings = MapLayout.DefaultSettings(Map);
            var obstacles = MapLayout.GenerateObstacles(settings);

            Assert.AreEqual(settings.Count, obstacles.Count);
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                Assert.GreaterOrEqual(o.Position.magnitude - o.BoundingRadius, settings.CenterClearance - 1e-3f, "Demasiado cerca del centro");

                Rect inner = new Rect(Map.xMin + o.BoundingRadius, Map.yMin + o.BoundingRadius,
                    Map.width - 2f * o.BoundingRadius, Map.height - 2f * o.BoundingRadius);
                Assert.IsTrue(inner.Contains(o.Position), "Fuera del mapa");

                for (int j = i + 1; j < obstacles.Count; j++)
                {
                    var p = obstacles[j];
                    float gap = Vector2.Distance(o.Position, p.Position) - o.BoundingRadius - p.BoundingRadius;
                    Assert.GreaterOrEqual(gap, settings.MinGap - 1e-3f, $"Obstáculos {i} y {j} demasiado juntos");
                }
            }
        }

        [Test]
        public void GenerateObstacles_HasRocksAndColumns()
        {
            var obstacles = MapLayout.GenerateObstacles(MapLayout.DefaultSettings(Map));

            Assert.IsTrue(obstacles.Exists(o => o.Kind == MapLayout.ObstacleKind.Rock));
            Assert.IsTrue(obstacles.Exists(o => o.Kind == MapLayout.ObstacleKind.Column));
        }
    }
}
