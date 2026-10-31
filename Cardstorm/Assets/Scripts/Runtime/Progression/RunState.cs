using System;
using System.Collections.Generic;

namespace Cardstorm.Progression
{
    // Estado de la partida: nivel, experiencia, oro, copias compradas y eliminaciones.
    public class RunState
    {
        // Margen para que 10 × 0,1 de oro cuenten como 1 aunque la suma dé 0,99999.
        const float GoldEpsilon = 1e-4f;

        readonly Dictionary<string, int> copies = new();
        float goldFraction;

        public RunState(ProgressionRules rules, PlayerStats stats, int seed)
        {
            Rules = rules;
            Stats = stats;
            Levels = new LevelProgression(rules);
            Random = new Random(seed);
            Gold = rules.startingGold;
        }

        public ProgressionRules Rules { get; }
        public PlayerStats Stats { get; }
        public LevelProgression Levels { get; }
        public Random Random { get; }

        public int Level => Levels.Level;
        public int Gold { get; private set; }
        public int GoldEarned { get; private set; }
        public int GoldSpent { get; private set; }
        public int Removals { get; private set; }

        // Aplica el multiplicador de experiencia. Devuelve cuántos niveles se han subido.
        public int AddXp(float xp) => Levels.AddXp(xp * Stats.Value(StatType.XpGain));

        // Aplica el multiplicador de oro y guarda la fracción para la siguiente moneda.
        // Devuelve el oro que se ha sumado de verdad.
        public int AddGold(int amount)
        {
            if (amount <= 0) return 0;

            goldFraction += amount * Stats.Value(StatType.GoldGain);
            int whole = (int)Math.Floor(goldFraction + GoldEpsilon);
            goldFraction = Math.Max(0f, goldFraction - whole);
            Gold += whole;
            GoldEarned += whole;
            return whole;
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || amount > Gold) return false;
            Gold -= amount;
            GoldSpent += amount;
            return true;
        }

        public int CopiesOf(string cardId) =>
            cardId != null && copies.TryGetValue(cardId, out int n) ? n : 0;

        public void AddCopy(string cardId)
        {
            if (cardId == null) return;
            copies[cardId] = CopiesOf(cardId) + 1;
        }

        public void RegisterRemoval() => Removals++;
    }
}
