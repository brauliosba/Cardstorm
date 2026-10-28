using Cardstorm.CameraRig;
using NUnit.Framework;
using UnityEngine;

namespace Cardstorm.Tests
{
    public class CameraMathTests
    {
        static readonly Rect Map = new Rect(-60f, -60f, 120f, 120f);
        const float Tolerance = 1e-4f;

        [Test]
        public void ViewRect_IsCenteredWithOrthoSizeAndAspect()
        {
            Rect view = CameraMath.ViewRect(new Vector2(10f, 5f), 9f, 16f / 9f);

            Assert.AreEqual(32f, view.width, Tolerance);
            Assert.AreEqual(18f, view.height, Tolerance);
            Assert.AreEqual(10f, view.center.x, Tolerance);
            Assert.AreEqual(5f, view.center.y, Tolerance);
        }

        [Test]
        public void ClampToBounds_InCenter_DoesNotChange()
        {
            Vector2 result = CameraMath.ClampToBounds(new Vector2(3f, -4f), new Vector2(16f, 9f), Map);

            Assert.AreEqual(3f, result.x, Tolerance);
            Assert.AreEqual(-4f, result.y, Tolerance);
        }

        [Test]
        public void ClampToBounds_InCorner_KeepsViewInsideMap()
        {
            var half = new Vector2(16f, 9f);
            Vector2 result = CameraMath.ClampToBounds(new Vector2(59f, -59f), half, Map);

            Assert.AreEqual(60f - 16f, result.x, Tolerance);
            Assert.AreEqual(-60f + 9f, result.y, Tolerance);
            Rect view = new Rect(result - half, half * 2f);
            Assert.GreaterOrEqual(view.xMin, Map.xMin - Tolerance);
            Assert.LessOrEqual(view.xMax, Map.xMax + Tolerance);
            Assert.GreaterOrEqual(view.yMin, Map.yMin - Tolerance);
            Assert.LessOrEqual(view.yMax, Map.yMax + Tolerance);
        }

        [Test]
        public void ClampToBounds_MapSmallerThanView_CentersOnThatAxis()
        {
            var narrowMap = new Rect(-10f, -60f, 20f, 120f);
            Vector2 result = CameraMath.ClampToBounds(new Vector2(8f, 30f), new Vector2(16f, 9f), narrowMap);

            Assert.AreEqual(0f, result.x, Tolerance);
            Assert.AreEqual(30f, result.y, Tolerance);
        }
    }
}
