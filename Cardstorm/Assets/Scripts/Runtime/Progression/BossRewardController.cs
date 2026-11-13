using System.Collections.Generic;
using Cardstorm.Cards;
using Cardstorm.Core;
using Cardstorm.Player;
using Cardstorm.Spawning;
using Cardstorm.UI;
using UnityEngine;

namespace Cardstorm.Progression
{
    // Al derrotar a un jefe con recompensa, pausa la partida y ofrece cartas raras al azar; la elegida va al mazo.
    // Si la tienda está abierta, espera a que se cierre.
    public class BossRewardController : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] GameClock clock;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerHealth health;
        [SerializeField] PlayerCards cards;
        [SerializeField] BossSpawner bosses;
        [SerializeField] LevelUpController levelUp;
        [SerializeField] RewardScreen screen;
        [SerializeField] ShopCatalog catalog;

        [Tooltip("Cartas raras distintas que se ofrecen, como mucho las ranuras de la pantalla.")]
        [SerializeField, Min(1)] int choices = 3;

        [Tooltip("Invulnerabilidad al volver a la partida, como al cerrar la tienda.")]
        [SerializeField, Min(0f)] float resumeInvulnerability = 0.5f;

        readonly Queue<BossSpawner.BossEntry> pending = new();
        readonly List<CardDefinition> rares = new();
        readonly List<CardDefinition> offered = new();
        float previousTimeScale = 1f;

        public bool IsOpen { get; private set; }

        double Now => clock != null ? clock.Elapsed : Time.timeSinceLevelLoad;

        void Awake()
        {
            if (run == null) run = FindFirstObjectByType<RunManager>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
            if (bosses == null) bosses = FindFirstObjectByType<BossSpawner>();
            if (levelUp == null) levelUp = FindFirstObjectByType<LevelUpController>();
            if (screen == null) screen = FindFirstObjectByType<RewardScreen>(FindObjectsInactive.Include);
            if (screen == null) Debug.LogError("BossRewardController no tiene RewardScreen: los jefes no darán carta rara.", this);
        }

        void OnEnable()
        {
            if (bosses != null) bosses.BossDefeated += OnBossDefeated;
            if (levelUp != null) levelUp.Closed += TryOpen;
            if (screen != null) screen.CardChosen += OnCardChosen;
        }

        void OnDisable()
        {
            if (bosses != null) bosses.BossDefeated -= OnBossDefeated;
            if (levelUp != null) levelUp.Closed -= TryOpen;
            if (screen != null) screen.CardChosen -= OnCardChosen;
        }

        void OnBossDefeated(BossSpawner.BossEntry entry)
        {
            if (!entry.rareReward || screen == null) return;
            pending.Enqueue(entry);
            TryOpen();
        }

        void TryOpen()
        {
            while (!IsOpen && pending.Count > 0)
            {
                if (levelUp != null && levelUp.IsOpen) return;
                if (run.Won || (health != null && health.IsDead))
                {
                    pending.Clear();
                    return;
                }

                var entry = pending.Dequeue();
                if (PickRares()) Open(entry);
                else Debug.LogWarning($"[BossRewardController] El catálogo no tiene cartas raras de acción: {entry.DisplayName} no da recompensa.", this);
            }
        }

        // Hasta «choices» raras distintas, al azar con el azar de la partida.
        bool PickRares()
        {
            rares.Clear();
            offered.Clear();
            if (catalog != null)
                foreach (var card in catalog.actionCards)
                    if (card != null && card.rarity == CardRarity.Rare && !rares.Contains(card)) rares.Add(card);

            var random = run.State.Random;
            int count = Mathf.Min(Mathf.Min(choices, screen.SlotCount), rares.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = random.Next(i, rares.Count);
                (rares[i], rares[pick]) = (rares[pick], rares[i]);
                offered.Add(rares[i]);
            }
            return offered.Count > 0;
        }

        void Open(BossSpawner.BossEntry entry)
        {
            IsOpen = true;
            if (levelUp != null) levelUp.Held = true;
            clock.Paused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            input.SetGameplayEnabled(false);
            screen.Show(entry.DisplayName, offered);
        }

        void OnCardChosen(int slot)
        {
            if (!IsOpen || slot < 0 || slot >= offered.Count) return;

            var card = offered[slot];
            cards.Cycle.Add(new CardInstance(card));
            RunEvents.RaiseRareCardTaken(Now, card.id);
            Close();
        }

        void Close()
        {
            IsOpen = false;
            screen.Hide();
            Time.timeScale = previousTimeScale;
            if (health == null || !health.IsDead)
            {
                clock.Paused = false;
                if (health != null) health.GrantInvulnerability(resumeInvulnerability);
            }
            input.SetGameplayEnabled(true);

            // Otro jefe con recompensa antes que las tiendas que esperan.
            TryOpen();
            if (!IsOpen && levelUp != null) levelUp.Held = false;
        }
    }
}
