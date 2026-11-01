namespace Cardstorm.Progression
{
    public enum RemovalResult
    {
        Removed,
        NotOffered,          // La tienda de este nivel no ofrece eliminar.
        TooFewActionCards,   // Quedarían menos cartas de acción que el mínimo.
        NotEnoughGold,
    }

    // Eliminar carta dentro de una tienda, en los niveles que lo ofrecen. Se pueden quitar varias en la
    // misma tienda; el precio sube con cada eliminación de la partida y no depende del nivel. No toca el mazo: eso lo hace quien la llama.
    public class CardRemoval
    {
        readonly RunState state;

        public CardRemoval(RunState state) => this.state = state;

        public RunState State => state;
        public int Level { get; private set; }
        public bool Offered { get; private set; }

        // Cartas eliminadas en la tienda actual.
        public int RemovedHere { get; private set; }

        public int Price => Pricing.RemovalPrice(state.Rules, state.Removals);

        public void Open(int level, bool offered)
        {
            Level = level;
            Offered = offered;
            RemovedHere = 0;
        }

        // Una carta de acción solo se puede quitar si después quedan al menos minActionCards.
        // Las de poder no cuentan para el mínimo.
        public bool Allows(bool isAction, int actionCardsInDeck) =>
            !isAction || actionCardsInDeck > state.Rules.minActionCards;

        // Comprueba todo menos el oro: sirve para atenuar las cartas de la lista.
        public RemovalResult Check(bool isAction, int actionCardsInDeck)
        {
            if (!Offered) return RemovalResult.NotOffered;
            if (!Allows(isAction, actionCardsInDeck)) return RemovalResult.TooFewActionCards;
            return RemovalResult.Removed;
        }

        public RemovalResult TryRemove(string cardId, bool isAction, int actionCardsInDeck, double time)
        {
            var result = Check(isAction, actionCardsInDeck);
            if (result != RemovalResult.Removed) return result;

            int price = Price;
            if (!state.TrySpendGold(price)) return RemovalResult.NotEnoughGold;

            RemovedHere++;
            state.RegisterRemoval();
            RunEvents.RaiseCardRemoved(time, cardId, price);
            RunEvents.RaiseGoldSpent(time, price, SpendReason.Remove);
            return RemovalResult.Removed;
        }
    }
}
