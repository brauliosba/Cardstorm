using System.Collections.Generic;
using Cardstorm.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Energía en segmentos, uno por unidad, con el valor «N / Max» debajo.
    public class EnergyView : MonoBehaviour
    {
        [SerializeField] PlayerCards cards;

        [Tooltip("Segmento de ejemplo (desactivado). Su primer hijo es el relleno.")]
        [SerializeField] RectTransform segmentTemplate;

        [SerializeField] RectTransform segmentContainer;
        [SerializeField] TMP_Text valueText;

        readonly List<Image> fills = new();
        int shownValue = -1;
        int shownMax = -1;

        void Awake()
        {
            if (cards == null) cards = FindFirstObjectByType<PlayerCards>();
            segmentTemplate.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (cards == null || cards.Energy == null) return;

            var energy = cards.Energy;
            int max = Mathf.Max(1, Mathf.CeilToInt(energy.Max - 1e-4f));
            if (max != fills.Count) BuildSegments(max);

            float current = energy.Current;
            for (int i = 0; i < fills.Count; i++)
                fills[i].fillAmount = Mathf.Clamp01(current - i);

            int value = Mathf.FloorToInt(current + 1e-4f);
            if (value != shownValue || max != shownMax)
            {
                shownValue = value;
                shownMax = max;
                valueText.SetText("{0} / {1}", value, max);
            }
        }

        // Solo cuando cambia la energía máxima.
        void BuildSegments(int count)
        {
            while (fills.Count < count)
            {
                var segment = Instantiate(segmentTemplate, segmentContainer);
                segment.name = $"Segment{fills.Count}";
                segment.gameObject.SetActive(true);
                fills.Add(segment.GetChild(0).GetComponent<Image>());
            }
            while (fills.Count > count)
            {
                var last = fills[fills.Count - 1];
                fills.RemoveAt(fills.Count - 1);
                Destroy(last.transform.parent.gameObject);
            }
        }
    }
}
