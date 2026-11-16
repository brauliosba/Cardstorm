using System;
using System.Collections.Generic;
using Cardstorm.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Una línea de las listas del mazo: una carta con una gema (o sin ella), cuántas copias hay y cuál se elige.
    public class RemovalEntry
    {
        public CardDefinition Card;
        public GemDefinition Gem;
        public CardInstance Instance;
        public int Count;
        public bool Allowed;

        // Sustituye al tipo en la línea («Sustituye a Filo»); null para el tipo de siempre.
        public string Note;
    }

    // Líneas de una lista del mazo. La primera hace de plantilla y se copia para las demás; se reutilizan
    // entre pantallas.
    public class DeckList
    {
        readonly RemovalEntryView template;
        readonly Transform parent;
        readonly Action<int> pressed;
        readonly List<RemovalEntryView> views = new();

        public DeckList(RemovalEntryView template, Transform parent, Action<int> pressed)
        {
            this.template = template;
            this.parent = parent != null ? parent : template.transform.parent;
            this.pressed = pressed;
            Add(template);
        }

        // Solo se usan las count primeras entradas.
        public void Show(IReadOnlyList<RemovalEntry> entries, int count)
        {
            while (views.Count < count)
                Add(UnityEngine.Object.Instantiate(template, parent));

            for (int i = 0; i < views.Count; i++)
            {
                if (i < count) views[i].Show(entries[i]);
                else views[i].Hide();
            }
        }

        public void Wobble(int index)
        {
            if (index >= 0 && index < views.Count) views[index].Wobble();
        }

        // La primera línea que se puede pulsar, o null.
        public Selectable FirstSelectable()
        {
            foreach (var view in views)
            {
                var b = view.Button;
                if (b != null && b.isActiveAndEnabled && b.interactable) return b;
            }
            return null;
        }

        public void Dispose()
        {
            foreach (var view in views)
                if (view != null) view.Button.onClick.RemoveAllListeners();
        }

        void Add(RemovalEntryView view)
        {
            int index = views.Count;
            views.Add(view);
            view.Button.onClick.AddListener(() => pressed(index));
        }
    }
}
