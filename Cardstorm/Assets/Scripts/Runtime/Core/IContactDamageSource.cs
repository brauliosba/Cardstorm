namespace Cardstorm.Core
{
    // Lo implementa todo lo que hace daño al jugador al tocarlo.
    public interface IContactDamageSource
    {
        float ContactDamage { get; }
    }
}
