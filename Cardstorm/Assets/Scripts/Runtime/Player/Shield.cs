namespace Cardstorm.Player
{
    // Escudo que absorbe daño durante un tiempo. No se acumulan: se queda el mayor y se renueva la duración.
    public struct Shield
    {
        public float Amount { get; private set; }
        public float Remaining { get; private set; }

        // Lo que absorbía al concederse, para la vista.
        public float Capacity { get; private set; }

        public bool Active => Amount > 0f && Remaining > 0f;
        public float Fraction => Capacity > 0f ? Amount / Capacity : 0f;

        public void Grant(float amount, float duration)
        {
            if (amount <= 0f || duration <= 0f) return;

            if (!Active)
            {
                Amount = Capacity = amount;
                Remaining = duration;
                return;
            }

            if (amount > Amount) Amount = Capacity = amount;
            Remaining = System.Math.Max(Remaining, duration);
        }

        // Devuelve el daño que pasa a la vida.
        public float Absorb(float damage)
        {
            if (!Active || damage <= 0f) return damage;

            if (damage < Amount)
            {
                Amount -= damage;
                return 0f;
            }

            float overflow = damage - Amount;
            Clear();
            return overflow;
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            Remaining -= dt;
            if (Remaining <= 0f) Clear();
        }

        public void Clear()
        {
            Amount = 0f;
            Remaining = 0f;
            Capacity = 0f;
        }
    }
}
