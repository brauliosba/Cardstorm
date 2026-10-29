using System;
using Cardstorm.Core;
using Cardstorm.Player;
using UnityEngine;

namespace Cardstorm.Cards
{
    // Mano, energía y mazo del jugador. Juega la carta de la ranura pulsada si hay energía.
    [RequireComponent(typeof(CardCaster), typeof(PlayerInputReader), typeof(PlayerHealth))]
    public class PlayerCards : MonoBehaviour
    {
        [SerializeField] DeckDefinition startingDeck;
        [SerializeField] GameClock clock;

        [Header("Mano")]
        [SerializeField, Min(1)] int handSize = 4;

        [Tooltip("Segundos desde que la carta entra en la ranura hasta que se puede jugar.")]
        [SerializeField, Min(0f)] float drawTime = 0.5f;

        [Tooltip("Segundos que tarda en barajarse el descarte cuando el mazo se agota.")]
        [SerializeField, Min(0f)] float reshuffleTime = 1f;

        [Tooltip("0 = aleatoria en cada partida; otro valor repite el mismo orden.")]
        [SerializeField] int seed;

        [Header("Energía")]
        [SerializeField, Min(1f)] float maxEnergy = 10f;

        [Tooltip("Energía por segundo.")]
        [SerializeField, Min(0f)] float energyRegen = 1f;

        [SerializeField, Min(0f)] float startingEnergy = 10f;

        CardCaster caster;
        PlayerInputReader input;
        PlayerHealth health;

        public Energy Energy { get; private set; }
        public CardCycle<CardInstance> Cycle { get; private set; }
        public int CardsPlayed { get; private set; }

        public event Action<int, CardInstance> CardPlayed;
        public event Action<int, PlayResult> PlayFailed;

        bool CanAct => !health.IsDead && (clock == null || !clock.Paused);

        void Awake()
        {
            caster = GetComponent<CardCaster>();
            input = GetComponent<PlayerInputReader>();
            health = GetComponent<PlayerHealth>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();

            if (startingDeck == null)
                Debug.LogError("PlayerCards no tiene mazo inicial.", this);

            int actualSeed = seed != 0 ? seed : Environment.TickCount;
            Energy = new Energy(maxEnergy, energyRegen, startingEnergy);
            Cycle = new CardCycle<CardInstance>(
                startingDeck != null ? startingDeck.Build() : new(), handSize, drawTime, reshuffleTime, actualSeed);
        }

        void OnEnable() => input.CardPressed += OnCardPressed;

        void OnDisable() => input.CardPressed -= OnCardPressed;

        void Update()
        {
            if (!CanAct) return;

            float dt = Time.deltaTime;
            Energy.Tick(dt);
            Cycle.Tick(dt);
        }

        void OnCardPressed(int slot)
        {
            if (!CanAct || slot < 0 || slot >= Cycle.HandSize) return;

            var result = Cycle.TryPlay(slot, Energy, out var card);
            if (result != PlayResult.Played)
            {
                PlayFailed?.Invoke(slot, result);
                return;
            }

            caster.Cast(card.Definition);
            CardsPlayed++;
            CardPlayed?.Invoke(slot, card);
        }
    }
}
