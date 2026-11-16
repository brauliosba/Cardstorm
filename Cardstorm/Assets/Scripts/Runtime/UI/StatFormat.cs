using System;
using System.Globalization;
using Cardstorm.Progression;

namespace Cardstorm.UI
{
    // Valor de cada estadística tal como se muestra en el panel.
    public static class StatFormat
    {
        public static bool IsMultiplier(StatType stat) =>
            stat is StatType.Damage or StatType.EnergyRegen or StatType.XpGain or StatType.GoldGain;

        public static string Value(StatType stat, float value, float baseValue) =>
            Value(stat, value, baseValue, StatTexts.Spanish);

        public static string Value(StatType stat, float value, float baseValue, in StatTexts texts) => stat switch
        {
            StatType.MaxHealth or StatType.MaxEnergy => Number(value, "0", texts),
            StatType.HealthRegen => Number(value, "0.##", texts) + "/s",
            StatType.MoveSpeed => Number(value, "0.0#", texts) + " u/s",
            StatType.PickupRadius => Number(value, "0.0#", texts) + " u",
            StatType.GoldGain => ExtraPerTen(value, baseValue, texts),
            _ when IsMultiplier(stat) => Percent(value, baseValue, texts),
            _ => Number(value, "0.##", texts),
        };

        // Diferencia con la base en porcentaje: +30 %.
        static string Percent(float value, float baseValue, in StatTexts texts)
        {
            float b = baseValue != 0f ? baseValue : 1f;
            int percent = (int)Math.Round((value / b - 1f) * 100f, MidpointRounding.AwayFromZero);
            return string.Format(texts.Percent, (percent >= 0 ? "+" : "") + percent.ToString(CultureInfo.InvariantCulture));
        }

        // Oro extra por cada 10 recogidos, como lo describe Codicia: ×1,1 → +1 cada 10.
        static string ExtraPerTen(float value, float baseValue, in StatTexts texts)
        {
            float b = baseValue != 0f ? baseValue : 1f;
            float extra = (float)Math.Round((value / b - 1f) * 10f, 1, MidpointRounding.AwayFromZero);
            return string.Format(texts.PerTen, (extra >= 0f ? "+" : "") + Number(extra, "0.#", texts));
        }

        static string Number(float value, string format, in StatTexts texts) =>
            value.ToString(format, CultureInfo.InvariantCulture).Replace(".", texts.DecimalSeparator);
    }

    // Plantillas del idioma activo para los valores del panel.
    public readonly struct StatTexts
    {
        public readonly string DecimalSeparator;
        public readonly string Percent;
        public readonly string PerTen;

        public StatTexts(string decimalSeparator, string percent, string perTen)
        {
            DecimalSeparator = decimalSeparator;
            Percent = percent;
            PerTen = perTen;
        }

        public static StatTexts Spanish => new StatTexts(",", "{0} %", "{0} cada 10");
    }
}
