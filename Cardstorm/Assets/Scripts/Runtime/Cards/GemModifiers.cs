using System;
using UnityEngine;

namespace Cardstorm.Cards
{
    // Lo que una carta hace, para saber qué gemas admite.
    [Flags]
    public enum CardTraits
    {
        None       = 0,
        Damage     = 1 << 0,  // Algún efecto hace daño
        Projectile = 1 << 1,  // Lanza proyectiles
        Area       = 1 << 2,  // Tiene radio, arco o alcance que crece con Amplitud
        Targeted   = 1 << 3,  // Elige un enemigo como objetivo
        HasCost    = 1 << 4,  // Cuesta más de 0
    }

    // Cambios que una gema hace a la copia de la carta. Una carta sin gema usa None.
    [Serializable]
    public struct GemModifiers
    {
        [Tooltip("Multiplica el daño de todos los efectos de la carta.")]
        [Min(0f)] public float damageMultiplier;

        [Tooltip("Multiplica el coste de la copia.")]
        [Min(0f)] public float costMultiplier;

        [Tooltip("Multiplica radios, alcances, arcos (hasta 360°) y saltos de cadena.")]
        [Min(0f)] public float areaMultiplier;

        [Tooltip("Enemigos de más que atraviesa cada proyectil.")]
        [Min(0)] public int extraPierce;

        [Tooltip("Fracción del daño en los impactos de más.")]
        [Range(0f, 1f)] public float extraPierceDamage;

        [Tooltip("Probabilidad de lanzar la carta una segunda vez.")]
        [Range(0f, 1f)] public float repeatChance;

        [Tooltip("Sustituye la prioridad de objetivo de la copia.")]
        public bool overrideTargeting;
        public TargetPriority targeting;

        public static GemModifiers None => new GemModifiers
        {
            damageMultiplier = 1f,
            costMultiplier = 1f,
            areaMultiplier = 1f,
            extraPierceDamage = 1f,
        };
    }
}
