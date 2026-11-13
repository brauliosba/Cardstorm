namespace Cardstorm.Enemies
{
    // Ralentización de un enemigo. No se acumulan: se queda la más fuerte y, si son iguales, la más larga.
    // Guarda la reducción y no el factor para que el valor por defecto sea «sin ralentizar».
    public struct Slow
    {
        float reduction;

        // Multiplicador de la velocidad (1 = sin ralentizar).
        public float Factor => 1f - reduction;
        public float Remaining { get; private set; }
        public bool Active => Remaining > 0f;

        // Los jefes reciben la mitad del efecto.
        public static float ForBoss(float factor) => 1f - (1f - factor) * 0.5f;

        public void Apply(float factor, float duration)
        {
            if (duration <= 0f || factor >= 1f) return;
            float r = 1f - System.Math.Max(factor, 0f);

            if (!Active || r > reduction + 1e-4f)
            {
                reduction = r;
                Remaining = duration;
            }
            else if (r > reduction - 1e-4f && duration > Remaining)
            {
                Remaining = duration;
            }
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            Remaining -= dt;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                reduction = 0f;
            }
        }
    }
}
