using System.Collections.Generic;
using System.Linq;
using Cardstorm.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace Cardstorm.Tests
{
    public class FormationMathTests
    {
        // Vista de 32 x 18 centrada en (10, 5).
        static readonly Rect View = new(new Vector2(-6f, -4f), new Vector2(32f, 18f));
        const float Margin = 2f;
        const float Tolerance = 1e-4f;

        static bool OutsideView(Vector2 p) => !View.Contains(p);

        [Test]
        public void EdgePoint_IsOnTheSideAndPushedOutwards()
        {
            Assert.AreEqual(new Vector2(View.center.x, View.yMax + Margin), FormationMath.EdgePoint(View, ViewSide.Top, 0.5f, Margin));
            Assert.AreEqual(new Vector2(View.xMin, View.yMin - Margin), FormationMath.EdgePoint(View, ViewSide.Bottom, 0f, Margin));
            Assert.AreEqual(new Vector2(View.xMin - Margin, View.yMax), FormationMath.EdgePoint(View, ViewSide.Left, 1f, Margin));
            Assert.AreEqual(new Vector2(View.xMax + Margin, View.center.y), FormationMath.EdgePoint(View, ViewSide.Right, 0.5f, Margin));
        }

        [Test]
        public void Cluster_PutsAllPointsWithinSpread()
        {
            var points = new List<Vector2>();
            var center = new Vector2(3f, -2f);
            FormationMath.Cluster(center, 12, 1.5f, points);

            Assert.AreEqual(12, points.Count);
            Assert.IsTrue(points.All(p => Vector2.Distance(p, center) <= 1.5f + Tolerance));
            Assert.AreEqual(12, points.Distinct().Count());
        }

        [Test]
        public void Cluster_SingleEnemy_GoesToCenter()
        {
            var points = new List<Vector2>();
            FormationMath.Cluster(new Vector2(1f, 2f), 1, 1.5f, points);

            Assert.AreEqual(new Vector2(1f, 2f), points.Single());
        }

        [Test]
        public void Ring_EquallySpacedAtRequestedDistance()
        {
            var points = new List<Vector2>();
            var center = new Vector2(10f, 5f);
            FormationMath.Ring(center, 15, 19f, 0f, points);

            Assert.AreEqual(15, points.Count);
            foreach (var p in points)
                Assert.AreEqual(19f, Vector2.Distance(p, center), Tolerance);

            float expectedStep = 2f * Mathf.PI / 15f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i] - center, b = points[(i + 1) % points.Count] - center;
                Assert.AreEqual(expectedStep, Vector2.Angle(a, b) * Mathf.Deg2Rad, 1e-3f);
            }
        }

        [Test]
        public void RingRadius_IsHalfDiagonalPlusExtra_AndOutsideView()
        {
            float radius = FormationMath.RingRadius(View);

            Assert.AreEqual(Mathf.Sqrt(32f * 32f + 18f * 18f) * 0.5f + 1f, radius, Tolerance);
            var points = new List<Vector2>();
            FormationMath.Ring(View.center, 30, radius, 0.3f, points);
            Assert.IsTrue(points.All(OutsideView));
        }

        [Test]
        public void Pincer_HalfOnEachOppositeEdge()
        {
            var points = new List<Vector2>();
            FormationMath.Pincer(View, horizontal: true, 20, Margin, points);

            Assert.AreEqual(20, points.Count);
            Assert.AreEqual(10, points.Count(p => Mathf.Approximately(p.x, View.xMin - Margin)));
            Assert.AreEqual(10, points.Count(p => Mathf.Approximately(p.x, View.xMax + Margin)));

            FormationMath.Pincer(View, horizontal: false, 7, Margin, points);
            Assert.AreEqual(4, points.Count(p => Mathf.Approximately(p.y, View.yMax + Margin)));
            Assert.AreEqual(3, points.Count(p => Mathf.Approximately(p.y, View.yMin - Margin)));
        }

        [Test]
        public void EdgeLine_SpreadsAlongOneEdge()
        {
            var points = new List<Vector2>();
            FormationMath.EdgeLine(View, ViewSide.Left, 8, Margin, points);

            Assert.AreEqual(8, points.Count);
            Assert.IsTrue(points.All(p => Mathf.Approximately(p.x, View.xMin - Margin)));
            Assert.IsTrue(points.All(p => p.y > View.yMin && p.y < View.yMax));
            Assert.AreEqual(8, points.Select(p => p.y).Distinct().Count());
        }

        [Test]
        public void Column_IsDenseThreeWide_StartingAtFrontOffset()
        {
            var points = new List<Vector2>();
            FormationMath.Column(View, ViewSide.Right, 40, 3, 0.9f, Margin, 2.5f, points);

            Assert.AreEqual(40, points.Count);
            Assert.IsTrue(points.All(OutsideView));
            // Ancho: como mucho 3 posiciones distintas a lo largo del borde.
            Assert.LessOrEqual(points.Select(p => Mathf.Round(p.y * 1000f)).Distinct().Count(), 3);
            // La primera fila está a margen + adelanto del borde; el resto, detrás.
            float front = View.xMax + Margin + 2.5f;
            Assert.AreEqual(front, points.Min(p => p.x), Tolerance);
            Assert.AreEqual(front + 13 * 0.9f, points.Max(p => p.x), Tolerance);
        }

        [Test]
        public void Column_SingleUnitAtFront_IsCenteredOnTheEdge()
        {
            var points = new List<Vector2>();
            FormationMath.Column(View, ViewSide.Top, 1, 3, 0.9f, Margin, 0f, points);

            Assert.AreEqual(new Vector2(View.center.x, View.yMax + Margin), points.Single());
        }

        [Test]
        public void Opposite_AndOutward()
        {
            Assert.AreEqual(ViewSide.Bottom, FormationMath.Opposite(ViewSide.Top));
            Assert.AreEqual(ViewSide.Left, FormationMath.Opposite(ViewSide.Right));
            Assert.AreEqual(Vector2.right, FormationMath.Outward(ViewSide.Right));
            Assert.AreEqual(Vector2.down, FormationMath.Outward(ViewSide.Bottom));
        }

        [Test]
        public void MoveInside_KeepsRadiusFromTheBorder()
        {
            var bounds = new Rect(-60f, -60f, 120f, 120f);

            Assert.AreEqual(new Vector2(59.5f, -59.5f), FormationMath.MoveInside(bounds, new Vector2(70f, -80f), 0.5f));
            Assert.AreEqual(new Vector2(3f, 4f), FormationMath.MoveInside(bounds, new Vector2(3f, 4f), 0.5f));
        }
    }
}
