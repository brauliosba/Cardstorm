namespace Cardstorm.Core
{
    // Lo implementa todo lo que hace daño al jugador al tocarlo.
    public interface IContactDamageSource
    {
        float ContactDamage { get; }

        // Id del enemigo, para la causa de la muerte.
        string SourceId { get; }
    }
}
