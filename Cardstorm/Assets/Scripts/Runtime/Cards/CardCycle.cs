using System;
using System.Collections.Generic;

namespace Cardstorm.Cards
{
    public enum SlotState
    {
        Waiting,  // Sin carta: el mazo está vacío o se está barajando.
        Drawing,  // La carta ya se ve, pero aún no se puede jugar.
        Ready,
    }

    public enum PlayResult { Played, NoCard, Drawing, NotEnoughEnergy }

    // Ciclo mazo → mano → descarte.
    public class CardCycle<TCard> where TCard : class, ICard
    {
        readonly List<TCard> drawPile = new();  // La carta de arriba es la última.
        readonly List<TCard> discardPile = new();
        readonly List<TCard> exhausted = new();
        readonly TCard[] hand;
        readonly SlotState[] states;
        readonly float[] drawTimers;
        readonly Random rng;

        float reshuffleTimer;

        public CardCycle(IEnumerable<TCard> deck, int handSize, float drawTime, float reshuffleTime, int seed)
        {
            hand = new TCard[handSize];
            states = new SlotState[handSize];
            drawTimers = new float[handSize];
            DrawTime = drawTime;
            ReshuffleTime = reshuffleTime;
            rng = new Random(seed);

            drawPile.AddRange(deck);
            Shuffle(drawPile);

            // La mano inicial se reparte sin tiempo de robo.
            for (int i = 0; i < handSize; i++)
            {
                if (TryReserve(i))
                {
                    states[i] = SlotState.Ready;
                    drawTimers[i] = 0f;
                }
            }
        }

        public float DrawTime { get; set; }
        public float ReshuffleTime { get; set; }

        public int HandSize => hand.Length;
        public int DrawCount => drawPile.Count;
        public int DiscardCount => discardPile.Count;
        public int ExhaustedCount => exhausted.Count;
        public bool IsReshuffling { get; private set; }

        public float ReshuffleProgress =>
            IsReshuffling && ReshuffleTime > 0f ? 1f - reshuffleTimer / ReshuffleTime : 0f;

        // La carta entra en la ranura y empieza el tiempo de robo.
        public event Action<int, TCard> CardDrawn;
        public event Action<int, TCard> CardReady;
        public event Action<int, TCard> CardPlayed;
        public event Action ReshuffleStarted;
        public event Action Reshuffled;

        public SlotState StateOf(int slot) => states[slot];
        public TCard CardIn(int slot) => hand[slot];

        // 0 al empezar el robo, 1 con la carta lista.
        public float DrawProgress(int slot) => states[slot] switch
        {
            SlotState.Ready => 1f,
            SlotState.Drawing => DrawTime > 0f ? 1f - drawTimers[slot] / DrawTime : 1f,
            _ => 0f,
        };

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            if (IsReshuffling)
            {
                reshuffleTimer -= dt;
                if (reshuffleTimer <= 0f) FinishReshuffle();
            }

            for (int i = 0; i < hand.Length; i++)
            {
                if (states[i] == SlotState.Drawing)
                {
                    drawTimers[i] -= dt;
                    if (drawTimers[i] <= 0f)
                    {
                        states[i] = SlotState.Ready;
                        CardReady?.Invoke(i, hand[i]);
                    }
                }
                else if (states[i] == SlotState.Waiting)
                {
                    TryReserve(i);
                }
            }
        }

        public PlayResult TryPlay(int slot, Energy energy, out TCard card)
        {
            card = hand[slot];
            if (states[slot] == SlotState.Waiting) return PlayResult.NoCard;
            if (states[slot] == SlotState.Drawing) return PlayResult.Drawing;
            if (!energy.TrySpend(card.Cost)) return PlayResult.NotEnoughEnergy;

            hand[slot] = null;
            states[slot] = SlotState.Waiting;
            (card.ExhaustOnPlay ? exhausted : discardPile).Add(card);
            CardPlayed?.Invoke(slot, card);
            TryReserve(slot);
            return PlayResult.Played;
        }

        // Mete una carta nueva en el mazo de robo, en una posición al azar. Durante el rebarajado
        // el mazo está vacío y la carta se baraja con el descarte al terminar. Una ranura en espera
        // la roba en el siguiente Tick.
        public void Add(TCard card)
        {
            if (card == null) return;
            drawPile.Insert(rng.Next(drawPile.Count + 1), card);
        }

        // Quita una carta del mazo, de la mano o del descarte. Si estaba en la mano, su ranura empieza
        // el robo normal. Las cartas agotadas ya no están en el ciclo y no se pueden quitar.
        public bool Remove(TCard card)
        {
            if (card == null) return false;

            for (int i = 0; i < hand.Length; i++)
            {
                if (hand[i] != card) continue;
                hand[i] = null;
                states[i] = SlotState.Waiting;
                TryReserve(i);
                return true;
            }

            return drawPile.Remove(card) || discardPile.Remove(card);
        }

        // Copia en la lista las cartas que siguen en el ciclo: primero el mazo, luego el descarte y por
        // último la mano. Las agotadas no se incluyen.
        public void CopyCards(List<TCard> into)
        {
            into.Clear();
            into.AddRange(drawPile);
            into.AddRange(discardPile);
            foreach (var card in hand)
                if (card != null) into.Add(card);
        }

        // Pone en la ranura la carta de arriba del mazo; si no hay, empieza el rebarajado cuando hay descarte.
        bool TryReserve(int slot)
        {
            if (!IsReshuffling && drawPile.Count > 0)
            {
                int top = drawPile.Count - 1;
                hand[slot] = drawPile[top];
                drawPile.RemoveAt(top);
                states[slot] = SlotState.Drawing;
                drawTimers[slot] = DrawTime;
                CardDrawn?.Invoke(slot, hand[slot]);
                return true;
            }

            if (!IsReshuffling && discardPile.Count > 0) StartReshuffle();
            states[slot] = SlotState.Waiting;
            return false;
        }

        void StartReshuffle()
        {
            IsReshuffling = true;
            reshuffleTimer = ReshuffleTime;
            ReshuffleStarted?.Invoke();
        }

        void FinishReshuffle()
        {
            IsReshuffling = false;
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle(drawPile);
            Reshuffled?.Invoke();
        }

        void Shuffle(List<TCard> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
