namespace Cardstorm.Enemies
{
    public enum BossPattern { Summon, Charge, Ring }
    public enum PatternState { Chase, Windup, Busy, Retreat }
    public enum PatternAction { None, StartWindup, Fire, StartCharge }

    // Valores del ciclo de patrones. Se crean desde EnemyDefinition.
    public readonly struct PatternSettings
    {
        public readonly float Interval;
        public readonly float FirstDelay;
        public readonly float SummonWindup;
        public readonly float RingWindup;
        public readonly bool SummonEnabled;
        public readonly bool ChargeEnabled;
        public readonly bool RingEnabled;

        public PatternSettings(float interval, float firstDelay, float summonWindup, float ringWindup,
            bool summonEnabled = true, bool chargeEnabled = true, bool ringEnabled = true)
        {
            Interval = interval;
            FirstDelay = firstDelay;
            SummonWindup = summonWindup;
            RingWindup = ringWindup;
            SummonEnabled = summonEnabled;
            ChargeEnabled = chargeEnabled;
            RingEnabled = ringEnabled;
        }

        public bool IsEnabled(BossPattern pattern) =>
            pattern == BossPattern.Summon ? SummonEnabled : pattern == BossPattern.Charge ? ChargeEnabled : RingEnabled;

        public float WindupOf(BossPattern pattern) =>
            pattern == BossPattern.Summon ? SummonWindup : pattern == BossPattern.Ring ? RingWindup : 0f;
    }

    // Ciclo de patrones del jefe: invoca, embiste y dispara un anillo, siempre en ese orden.
    // Los patrones desactivados se saltan.
    // Dice qué hacer en cada paso; EnemyManager lo ejecuta.
    public sealed class BossPatternLogic
    {
        static readonly BossPattern[] Order = { BossPattern.Summon, BossPattern.Charge, BossPattern.Ring };

        int next;
        float windupTimer;

        public PatternState State { get; private set; }

        // Patrón en curso o el último que hizo.
        public BossPattern Current { get; private set; }

        // Segundos hasta el siguiente patrón. Cuenta desde el inicio del anterior.
        public float Timer { get; private set; }

        public float WindupProgress { get; private set; }

        public void Reset(in PatternSettings s)
        {
            State = PatternState.Chase;
            Current = Order[0];
            next = 0;
            windupTimer = 0f;
            WindupProgress = 0f;
            Timer = s.FirstDelay;
        }

        // Pasa al siguiente patrón activo. Sin ninguno, false.
        bool TakeNext(in PatternSettings s)
        {
            for (int i = 0; i < Order.Length; i++)
            {
                var pattern = Order[next];
                next = (next + 1) % Order.Length;
                if (!s.IsEnabled(pattern)) continue;
                Current = pattern;
                return true;
            }
            return false;
        }

        public void BeginRetreat()
        {
            State = PatternState.Retreat;
            WindupProgress = 0f;
        }

        // chargeFinished solo importa en Busy. Como mucho una acción por paso.
        public PatternAction Step(float dt, bool chargeFinished, in PatternSettings s)
        {
            if (State == PatternState.Retreat || dt <= 0f) return PatternAction.None;

            Timer -= dt;
            switch (State)
            {
                case PatternState.Chase:
                    if (Timer > 0f || !TakeNext(s)) return PatternAction.None;
                    Timer = s.Interval;
                    if (Current == BossPattern.Charge)
                    {
                        State = PatternState.Busy;
                        return PatternAction.StartCharge;
                    }
                    State = PatternState.Windup;
                    windupTimer = 0f;
                    WindupProgress = 0f;
                    return PatternAction.StartWindup;

                case PatternState.Windup:
                    windupTimer += dt;
                    float windup = s.WindupOf(Current);
                    WindupProgress = windup > 0f ? System.Math.Min(windupTimer / windup, 1f) : 1f;
                    if (windupTimer < windup) return PatternAction.None;
                    State = PatternState.Chase;
                    WindupProgress = 0f;
                    return PatternAction.Fire;

                case PatternState.Busy:
                    if (chargeFinished) State = PatternState.Chase;
                    return PatternAction.None;
            }
            return PatternAction.None;
        }
    }
}
