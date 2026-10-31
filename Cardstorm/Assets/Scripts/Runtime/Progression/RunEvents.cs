using System;

namespace Cardstorm.Progression
{
    public enum GoldSource { Enemy, Chest, Boss }

    // Eventos de la partida para la interfaz y la telemetría. Todos llevan los segundos de partida.
    // Los suscriptores se dan de baja en su OnDisable: los eventos estáticos sobreviven a la escena.
    public static class RunEvents
    {
        // Tiempo, experiencia sumada (ya multiplicada).
        public static event Action<double, float> XpGained;

        // Tiempo, oro sumado (ya multiplicado), origen.
        public static event Action<double, int, GoldSource> GoldGained;

        // Tiempo, nivel alcanzado. Una llamada por nivel aunque se suban varios de golpe.
        public static event Action<double, int> LevelUp;

        public static void RaiseXpGained(double time, float xp) => XpGained?.Invoke(time, xp);

        public static void RaiseGoldGained(double time, int gold, GoldSource source) => GoldGained?.Invoke(time, gold, source);

        public static void RaiseLevelUp(double time, int level) => LevelUp?.Invoke(time, level);
    }
}
