using System;
using System.Collections.Generic;

namespace Cardstorm.Progression
{
    public enum GoldSource { Enemy, Chest, Boss, Debug }

    public enum SpendReason { Buy, Refresh, Remove, GemRefresh }

    public enum LevelUpOption { Shop, Gem }

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

        // Tiempo, nivel de las gemas ofrecidas.
        public static event Action<double, int> GemsOffered;

        // Tiempo, precio pagado.
        public static event Action<double, int> GemsRefreshed;

        // Tiempo, id de la gema, id de la carta, id de la gema que tenía (null si no tenía).
        public static event Action<double, string, string, string> GemApplied;

        // Tiempo, nivel, opción elegida en la subida.
        public static event Action<double, int, LevelUpOption> LevelUpChoice;

        // Tiempo, id de la carta que Resonancia ha lanzado otra vez, id de su gema.
        public static event Action<double, string, string> CardRepeated;

        // Tiempo, ids de las cartas sorteadas en la tienda (null en las ranuras vacías). La lista se reutiliza: copiarla si se guarda.
        public static event Action<double, IReadOnlyList<string>> CardsRolled;

        // Tiempo, ids de las gemas sorteadas. La lista se reutiliza.
        public static event Action<double, IReadOnlyList<string>> GemsRolled;

        // Tiempo, tecla de depuración usada.
        public static event Action<double, string> CheatUsed;

        // Tiempo, oro gastado, motivo.
        public static event Action<double, int, SpendReason> GoldSpent;

        // Tiempo, id del jefe.
        public static event Action<double, string> BossSpawned;

        // Tiempo, id del jefe.
        public static event Action<double, string> BossKilled;

        // Tiempo, id del jefe. Se acabó su tiempo y huyó sin dar nada.
        public static event Action<double, string> BossRetreated;

        // Tiempo, id de la carta rara elegida tras derrotar a un jefe.
        public static event Action<double, string> RareCardTaken;

        // Tiempo, nivel alcanzado.
        public static event Action<double, int> RunWon;

        public static void RaiseXpGained(double time, float xp) => XpGained?.Invoke(time, xp);

        public static void RaiseGoldGained(double time, int gold, GoldSource source) => GoldGained?.Invoke(time, gold, source);

        public static void RaiseLevelUp(double time, int level) => LevelUp?.Invoke(time, level);

        public static void RaiseShopOffered(double time, int level) => ShopOffered?.Invoke(time, level);

        public static void RaiseCardBought(double time, string cardId, int price) => CardBought?.Invoke(time, cardId, price);

        public static void RaiseShopRefreshed(double time, int price) => ShopRefreshed?.Invoke(time, price);

        public static void RaiseCardRemoved(double time, string cardId, int price) => CardRemoved?.Invoke(time, cardId, price);

        public static void RaiseGemsOffered(double time, int level) => GemsOffered?.Invoke(time, level);

        public static void RaiseGemsRefreshed(double time, int price) => GemsRefreshed?.Invoke(time, price);

        public static void RaiseGemApplied(double time, string gemId, string cardId, string previousGemId) =>
            GemApplied?.Invoke(time, gemId, cardId, previousGemId);

        public static void RaiseLevelUpChoice(double time, int level, LevelUpOption option) =>
            LevelUpChoice?.Invoke(time, level, option);

        public static void RaiseCardRepeated(double time, string cardId, string gemId) => CardRepeated?.Invoke(time, cardId, gemId);

        public static void RaiseCardsRolled(double time, IReadOnlyList<string> ids) => CardsRolled?.Invoke(time, ids);

        public static void RaiseGemsRolled(double time, IReadOnlyList<string> ids) => GemsRolled?.Invoke(time, ids);

        public static void RaiseCheatUsed(double time, string key) => CheatUsed?.Invoke(time, key);

        public static void RaiseGoldSpent(double time, int amount, SpendReason reason) => GoldSpent?.Invoke(time, amount, reason);

        public static void RaiseBossSpawned(double time, string bossId) => BossSpawned?.Invoke(time, bossId);

        public static void RaiseBossKilled(double time, string bossId) => BossKilled?.Invoke(time, bossId);

        public static void RaiseBossRetreated(double time, string bossId) => BossRetreated?.Invoke(time, bossId);

        public static void RaiseRareCardTaken(double time, string cardId) => RareCardTaken?.Invoke(time, cardId);

        public static void RaiseRunWon(double time, int level) => RunWon?.Invoke(time, level);
    }
}
