using Cardstorm.Cards;
using Cardstorm.Progression;

namespace Cardstorm.Localization
{
    // Nombres de las tablas y claves que usa el código. Las de contenido salen del id del asset.
    public static class L10nKeys
    {
        public const string UiTable = "UI";
        public const string ContentTable = "Content";

        public const string HudExhausted = "ui.hud.exhausted";
        public const string HudLevel = "ui.hud.level";
        public const string HudDeck = "ui.hud.deck";
        public const string HudReshuffling = "ui.hud.reshuffling";

        public const string Gold = "ui.common.gold";
        public const string GoldPrice = "ui.common.gold_price";
        public const string Refresh = "ui.common.refresh";
        public const string Level = "ui.common.level";
        public const string Queue = "ui.common.queue";

        public const string UpgradeCard = "ui.levelup.upgrade_card";

        public const string RemovalPrice = "ui.removal.price";
        public const string RemovalMinAction = "ui.removal.min_action";
        public const string RemovalReplaces = "ui.removal.replaces";

        public const string GemPick = "ui.gem.pick_gem";
        public const string GemSocket = "ui.gem.socket";
        public const string GemCardsNone = "ui.gem.cards_none";
        public const string GemCardsOne = "ui.gem.cards_one";
        public const string GemCardsMany = "ui.gem.cards_many";

        public const string RewardTitle = "ui.reward.title";

        public const string EndTime = "ui.end.time";
        public const string EndHint = "ui.end.hint";
        public const string PauseHint = "ui.pause.hint";

        public const string StatCapped = "ui.stat.capped";
        public const string StatPercent = "ui.stat.percent";
        public const string StatPerTen = "ui.stat.per_ten";
        public const string DecimalSeparator = "ui.fmt.decimal";

        public static string CardName(string id) => $"card.{id}.name";
        public static string CardDescription(string id) => $"card.{id}.description";
        public static string GemName(string id) => $"gem.{id}.name";
        public static string GemDescription(string id) => $"gem.{id}.description";
        public static string EnemyName(string id) => $"enemy.{id}.name";

        public static string Type(CardType type) => type switch
        {
            CardType.Defense => "ui.type.defense",
            CardType.Mobility => "ui.type.mobility",
            CardType.Summon => "ui.type.summon",
            CardType.Power => "ui.type.power",
            _ => "ui.type.attack",
        };

        public static string Rarity(CardRarity rarity) => rarity switch
        {
            CardRarity.Basic => "ui.rarity.basic",
            CardRarity.Uncommon => "ui.rarity.uncommon",
            CardRarity.Rare => "ui.rarity.rare",
            _ => "ui.rarity.common",
        };

        public static string Stat(StatType stat) => stat switch
        {
            StatType.MaxHealth => "ui.stat.max_health",
            StatType.HealthRegen => "ui.stat.health_regen",
            StatType.Damage => "ui.stat.damage",
            StatType.EnergyRegen => "ui.stat.energy_regen",
            StatType.MaxEnergy => "ui.stat.max_energy",
            StatType.MoveSpeed => "ui.stat.move_speed",
            StatType.PickupRadius => "ui.stat.pickup_radius",
            StatType.XpGain => "ui.stat.xp_gain",
            StatType.GoldGain => "ui.stat.gold_gain",
            _ => null,
        };
    }
}
