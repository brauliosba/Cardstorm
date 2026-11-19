using UnityEngine;

namespace Cardstorm.Visuals
{
    // Cálculos de la animación sin fotogramas: rebote y balanceo al andar, respiración y aplastamiento.
    public static class SpriteMotionMath
    {
        // 0 quieto, 1 a la velocidad de referencia o más.
        public static float MoveAmount(float speed, float referenceSpeed) =>
            referenceSpeed <= 0f ? 0f : Mathf.Clamp01(speed / referenceSpeed);

        // Cada paso es media vuelta de la fase: avanza con la distancia, así los pasos siguen al movimiento.
        public static float AdvancePhase(float phase, float distance, float stepLength)
        {
            if (stepLength <= 0f || distance <= 0f) return phase;
            return (phase + distance / stepLength * Mathf.PI) % (2f * Mathf.PI);
        }

        // Un salto por paso, nunca por debajo del suelo.
        public static float Bob(float phase, float height, float move) =>
            Mathf.Abs(Mathf.Sin(phase)) * height * move;

        // Inclinación en grados: un paso a cada lado.
        public static float Tilt(float phase, float angle, float move) =>
            Mathf.Sin(phase) * angle * move;

        // Escala vertical en reposo. Andando no respira.
        public static float Breathe(float time, float amount, float rate, float move) =>
            1f + Mathf.Sin(time * rate * 2f * Mathf.PI) * amount * (1f - move);

        // Aplastamiento tras un golpe: empieza en strength y se apaga en duration.
        public static float Squash(float elapsed, float duration, float strength)
        {
            if (duration <= 0f || elapsed >= duration || elapsed < 0f) return 0f;
            float t = 1f - elapsed / duration;
            return strength * t * t;
        }

        // Altura al flotar: sube y baja sin tocar el suelo.
        public static float Hover(float time, float height, float rate) =>
            (Mathf.Sin(time * rate * 2f * Mathf.PI) + 1f) * 0.5f * height;

        // Al flotar se inclina hacia donde va, en grados (negativo = hacia la derecha).
        public static float Lean(float velocityX, float referenceSpeed, float angle) =>
            referenceSpeed <= 0f ? 0f : -Mathf.Clamp(velocityX / referenceSpeed, -1f, 1f) * angle;

        // Mira a la izquierda si va a la izquierda. Con poca velocidad horizontal no cambia.
        public static bool FacingLeft(bool current, float velocityX, float deadZone)
        {
            if (velocityX > deadZone) return false;
            if (velocityX < -deadZone) return true;
            return current;
        }
    }
}
