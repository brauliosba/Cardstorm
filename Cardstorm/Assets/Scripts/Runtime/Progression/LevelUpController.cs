using System;
using System.Collections.Generic;
using Cardstorm.Cards;
using Cardstorm.Core;
using Cardstorm.Player;
using Cardstorm.UI;
using UnityEngine;

namespace Cardstorm.Progression
{
    // Pausa la partida en cada subida de nivel y muestra la tienda; al cerrar la última, la reanuda.
    // En los niveles que lo ofrecen, la tienda deja además eliminar una carta del mazo.
    public class LevelUpController : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] GameClock clock;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerHealth health;
        [SerializeField] PlayerCards cards;
        [SerializeField] ShopScreen shop;
        [SerializeField] RemovalScreen removalScreen;
        [SerializeField] ShopCatalog catalog;

        [Tooltip("Invulnerabilidad al volver a la partida, para no recibir un golpe nada más cerrar la tienda.")]
        [SerializeField, Min(0f)] float resumeInvulnerability = 0.5f;

        [Tooltip("Fracción de la vida máxima que se cura en cada subida de nivel.")]
        [SerializeField, Range(0f, 1f)] float levelUpHealFraction = 0.5f;

        LevelUpQueue queue;
        ShopOffer<CardDefinition> offer;
        CardRemoval removal;
        readonly List<CardInstance> deckBuffer = new();
        readonly List<RemovalEntry> entries = new();
        int entryCount;
        int actionCards;
        float previousTimeScale = 1f;
        bool forceRemoval;
        bool refillEnergyOnClose;
        bool held;

        public bool IsOpen { get; private set; }

        // Al cerrar la última tienda de la tanda.
        public event Action Closed;

        // Mientras otra pantalla congela la partida, las subidas esperan en la cola y se abren al soltarla.
        public bool Held
        {
            get => held;
            set
            {
                held = value;
                if (!held && !IsOpen && queue != null && !queue.IsEmpty) Open();
            }
        }
        public LevelUpQueue Queue => queue;
        public ShopOffer<CardDefinition> Offer => offer;
        public CardRemoval Removal => removal;

        double Now => clock != null ? clock.Elapsed : Time.timeSinceLevelLoad;

        void Awake()
        {
            if (run == null) run = FindFirstObjectByType<RunManager>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
            if (shop == null) shop = FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
            if (removalScreen == null) removalScreen = FindFirstObjectByType<RemovalScreen>(FindObjectsInactive.Include);
            if (catalog == null) Debug.LogError("LevelUpController no tiene ShopCatalog: la tienda saldrá vacía.", this);
        }

        void OnEnable()
        {
            RunEvents.LevelUp += OnLevelUp;
            if (shop != null)
            {
                shop.BuyPressed += OnBuy;
                shop.RefreshPressed += OnRefresh;
                shop.RemovePressed += OnRemovePressed;
                shop.ContinuePressed += OnContinue;
            }
            if (removalScreen != null)
            {
                removalScreen.EntryPressed += OnRemoveEntry;
                removalScreen.BackPressed += CloseRemoval;
            }
        }

        void OnDisable()
        {
            RunEvents.LevelUp -= OnLevelUp;
            if (shop != null)
            {
                shop.BuyPressed -= OnBuy;
                shop.RefreshPressed -= OnRefresh;
                shop.RemovePressed -= OnRemovePressed;
                shop.ContinuePressed -= OnContinue;
            }
            if (removalScreen != null)
            {
                removalScreen.EntryPressed -= OnRemoveEntry;
                removalScreen.BackPressed -= CloseRemoval;
            }
        }

        // RunManager crea el estado en su Start: la cola y la tienda se crean con la primera subida.
        void EnsureCreated()
        {
            var state = run.State;
            queue ??= new LevelUpQueue(state.Rules);
            if (offer == null)
            {
                var roller = new ShopRoller<CardDefinition>(
                    state.Rules,
                    catalog != null ? catalog.actionCards : null,
                    catalog != null ? catalog.powerCards : null);
                offer = new ShopOffer<CardDefinition>(roller, state);
            }
            removal ??= new CardRemoval(state);
        }

        void OnLevelUp(double time, int level)
        {
            if (run.Won) return;
            if (health != null) health.Heal(health.MaxHealth * levelUpHealFraction);
            // La energía se rellena al cerrar la tienda, para incluir la Reserva que se compre en ella.
            refillEnergyOnClose = true;
            EnsureCreated();
            queue.Enqueue(level);

            if (IsOpen) shop.UpdateQueue(queue.Position, queue.Total);
            else if (!held) Open();
        }

        // Depuración: abre una tienda del nivel actual con Eliminar carta, sin subir de nivel.
        public void OpenShopWithRemoval()
        {
            if (IsOpen || held || run.State == null || run.Won) return;
            EnsureCreated();
            forceRemoval = true;
            queue.Enqueue(run.State.Level);
            Open();
        }

        void Open()
        {
            IsOpen = true;
            clock.Paused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            input.SetGameplayEnabled(false);
            ShowCurrent();
        }

        void ShowCurrent()
        {
            offer.Open(queue.CurrentLevel, Now);
            bool offersRemoval = queue.CurrentOffersRemoval || forceRemoval;
            forceRemoval = false;
            removal.Open(queue.CurrentLevel, offersRemoval && removalScreen != null);
            shop.Show(queue.CurrentLevel, queue.Position, queue.Total, offer, removal);
        }

        void OnBuy(int slot)
        {
            if (!IsOpen) return;
            if (!offer.TryBuy(slot, Now, out var card))
            {
                shop.BuyFailed(slot);
                return;
            }

            cards.Cycle.Add(new CardInstance(card));
            shop.UpdateOffer(offer);
            shop.Bought(slot);
        }

        void OnRefresh()
        {
            if (!IsOpen) return;
            if (!offer.TryRefresh(Now))
            {
                shop.RefreshFailed();
                return;
            }
            shop.UpdateOffer(offer);
        }

        // Sin oro, el botón tambalea y no abre la lista.
        void OnRemovePressed()
        {
            if (!IsOpen || removalScreen.IsVisible || !removal.Offered) return;
            if (removal.State.Gold < removal.Price)
            {
                shop.RemoveFailed();
                return;
            }

            shop.SetSuspended(true);
            ShowEntries();
        }

        void ShowEntries()
        {
            BuildEntries();
            removalScreen.Show(entries, entryCount, removal.Price, removal.State.Gold, removal.State.Rules.minActionCards);
        }

        void OnRemoveEntry(int index)
        {
            if (!IsOpen || index < 0 || index >= entryCount) return;

            var entry = entries[index];
            var result = removal.TryRemove(entry.Card.id, IsAction(entry.Card), actionCards, Now);
            if (result != RemovalResult.Removed)
            {
                removalScreen.EntryFailed(index);
                return;
            }

            // La lista sigue abierta para quitar más, con el precio nuevo; Volver regresa a la tienda.
            cards.Cycle.Remove(entry.Instance);
            shop.UpdateOffer(offer);
            ShowEntries();
        }

        void CloseRemoval()
        {
            if (!removalScreen.IsVisible) return;
            removalScreen.Hide();
            shop.SetSuspended(false);
        }

        // Agrupa el mazo por carta.
        void BuildEntries()
        {
            cards.Cycle.CopyCards(deckBuffer);
            entryCount = 0;
            actionCards = 0;

            foreach (var instance in deckBuffer)
            {
                var card = instance.Definition;
                if (IsAction(card)) actionCards++;

                int found = -1;
                for (int i = 0; i < entryCount; i++)
                    if (entries[i].Card == card) { found = i; break; }

                if (found >= 0)
                {
                    entries[found].Count++;
                    continue;
                }

                if (entryCount == entries.Count) entries.Add(new RemovalEntry());
                var entry = entries[entryCount++];
                entry.Card = card;
                entry.Instance = instance;
                entry.Count = 1;
            }

            for (int i = 0; i < entryCount; i++)
                entries[i].Allowed = removal.Allows(IsAction(entries[i].Card), actionCards);

            entries.Sort(0, entryCount, EntryOrder.Instance);
        }

        static bool IsAction(CardDefinition card) => card.type != CardType.Power;

        sealed class EntryOrder : IComparer<RemovalEntry>
        {
            public static readonly EntryOrder Instance = new();

            public int Compare(RemovalEntry a, RemovalEntry b)
            {
                int byType = IsAction(b.Card).CompareTo(IsAction(a.Card));
                return byType != 0 ? byType : string.CompareOrdinal(a.Card.displayName, b.Card.displayName);
            }
        }

        void OnContinue()
        {
            if (!IsOpen || (removalScreen != null && removalScreen.IsVisible)) return;

            if (queue.Complete()) ShowCurrent();
            else Close();
        }

        void Close()
        {
            IsOpen = false;
            shop.Hide();
            Time.timeScale = previousTimeScale;

            if (refillEnergyOnClose && cards != null) cards.Energy.Refill();
            refillEnergyOnClose = false;

            // La derrota también usa Paused: si el jugador ha muerto, el reloj sigue parado.
            if (health == null || !health.IsDead)
            {
                clock.Paused = false;
                if (health != null) health.GrantInvulnerability(resumeInvulnerability);
            }
            input.SetGameplayEnabled(true);
            Closed?.Invoke();
        }
    }
}
