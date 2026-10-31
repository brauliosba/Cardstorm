using System.Collections.Generic;

namespace Cardstorm.Progression
{
    // Subidas de nivel pendientes. Cada una abre una tienda; mientras quede alguna, el juego sigue en pausa.
    // Total y Position dan el «1 de 3» de las subidas encadenadas.
    public class LevelUpQueue
    {
        readonly ProgressionRules rules;
        readonly Queue<int> levels = new();

        public LevelUpQueue(ProgressionRules rules) => this.rules = rules;

        public bool IsEmpty => levels.Count == 0;
        public int Count => levels.Count;

        // Nivel de la tienda que se está mostrando (0 si no hay ninguna).
        public int CurrentLevel => levels.Count > 0 ? levels.Peek() : 0;

        // Tiendas de la tanda actual (se reinicia al vaciarse la cola) y posición de la actual, desde 1.
        public int Total { get; private set; }
        public int Position => IsEmpty ? 0 : Total - levels.Count + 1;

        // Veces que se ha refrescado la tienda actual; vuelve a 0 en cada tienda.
        public int Refreshes { get; private set; }

        public bool CurrentOffersRemoval => !IsEmpty && OffersRemoval(CurrentLevel);

        public bool OffersRemoval(int level) =>
            level > 1 && rules.removalEveryLevels > 0 && level % rules.removalEveryLevels == 0;

        public void Enqueue(int level)
        {
            levels.Enqueue(level);
            Total++;
        }

        public void RegisterRefresh() => Refreshes++;

        // Cierra la tienda actual. Devuelve true si queda otra por mostrar.
        public bool Complete()
        {
            if (IsEmpty) return false;

            levels.Dequeue();
            Refreshes = 0;
            if (IsEmpty) Total = 0;
            return !IsEmpty;
        }
    }
}
