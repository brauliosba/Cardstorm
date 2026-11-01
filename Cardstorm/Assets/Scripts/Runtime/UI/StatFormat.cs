using System;
using System.Globalization;
using Cardstorm.Progression;

namespace Cardstorm.UI
{
    // Nombre y valor de cada estadística tal como se muestran en el panel. Los multiplicadores se
    // enseñan como porcentaje sobre la base; el resto, en sus unidades. Decimales con coma.
    public static class StatFormat
    {
        public static string Label(StatType stat) => stat switch
        {
            StatType.MaxHealth => "Vida máxima",
            StatType.HealthRegen => "Regeneración",
            StatType.Damage => "Daño",
            StatType.EnergyRegen => "Recarga de energía",
            StatType.MaxEnergy => "Energía máxima",
            StatType.MoveSpeed => "Velocidad",
            StatType.PickupRadius => "Radio de recogida",
            StatType.XpGain => "Experiencia",
            StatType.GoldGain => "Oro",
            _ => stat.ToString(),
        };

        public static bool IsMultiplier(StatType stat) =>
            stat is StatType.Damage or StatType.EnergyRegen or StatType.XpGain or StatType.GoldGain;

        public static string Value(StatType stat, float value, float baseValue) => stat switch
        {
            StatType.MaxHealth or StatType.MaxEnergy => Number(value, "0"),
            StatType.HealthRegen => Number(value, "0.##") + "/s",
            StatType.MoveSpeed => Number(value, "0.0#") + " u/s",
            StatType.PickupRadius => Number(value, "0.0#") + " u",
            _ when IsMultiplier(stat) => Percent(value, baseValue),
            _ => Number(value, "0.##"),
        };

        // Diferencia con la base en porcentaje: +30 %.
        static string Percent(float value, float baseValue)
        {
            float b = baseValue != 0f ? baseValue : 1f;
            int percent = (int)Math.Round((value / b - 1f) * 100f, MidpointRounding.AwayFromZero);
            return (percent >= 0 ? "+" : "") + percent.ToString(CultureInfo.InvariantCulture) + " %";
        }

        static string Number(float value, string format) =>
            value.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
