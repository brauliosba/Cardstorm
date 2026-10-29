using UnityEngine;

namespace Cardstorm.Cards
{
    // Energía de las cartas: se regenera de forma continua hasta un máximo.
    public class Energy
    {
        // Margen para que la suma de muchos dt no deje 0,99999 frente a un coste de 1.
        const float Epsilon = 1e-4f;

        readonly float baseMax;
        readonly float baseRegen;

        public Energy(float baseMax, float baseRegen, float start)
        {
            this.baseMax = baseMax;
            this.baseRegen = baseRegen;
            Current = Mathf.Clamp(start, 0f, baseMax);
        }

        public float Current { get; private set; }

        // Bonificación de la carta Reserva.
        public float MaxBonus { get; set; }

        // Multiplicador de la carta Recarga rápida.
        public float RegenMultiplier { get; set; } = 1f;

        // Truco de depuración: las cartas no gastan energía.
        public bool Infinite { get; set; }

        public float Max => baseMax + MaxBonus;
        public float RegenPerSecond => baseRegen * RegenMultiplier;

        // Energía que se habría regenerado con la barra llena.
        public float Wasted { get; private set; }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            float gain = RegenPerSecond * dt;
            float room = Mathf.Max(0f, Max - Current);
            if (gain > room)
            {
                Wasted += gain - room;
                Current = Max;
            }
            else
            {
                Current += gain;
            }
        }

        public bool CanAfford(float cost) => Infinite || Current + Epsilon >= cost;

        public bool TrySpend(float cost)
        {
            if (!CanAfford(cost)) return false;
            if (!Infinite) Current = Mathf.Max(0f, Current - cost);
            return true;
        }
    }
}
