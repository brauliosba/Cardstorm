using Cardstorm.Cards;
using Cardstorm.Core;
using Cardstorm.Player;
using Cardstorm.UI;
using UnityEngine;

namespace Cardstorm.Progression
{
    // Pausa la partida en cada subida de nivel y muestra la tienda; al cerrar la última, la reanuda.
    public class LevelUpController : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] GameClock clock;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerHealth health;
        [SerializeField] PlayerCards cards;
        [SerializeField] ShopScreen shop;
        [SerializeField] ShopCatalog catalog;

        [Tooltip("Invulnerabilidad al volver a la partida, para no recibir un golpe nada más cerrar la tienda.")]
        [SerializeField, Min(0f)] float resumeInvulnerability = 0.5f;

        LevelUpQueue queue;
        ShopOffer<CardDefinition> offer;
        float previousTimeScale = 1f;

        public bool IsOpen { get; private set; }
        public LevelUpQueue Queue => queue;
        public ShopOffer<CardDefinition> Offer => offer;

        double Now => clock != null ? clock.Elapsed : Time.timeSinceLevelLoad;

        void Awake()
        {
            if (run == null) run = FindFirstObjectByType<RunManager>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
            if (shop == null) shop = FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
            if (catalog == null) Debug.LogError("LevelUpController no tiene ShopCatalog: la tienda saldrá vacía.", this);
        }

        void OnEnable()
        {
            RunEvents.LevelUp += OnLevelUp;
            if (shop != null)
            {
                shop.BuyPressed += OnBuy;
                shop.RefreshPressed += OnRefresh;
                shop.ContinuePressed += OnContinue;
            }
        }

        void OnDisable()
        {
            RunEvents.LevelUp -= OnLevelUp;
            if (shop != null)
            {
                shop.BuyPressed -= OnBuy;
                shop.RefreshPressed -= OnRefresh;
                shop.ContinuePressed -= OnContinue;
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
        }

        void OnLevelUp(double time, int level)
        {
            EnsureCreated();
            queue.Enqueue(level);

            if (IsOpen) shop.UpdateQueue(queue.Position, queue.Total);
            else Open();
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
            shop.Show(queue.CurrentLevel, queue.Position, queue.Total, queue.CurrentOffersRemoval, offer);
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

        void OnContinue()
        {
            if (!IsOpen) return;

            if (queue.Complete()) ShowCurrent();
            else Close();
        }

        void Close()
        {
            IsOpen = false;
            shop.Hide();
            Time.timeScale = previousTimeScale;

            // La derrota también usa Paused: si el jugador ha muerto, el reloj sigue parado.
            if (health == null || !health.IsDead)
            {
                clock.Paused = false;
                if (health != null) health.GrantInvulnerability(resumeInvulnerability);
            }
            input.SetGameplayEnabled(true);
        }
    }
}
