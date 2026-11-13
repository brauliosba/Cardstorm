namespace Cardstorm.Enemies
{
    public enum ChargeState { Approach, Windup, Charging, Recover }

    // Valores de la embestida. Se crean desde EnemyDefinition.
    public readonly struct ChargeSettings
    {
        public readonly float TriggerRange;
        public readonly float Windup;
        public readonly float Speed;
        public readonly float Distance;
        public readonly float Recover;
        public readonly float Cooldown;

        public ChargeSettings(float triggerRange, float windup, float speed, float distance, float recover, float cooldown)
        {
            TriggerRange = triggerRange;
            Windup = windup;
            Speed = speed;
            Distance = distance;
            Recover = recover;
            Cooldown = cooldown;
        }
    }

    // Máquina de estados de la embestida: persigue, avisa, embiste en línea recta y se queda quieto.
    // La dirección y el movimiento los pone EnemyManager.
    public sealed class ChargeLogic
    {
        public ChargeState State { get; private set; }

        // Segundos en el estado actual (aviso y recuperación).
        public float Timer { get; private set; }
        public float Travelled { get; private set; }
        public float Cooldown { get; private set; }

        public void Reset()
        {
            State = ChargeState.Approach;
            Timer = 0f;
            Travelled = 0f;
            Cooldown = 0f;
        }

        // Empieza el aviso sin mirar distancia, línea de visión ni enfriamiento. Solo desde Approach.
        public void Begin()
        {
            if (State == ChargeState.Approach) Enter(ChargeState.Windup);
        }

        // De 0 a 1 durante el aviso.
        public float WindupProgress(in ChargeSettings s) =>
            s.Windup > 0f ? System.Math.Min(Timer / s.Windup, 1f) : 1f;

        // clearPath solo importa en Approach y blocked solo en Charging.
        // speedFactor es la ralentización: embiste más despacio, pero recorre la misma distancia.
        public ChargeState Step(float dt, float distance, bool clearPath, bool blocked, in ChargeSettings s,
            float speedFactor = 1f)
        {
            switch (State)
            {
                case ChargeState.Approach:
                    if (Cooldown > 0f) Cooldown -= dt;
                    if (Cooldown <= 0f && distance <= s.TriggerRange && clearPath)
                        Enter(ChargeState.Windup);
                    break;

                case ChargeState.Windup:
                    Timer += dt;
                    if (Timer >= s.Windup)
                    {
                        Enter(ChargeState.Charging);
                        Travelled = 0f;
                    }
                    break;

                case ChargeState.Charging:
                    if (blocked)
                    {
                        Enter(ChargeState.Recover);
                        break;
                    }
                    Travelled += s.Speed * speedFactor * dt;
                    if (Travelled >= s.Distance) Enter(ChargeState.Recover);
                    break;

                case ChargeState.Recover:
                    Timer += dt;
                    if (Timer >= s.Recover)
                    {
                        Enter(ChargeState.Approach);
                        Cooldown = s.Cooldown;
                    }
                    break;
            }
            return State;
        }

        void Enter(ChargeState state)
        {
            State = state;
            Timer = 0f;
        }
    }
}
