using UnityEngine;

namespace Cardstorm.CameraRig
{
    // Cálculos de la vista ortográfica, sin dependencias de escena.
    public static class CameraMath
    {
        // Rectángulo visible de una cámara ortográfica centrada en center.
        public static Rect ViewRect(Vector2 center, float orthoSize, float aspect)
        {
            var half = new Vector2(orthoSize * aspect, orthoSize);
            return new Rect(center - half, half * 2f);
        }

        // Limita el centro de la vista para que no salga de bounds. Si el mapa es más pequeño que la vista en un eje, centra en ese eje.
        public static Vector2 ClampToBounds(Vector2 desired, Vector2 halfExtents, Rect bounds)
        {
            return new Vector2(
                ClampAxis(desired.x, halfExtents.x, bounds.xMin, bounds.xMax),
                ClampAxis(desired.y, halfExtents.y, bounds.yMin, bounds.yMax));
        }

        static float ClampAxis(float value, float half, float min, float max)
        {
            if (max - min <= half * 2f)
                return (min + max) * 0.5f;
            return Mathf.Clamp(value, min + half, max - half);
        }
    }
}
