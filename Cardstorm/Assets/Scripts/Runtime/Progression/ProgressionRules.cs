using System;
using Cardstorm.Cards;
using UnityEngine;

namespace Cardstorm.Progression
{
    // Valor base y máximo de una estadística del personaje.
    [Serializable]
    public class StatRule
    {
        public StatType stat;
        public float baseValue;
        public float max;

        public StatRule(StatType stat, float baseValue, float max)
        {
            this.stat = stat;
            this.baseValue = baseValue;
            this.max = max;
        }

        public static StatRule[] Defaults() => new[]
        {
            new StatRule(StatType.MaxHealth, 100f, 175f),
            new StatRule(StatType.HealthRegen, 0f, 0.9f),
            new StatRule(StatType.Damage, 1f, 1.3f),
            new StatRule(StatType.EnergyRegen, 1f, 1.45f),
            new StatRule(StatType.MaxEnergy, 10f, 16f),
            new StatRule(StatType.MoveSpeed, 5f, 6.5f),
            new StatRule(StatType.PickupRadius, 2f, 4f),
            new StatRule(StatType.XpGain, 1f, 1.3f),
            new StatRule(StatType.GoldGain, 1f, 1.2f),
        };
    }

    // Todos los números de la progresión dentro de la partida. Clase simple para poder crearla en las pruebas.
    [Serializable]
    public class ProgressionRules
    {
        [Header("Experiencia · XP(n) = base + factor · (n − 1)^exponente")]
        public float xpBase = 5f;
        public float xpFactor = 4f;
        public float xpExponent = 1.5f;

        [Header("Oro y precios")]
        [Min(0)] public int startingGold = 38;

        [Tooltip("Subida de todos los precios por nivel: × (1 + inflación · (nivel − 1)).")]
        [Min(0f)] public float inflationPerLevel = 0.04f;

        [Tooltip("Precio base de una carta según su rareza (las básicas cuestan como las comunes).")]
        [Min(0)] public int commonPrice = 38;
        [Min(0)] public int uncommonPrice = 55;
        [Min(0)] public int rarePrice = 88;

        [Header("Tienda")]
        [Tooltip("Peso de cada rareza al sortear una ranura de acción.")]
        [Min(0f)] public float commonWeight = 60f;
        [Min(0f)] public float uncommonWeight = 30f;
        [Min(0f)] public float rareWeight = 10f;

        [Tooltip("Segundos de partida antes de los cuales no salen cartas raras.")]
        [Min(0f)] public float rareMinTime = 300f;

        [Tooltip("Refrescar cuesta paso · k, con k = 1, 2, 3… en cada tienda.")]
        [Min(0)] public int refreshPriceStep = 12;

        [Header("Eliminar carta")]
        [Min(1)] public int removalEveryLevels = 5;
        [Tooltip("Precio de la primera eliminación. No lleva inflación.")]
        [Min(0)] public int removalBasePrice = 5;
        [Tooltip("Lo que sube el precio con cada eliminación anterior en la partida.")]
        [Min(0)] public int removalStep = 5;
        [Tooltip("Cartas de acción que deben quedar como mínimo en el mazo.")]
        [Min(1)] public int minActionCards = 5;

        [Header("Estadísticas del personaje")]
        public StatRule[] stats = StatRule.Defaults();

        public int RarityPrice(CardRarity rarity) => rarity switch
        {
            CardRarity.Uncommon => uncommonPrice,
            CardRarity.Rare => rarePrice,
            _ => commonPrice,
        };
    }
}
