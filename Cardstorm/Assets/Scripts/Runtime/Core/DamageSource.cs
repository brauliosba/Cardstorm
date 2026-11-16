using System;

namespace Cardstorm.Core
{
    // Carta (y gema) que causa un daño. Viaja con los proyectiles, impactos y torretas hasta el enemigo.
    public readonly struct DamageSource : IEquatable<DamageSource>
    {
        public DamageSource(string cardId, string gemId)
        {
            CardId = cardId;
            GemId = gemId;
        }

        // null = sin origen.
        public string CardId { get; }

        // null = sin gema.
        public string GemId { get; }

        public static readonly DamageSource None = default;

        // Teclas de depuración.
        public static readonly DamageSource Debug = new("debug", null);

        public bool Equals(DamageSource other) =>
            string.Equals(CardId, other.CardId, StringComparison.Ordinal) && string.Equals(GemId, other.GemId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is DamageSource other && Equals(other);

        public override int GetHashCode() =>
            ((CardId != null ? StringComparer.Ordinal.GetHashCode(CardId) : 0) * 397) ^ (GemId != null ? StringComparer.Ordinal.GetHashCode(GemId) : 0);

        public override string ToString() => GemId != null ? $"{CardId}+{GemId}" : CardId ?? "";
    }

    // Daño a los enemigos, para la telemetría. Estático como RunEvents: los suscriptores se dan de baja en su OnDisable.
    public static class DamageEvents
    {
        // Origen, daño útil, daño que sobra al matar, si lo ha matado.
        public static event Action<DamageSource, float, float, bool> EnemyDamaged;

        public static void RaiseEnemyDamaged(in DamageSource source, float dealt, float overkill, bool killed) =>
            EnemyDamaged?.Invoke(source, dealt, overkill, killed);
    }
}
