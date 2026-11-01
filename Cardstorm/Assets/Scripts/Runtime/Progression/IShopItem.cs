using Cardstorm.Cards;

namespace Cardstorm.Progression
{
    // Lo que la tienda necesita saber de una carta para sortearla y ponerle precio.
    public interface IShopItem
    {
        // Identifica las copias compradas en la partida.
        string Id { get; }
        CardRarity Rarity { get; }

        // Precio antes de la inflación. 0 = el de su rareza.
        int BasePrice { get; }

        // El precio se multiplica por este valor con cada copia comprada. 1 = fijo.
        float PriceMultiplierPerCopy { get; }

        // Copias que se pueden comprar en una partida. 0 = sin límite.
        int MaxCopies { get; }
    }
}
