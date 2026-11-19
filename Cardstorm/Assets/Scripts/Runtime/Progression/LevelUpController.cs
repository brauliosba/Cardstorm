using System;
using System.Collections.Generic;
using Cardstorm.Cards;
using Cardstorm.Combat;
using Cardstorm.Core;
using Cardstorm.Localization;
using Cardstorm.Player;
using Cardstorm.UI;
using UnityEngine;

namespace Cardstorm.Progression
{
    // Pausa la partida en cada subida de nivel.
    public class LevelUpController : MonoBehaviour
    {
        enum Phase { None, Choice, Shop, Gems, Removal }

        [SerializeField] RunManager run;
        [SerializeField] GameClock clock;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerHealth health;
        [SerializeField] PlayerCards cards;
        [SerializeField] LevelUpChoiceScreen choice;
        [SerializeField] ShopScreen shop;
        [SerializeField] GemScreen gemScreen;
        [SerializeField] RemovalScreen removalScreen;
        [SerializeField] ShopCatalog catalog;
        [SerializeField] GemCatalog gemCatalog;
        [SerializeField] VfxManager vfx;

        [Tooltip("Se emite en el jugador al subir de nivel. La elección se abre cuando acaba (la vida del sistema raíz).")]
        [SerializeField] ParticleSystem levelUpVfx;

        [Tooltip("Invulnerabilidad al volver a la partida, para no recibir un golpe nada más cerrar la tienda.")]
        [SerializeField, Min(0f)] float resumeInvulnerability = 0.5f;

        [Tooltip("Fracción de la vida máxima que se cura en cada subida de nivel.")]
        [SerializeField, Range(0f, 1f)] float levelUpHealFraction = 0.5f;

        LevelUpQueue queue;
        ShopOffer<CardDefinition> offer;
        GemOffer<GemDefinition> gemOffer;
        CardRemoval removal;
        readonly List<CardInstance> deckBuffer = new();
        readonly List<RemovalEntry> entries = new();
        readonly int[] compatible = new int[GemOffer<GemDefinition>.SlotCount];
        int entryCount;
        int actionCards;
        GemDefinition pickedGem;
        Phase phase;
        float previousTimeScale = 1f;
        bool forceRemoval;
        bool gemsOpened;
        bool spentInShop;
        bool lockNextScreen;
        bool refillEnergyOnClose;
        bool held;
        // Hora de la partida a la que se abre la elección tras el VFX (negativa si no hay ninguna esperando).
        double openAt = -1;

        public bool IsOpen { get; private set; }

        // Al cerrar la última subida de la tanda.
        public event Action Closed;

        // Mientras otra pantalla congela la partida, las subidas esperan en la cola y se abren al soltarla.
        public bool Held
        {
            get => held;
            set
            {
                held = value;
                if (!held && !IsOpen && !Waiting && queue != null && !queue.IsEmpty) Open();
            }
        }
        public LevelUpQueue Queue => queue;
        public ShopOffer<CardDefinition> Offer => offer;
        public GemOffer<GemDefinition> GemOffer => gemOffer;
        public CardRemoval Removal => removal;

        double Now => clock != null ? clock.Elapsed : Time.timeSinceLevelLoad;

        bool Waiting => openAt >= 0;

        float VfxDuration => levelUpVfx != null ? levelUpVfx.main.startLifetime.constantMax : 0f;

        bool GemsAvailable => gemScreen != null && gemCatalog != null && gemCatalog.gems.Count > 0;

        void Awake()
        {
            if (run == null) run = FindFirstObjectByType<RunManager>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
            if (choice == null) choice = FindFirstObjectByType<LevelUpChoiceScreen>(FindObjectsInactive.Include);
            if (shop == null) shop = FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
            if (gemScreen == null) gemScreen = FindFirstObjectByType<GemScreen>(FindObjectsInactive.Include);
            if (removalScreen == null) removalScreen = FindFirstObjectByType<RemovalScreen>(FindObjectsInactive.Include);
            if (vfx == null) vfx = FindFirstObjectByType<VfxManager>();
            if (catalog == null) Debug.LogError("LevelUpController no tiene ShopCatalog: la tienda saldrá vacía.", this);
            if (gemCatalog == null) Debug.LogWarning("LevelUpController no tiene GemCatalog: Mejorar carta no se podrá elegir.", this);
            if (choice == null) Debug.LogWarning("LevelUpController no tiene LevelUpChoiceScreen: cada subida abre directamente la tienda.", this);
        }

        void OnEnable()
        {
            RunEvents.LevelUp += OnLevelUp;
            if (choice != null)
            {
                choice.ShopChosen += OnShopChosen;
                choice.GemsChosen += OnGemsChosen;
            }
            if (shop != null)
            {
                shop.BuyPressed += OnBuy;
                shop.RefreshPressed += OnRefresh;
                shop.BackPressed += OnShopBack;
                shop.ContinuePressed += OnContinue;
            }
            if (gemScreen != null)
            {
                gemScreen.GemPicked += OnGemPicked;
                gemScreen.CardPicked += OnGemCard;
                gemScreen.RefreshPressed += OnGemRefresh;
                gemScreen.BackPressed += OnGemBack;
            }
            if (removalScreen != null)
            {
                removalScreen.EntryPressed += OnRemoveEntry;
                removalScreen.BackPressed += OnRemovalDone;
            }
        }

        void OnDisable()
        {
            RunEvents.LevelUp -= OnLevelUp;
            if (choice != null)
            {
                choice.ShopChosen -= OnShopChosen;
                choice.GemsChosen -= OnGemsChosen;
            }
            if (shop != null)
            {
                shop.BuyPressed -= OnBuy;
                shop.RefreshPressed -= OnRefresh;
                shop.BackPressed -= OnShopBack;
                shop.ContinuePressed -= OnContinue;
            }
            if (gemScreen != null)
            {
                gemScreen.GemPicked -= OnGemPicked;
                gemScreen.CardPicked -= OnGemCard;
                gemScreen.RefreshPressed -= OnGemRefresh;
                gemScreen.BackPressed -= OnGemBack;
            }
            if (removalScreen != null)
            {
                removalScreen.EntryPressed -= OnRemoveEntry;
                removalScreen.BackPressed -= OnRemovalDone;
            }
        }

        // El reloj de la partida se para en pausa: la espera también.
        void Update()
        {
            if (!Waiting || Now < openAt) return;
            openAt = -1;
            if (run.Won || (health != null && health.IsDead)) return;
            if (!held && !IsOpen && !queue.IsEmpty) Open();
        }

        // El pilar, el anillo y las flechas van en espacio local: siguen al jugador.
        void LateUpdate()
        {
            if (vfx != null && health != null) vfx.Follow(levelUpVfx, health.transform.position);
        }

        // RunManager crea el estado en su Start: la cola y las ofertas se crean con la primera subida.
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
            gemOffer ??= new GemOffer<GemDefinition>(gemCatalog != null ? gemCatalog.gems : null, state);
            removal ??= new CardRemoval(state);
        }

        void OnLevelUp(double time, int level)
        {
            if (run.Won) return;
            if (health != null) health.Heal(health.MaxHealth * levelUpHealFraction);
            // La energía se rellena al cerrar, para incluir la Reserva que se compre en la tienda.
            refillEnergyOnClose = true;
            EnsureCreated();
            queue.Enqueue(level);

            if (IsOpen)
            {
                if (choice != null) choice.UpdateQueue(queue.Position, queue.Total);
                shop.UpdateQueue(queue.Position, queue.Total);
            }
            else if (!held && !Waiting) PlayVfxThenOpen();
        }

        // Varias subidas seguidas comparten un solo VFX: las demás esperan en la cola.
        void PlayVfxThenOpen()
        {
            float duration = VfxDuration;
            if (duration <= 0f || vfx == null || health == null)
            {
                Open();
                return;
            }
            vfx.Burst(levelUpVfx, health.transform.position);
            health.GrantInvulnerability(duration);
            openAt = Now + duration;
        }

        // Depuración: abre la elección del nivel actual con Eliminar carta al final, sin subir de nivel.
        public void OpenWithRemoval()
        {
            if (!CanOpenForDebug()) return;
            forceRemoval = true;
            queue.Enqueue(run.State.Level);
            Open();
        }

        // Depuración: abre directamente Mejorar carta del nivel actual, sin subir de nivel.
        public void OpenGems()
        {
            if (!CanOpenForDebug() || !GemsAvailable) return;
            queue.Enqueue(run.State.Level);
            Open(gemsFirst: true);
        }

        bool CanOpenForDebug()
        {
            if (IsOpen || held || Waiting || run.State == null || run.Won) return false;
            EnsureCreated();
            return true;
        }

        void Open(bool gemsFirst = false)
        {
            IsOpen = true;
            clock.Paused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            input.SetGameplayEnabled(false);
            lockNextScreen = true;
            StartLevel(gemsFirst);
        }

        // La primera pantalla después de la partida bloquea la entrada un momento; las siguientes no.
        bool TakeInputLock()
        {
            bool value = lockNextScreen;
            lockNextScreen = false;
            return value;
        }

        // Las ofertas se sortean una vez por nivel: ir y volver de la elección no las cambia.
        void StartLevel(bool gemsFirst = false)
        {
            int level = queue.CurrentLevel;
            offer.Open(level, Now);
            gemsOpened = false;
            spentInShop = false;
            bool offersRemoval = queue.CurrentOffersRemoval || forceRemoval;
            forceRemoval = false;
            removal.Open(level, offersRemoval && removalScreen != null);

            if (!gemsFirst)
            {
                ShowChoice();
                return;
            }
            phase = Phase.Choice;
            OnGemsChosen();
        }

        void ShowChoice()
        {
            if (choice == null)
            {
                OnShopChosen();
                return;
            }
            phase = Phase.Choice;
            choice.Show(queue.CurrentLevel, queue.Position, queue.Total, run.State.Gold, GemsAvailable,
                removal.Offered, TakeInputLock());
        }

        void HideChoice()
        {
            if (choice != null && choice.IsVisible) choice.Hide();
        }


        void OnShopChosen()
        {
            if (!IsOpen || (phase != Phase.Choice && choice != null)) return;
            HideChoice();
            phase = Phase.Shop;
            shop.Show(queue.CurrentLevel, queue.Position, queue.Total, offer, choice != null && !spentInShop, TakeInputLock());
        }

        void OnBuy(int slot)
        {
            if (!IsOpen || phase != Phase.Shop) return;
            if (!offer.TryBuy(slot, Now, out var card))
            {
                shop.BuyFailed(slot);
                return;
            }

            cards.Cycle.Add(new CardInstance(card));
            // Comprar cierra la vuelta a la elección: no se puede comprar y además poner una gema.
            spentInShop = true;
            shop.SetBackAvailable(false);
            shop.UpdateOffer(offer);
            shop.Bought(slot);
        }

        void OnRefresh()
        {
            if (!IsOpen || phase != Phase.Shop) return;
            if (!offer.TryRefresh(Now))
            {
                shop.RefreshFailed();
                return;
            }
            shop.UpdateOffer(offer);
        }

        void OnShopBack()
        {
            if (!IsOpen || phase != Phase.Shop || spentInShop || choice == null) return;
            shop.Hide();
            ShowChoice();
        }

        void OnContinue()
        {
            if (!IsOpen || phase != Phase.Shop) return;
            shop.Hide();
            RunEvents.RaiseLevelUpChoice(Now, queue.CurrentLevel, LevelUpOption.Shop);
            AfterChoice();
        }


        void OnGemsChosen()
        {
            if (!IsOpen || phase != Phase.Choice || !GemsAvailable) return;
            if (!gemsOpened)
            {
                gemOffer.Open(queue.CurrentLevel, Now);
                gemsOpened = true;
            }
            HideChoice();
            phase = Phase.Gems;
            pickedGem = null;
            CountCompatible();
            gemScreen.ShowGems(gemOffer, compatible, TakeInputLock());
        }

        void OnGemRefresh()
        {
            if (!IsOpen || phase != Phase.Gems || gemScreen.ShowingCards) return;
            if (!gemOffer.TryRefresh(Now))
            {
                gemScreen.RefreshFailed();
                return;
            }
            CountCompatible();
            gemScreen.UpdateGems(gemOffer, compatible);
        }

        void OnGemPicked(int slot)
        {
            if (!IsOpen || phase != Phase.Gems || gemScreen.ShowingCards) return;
            var gem = gemOffer.Slot(slot);
            if (gem == null || compatible[slot] == 0)
            {
                gemScreen.GemFailed(slot);
                return;
            }

            pickedGem = gem;
            BuildEntries(gem);
            gemScreen.ShowCards(gem, entries, entryCount);
        }

        void OnGemCard(int index)
        {
            if (!IsOpen || phase != Phase.Gems || pickedGem == null || index < 0 || index >= entryCount) return;

            var instance = entries[index].Instance;
            if (!instance.Accepts(pickedGem))
            {
                gemScreen.CardFailed(index);
                return;
            }

            var previous = instance.Gem;
            instance.SetGem(pickedGem);
            RunEvents.RaiseGemApplied(Now, pickedGem.id, instance.Definition.id, previous != null ? previous.id : null);
            RunEvents.RaiseLevelUpChoice(Now, queue.CurrentLevel, LevelUpOption.Gem);
            pickedGem = null;
            gemScreen.Hide();
            AfterChoice();
        }

        void OnGemBack()
        {
            if (!IsOpen || phase != Phase.Gems) return;
            if (gemScreen.ShowingCards)
            {
                pickedGem = null;
                CountCompatible();
                gemScreen.ShowGems(gemOffer, compatible, lockInput: false);
                return;
            }
            gemScreen.Hide();
            ShowChoice();
        }

        // Copias del mazo que admiten cada gema ofrecida.
        void CountCompatible()
        {
            cards.Cycle.CopyCards(deckBuffer);
            for (int i = 0; i < compatible.Length; i++)
            {
                var gem = gemOffer.Slot(i);
                int count = 0;
                if (gem != null)
                    foreach (var instance in deckBuffer)
                        if (instance.Accepts(gem)) count++;
                compatible[i] = count;
            }
        }


        // Tras la tienda o las gemas: en los niveles que lo ofrecen, la lista de eliminar; si no, el nivel termina.
        void AfterChoice()
        {
            if (removal.Offered)
            {
                phase = Phase.Removal;
                ShowRemoval();
            }
            else CompleteLevel();
        }

        void ShowRemoval()
        {
            BuildEntries(null);
            removalScreen.Show(entries, entryCount, removal.Price, removal.State.Gold, removal.State.Rules.minActionCards);
        }

        void OnRemoveEntry(int index)
        {
            if (!IsOpen || phase != Phase.Removal || index < 0 || index >= entryCount) return;

            var entry = entries[index];
            var result = removal.TryRemove(entry.Card.id, IsAction(entry.Card), actionCards, Now);
            if (result != RemovalResult.Removed)
            {
                removalScreen.EntryFailed(index);
                return;
            }

            // La lista sigue abierta para quitar más, con el precio nuevo; Continuar termina el nivel.
            cards.Cycle.Remove(entry.Instance);
            ShowRemoval();
        }

        void OnRemovalDone()
        {
            if (!IsOpen || phase != Phase.Removal) return;
            removalScreen.Hide();
            CompleteLevel();
        }


        // Agrupa el mazo por carta y gema. Sin gema, para eliminar: todas las copias, y las de acción solo
        // si quedan más del mínimo. Con gema, para engarzar: las copias que la admiten o que ya la tienen
        // (atenuadas).
        void BuildEntries(GemDefinition gem)
        {
            cards.Cycle.CopyCards(deckBuffer);
            entryCount = 0;
            actionCards = 0;

            foreach (var instance in deckBuffer)
            {
                var card = instance.Definition;
                if (IsAction(card)) actionCards++;
                if (gem != null && !instance.Accepts(gem) && instance.Gem != gem) continue;

                int found = -1;
                for (int i = 0; i < entryCount; i++)
                    if (entries[i].Card == card && entries[i].Gem == instance.Gem) { found = i; break; }

                if (found >= 0)
                {
                    entries[found].Count++;
                    continue;
                }

                if (entryCount == entries.Count) entries.Add(new RemovalEntry());
                var entry = entries[entryCount++];
                entry.Card = card;
                entry.Gem = instance.Gem;
                entry.Instance = instance;
                entry.Count = 1;
                entry.Note = null;
            }

            for (int i = 0; i < entryCount; i++)
            {
                var entry = entries[i];
                if (gem == null)
                {
                    entry.Allowed = removal.Allows(IsAction(entry.Card), actionCards);
                    continue;
                }
                entry.Allowed = entry.Instance.Accepts(gem);
                if (entry.Gem != null && entry.Gem != gem) entry.Note = L10n.Format(L10nKeys.RemovalReplaces, L10n.GemName(entry.Gem));
            }

            entries.Sort(0, entryCount, EntryOrder.Instance);
        }

        static bool IsAction(CardDefinition card) => card.type != CardType.Power;

        // Acción antes que poder, por nombre y, con el mismo nombre, sin gema primero.
        sealed class EntryOrder : IComparer<RemovalEntry>
        {
            public static readonly EntryOrder Instance = new();

            public int Compare(RemovalEntry a, RemovalEntry b)
            {
                int byType = IsAction(b.Card).CompareTo(IsAction(a.Card));
                if (byType != 0) return byType;
                int byName = string.CompareOrdinal(a.Card.displayName, b.Card.displayName);
                if (byName != 0) return byName;
                if (a.Gem == b.Gem) return 0;
                if (a.Gem == null) return -1;
                if (b.Gem == null) return 1;
                return string.CompareOrdinal(a.Gem.displayName, b.Gem.displayName);
            }
        }


        void CompleteLevel()
        {
            phase = Phase.None;
            if (queue.Complete()) StartLevel();
            else Close();
        }

        void Close()
        {
            IsOpen = false;
            phase = Phase.None;
            HideChoice();
            if (shop.IsVisible) shop.Hide();
            if (gemScreen != null && gemScreen.IsVisible) gemScreen.Hide();
            if (removalScreen != null && removalScreen.IsVisible) removalScreen.Hide();
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
