using System;

namespace Cardstorm.Progression
{
    public enum GoldSource { Enemy, Chest, Boss, Debug }

    public enum SpendReason { Buy, Refresh, Remove }

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

        // Tiempo, nivel de la tienda.
        public static event Action<double, int> ShopOffered;

        // Tiempo, id de la carta, precio pagado.
        public static event Action<double, string, int> CardBought;

        // Tiempo, precio pagado.
        public static event Action<double, int> ShopRefreshed;

        // Tiempo, id de la carta, precio pagado.
        public static event Action<double, string, int> CardRemoved;

        // Tiempo, oro gastado, motivo.
        public static event Action<double, int, SpendReason> GoldSpent;

        // Tiempo, id del jefe.
        public static event Action<double, string> BossSpawned;

        // Tiempo, id del jefe.
        public static event Action<double, string> BossKilled;

        // Tiempo, nivel alcanzado.
        public static event Action<double, int> RunWon;

        public static void RaiseXpGained(double time, float xp) => XpGained?.Invoke(time, xp);

        public static void RaiseGoldGained(double time, int gold, GoldSource source) => GoldGained?.Invoke(time, gold, source);

        public static void RaiseLevelUp(double time, int level) => LevelUp?.Invoke(time, level);

        public static void RaiseShopOffered(double time, int level) => ShopOffered?.Invoke(time, level);

        public static void RaiseCardBought(double time, string cardId, int price) => CardBought?.Invoke(time, cardId, price);

        public static void RaiseShopRefreshed(double time, int price) => ShopRefreshed?.Invoke(time, price);

        public static void RaiseCardRemoved(double time, string cardId, int price) => CardRemoved?.Invoke(time, cardId, price);

        public static void RaiseGoldSpent(double time, int amount, SpendReason reason) => GoldSpent?.Invoke(time, amount, reason);

        public static void RaiseBossSpawned(double time, string bossId) => BossSpawned?.Invoke(time, bossId);

        public static void RaiseBossKilled(double time, string bossId) => BossKilled?.Invoke(time, bossId);

        public static void RaiseRunWon(double time, int level) => RunWon?.Invoke(time, level);
    }
}
