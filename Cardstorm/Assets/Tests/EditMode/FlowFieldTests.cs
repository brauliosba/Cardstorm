using Cardstorm.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace Cardstorm.Tests
{
    public class FlowFieldTests
    {
        // Rejilla de 21 x 21 celdas de 1 u centrada en el origen.
        static FlowField CreateField() => new(new Vector2(-10.5f, -10.5f), 21, 21, 1f);

        // Muro vertical en x = 0 desde y = -5 hasta y = 5.
        static void AddWall(FlowField field)
        {
            field.WorldToCell(Vector2.zero, out int wx, out int wy);
            for (int y = wy - 5; y <= wy + 5; y++)
                field.SetBlocked(wx, y, true);
        }

        [Test]
        public void OpenField_PointsDirectlyToTarget()
        {
            var field = CreateField();
            field.Build(Vector2.zero);

            Vector2 dir = field.GetDirection(new Vector2(5f, 5f), Vector2.zero);

            Assert.AreEqual(1f, dir.magnitude, 1e-3f);
            Assert.Greater(Vector2.Dot(dir, new Vector2(-1f, -1f).normalized), 0.999f);
        }

        [Test]
        public void Wall_DirectionDoesNotPointIntoWall()
        {
            var field = CreateField();
            AddWall(field);
            var target = new Vector2(-5f, 0f);
            field.Build(target);

            Vector2 dir = field.GetDirection(new Vector2(3f, 0f), target);

            Assert.Greater(Mathf.Abs(dir.y), 0.3f, "Debería desviarse para rodear el muro");
        }

        [Test]
        public void Wall_BehindWall_HeadsTowardsNearestEndEarly()
        {
            var field = CreateField();
            AddWall(field);
            var target = new Vector2(-5f, 0f);
            field.Build(target);

            // Lejos del muro y algo por encima del eje: debe ir ya hacia el extremo superior, no recto contra el muro.
            Vector2 dir = field.GetDirection(new Vector2(6f, 2f), target);

            Assert.Greater(dir.y, 0.3f);
            Assert.Less(dir.x, 0f);
        }

        [Test]
        public void Wall_CellsInShadowHaveNoLineOfSight_OthersDo()
        {
            var field = CreateField();
            AddWall(field);
            var target = new Vector2(-5f, 0f);
            field.Build(target);

            field.WorldToCell(new Vector2(5f, 0f), out int sx, out int sy);
            field.WorldToCell(new Vector2(-5f, 8f), out int vx, out int vy);
            Assert.IsFalse(field.HasLineOfSight(sx, sy));
            Assert.IsTrue(field.HasLineOfSight(vx, vy));
        }

        [Test]
        public void Wall_FollowingTheFieldReachesTarget()
        {
            var field = CreateField();
            AddWall(field);
            var target = new Vector2(-5f, 0f);
            field.Build(target);

            Vector2 pos = new(5f, 0f);
            for (int i = 0; i < 500 && Vector2.Distance(pos, target) > 0.5f; i++)
            {
                pos += field.GetDirection(pos, target) * 0.1f;
                field.WorldToCell(pos, out int x, out int y);
                Assert.IsFalse(field.IsBlocked(x, y), $"Ha entrado en una celda bloqueada en {pos}");
            }

            Assert.LessOrEqual(Vector2.Distance(pos, target), 0.5f, "No ha llegado al objetivo");
        }

        [Test]
        public void UnreachableCell_FallsBackToDirectDirection()
        {
            var field = CreateField();
            // Encierra la celda (5, 5) en un anillo de celdas bloqueadas.
            field.WorldToCell(new Vector2(5f, 5f), out int cx, out int cy);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0) field.SetBlocked(cx + dx, cy + dy, true);
            field.Build(Vector2.zero);

            Vector2 dir = field.GetDirection(new Vector2(5f, 5f), Vector2.zero);

            Assert.Greater(Vector2.Dot(dir, new Vector2(-1f, -1f).normalized), 0.999f);
        }

        [Test]
        public void TargetInsideBlockedCell_StillBuildsField()
        {
            var field = CreateField();
            field.WorldToCell(Vector2.zero, out int tx, out int ty);
            field.SetBlocked(tx, ty, true);
            field.Build(Vector2.zero);

            field.WorldToCell(new Vector2(4f, 0f), out int x, out int y);
            Assert.IsTrue(field.IsReachable(x, y));
        }

        [Test]
        public void BeyondMaxDistance_IsUnreachableAndGoesDirect()
        {
            var field = CreateField();
            AddWall(field);
            field.MaxDistance = 4f;
            var target = new Vector2(-5f, 0f);
            field.Build(target);

            field.WorldToCell(new Vector2(5f, 0f), out int x, out int y);
            Assert.IsFalse(field.IsReachable(x, y));
            Assert.Greater(Vector2.Dot(field.GetDirection(new Vector2(5f, 0f), target), Vector2.left), 0.999f);
        }

        [Test]
        public void OutsideGrid_FallsBackToDirectDirection()
        {
            var field = CreateField();
            field.Build(Vector2.zero);

            Vector2 dir = field.GetDirection(new Vector2(50f, 0f), Vector2.zero);

            Assert.Greater(Vector2.Dot(dir, Vector2.left), 0.999f);
        }
    }
}
