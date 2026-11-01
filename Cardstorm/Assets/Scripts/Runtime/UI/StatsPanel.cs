using System.Collections.Generic;
using Cardstorm.Player;
using Cardstorm.Progression;
using DG.Tweening;
using UnityEngine;

namespace Cardstorm.UI
{
    // Panel de estadísticas del personaje. Se abre y se cierra con su botón (Tab o LB) y sale de detrás
    // de la barra inferior hacia arriba, pegado a la izquierda.
    public class StatsPanel : MonoBehaviour
    {
        [SerializeField] PlayerStatsBinder player;
        [SerializeField] PlayerInputReader input;

        [Tooltip("El panel que se desliza. Anclado abajo a la izquierda, con el pivote abajo.")]
        [SerializeField] RectTransform panel;
        [Tooltip("Primera línea; se copia para las demás.")]
        [SerializeField] StatRowView rowTemplate;

        [Tooltip("Altura del borde superior de la barra inferior: el panel abierto queda justo encima.")]
        [SerializeField] float barTop = 288f;
        [SerializeField, Min(0f)] float gap = 8f;
        [Tooltip("Segundos del deslizamiento, en tiempo real.")]
        [SerializeField, Min(0f)] float slideTime = 0.2f;

        readonly List<StatRowView> rows = new();
        PlayerStats stats;
        Tween slide;
        bool dirty;

        public bool IsOpen { get; private set; }

        float ShownY => barTop + gap;

        // Cerrado, el borde superior queda a ras de la barra, que lo tapa.
        float HiddenY => barTop - panel.rect.height;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerStatsBinder>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
        }

        void Start()
        {
            stats = player != null ? player.Stats : null;
            if (stats != null) stats.Changed += OnStatChanged;

            BuildRows();
            dirty = true;
            SetY(HiddenY);
        }

        void OnEnable()
        {
            if (input != null) input.StatsPressed += Toggle;
        }

        void OnDisable()
        {
            if (input != null) input.StatsPressed -= Toggle;
        }

        void OnDestroy()
        {
            slide?.Kill();
            if (stats != null) stats.Changed -= OnStatChanged;
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            if (IsOpen && dirty) Refresh();

            slide?.Kill();
            float target = IsOpen ? ShownY : HiddenY;
            if (slideTime <= 0f)
            {
                SetY(target);
                return;
            }
            slide = DOTween.To(() => panel.anchoredPosition.y, SetY, target, slideTime)
                .SetEase(IsOpen ? Ease.OutCubic : Ease.InCubic)
                .SetUpdate(true)
                .SetLink(panel.gameObject);
        }

        void LateUpdate()
        {
            if (IsOpen && dirty) Refresh();
        }

        void OnStatChanged(StatType stat) => dirty = true;

        void BuildRows()
        {
            rows.Add(rowTemplate);
            for (int i = 1; i < StatTypes.Count; i++)
                rows.Add(Instantiate(rowTemplate, rowTemplate.transform.parent));
            for (int i = 0; i < StatTypes.Count; i++)
            {
                rows[i].name = $"Row_{(StatType)i}";
                rows[i].SetName(StatFormat.Label((StatType)i));
            }
        }

        // Solo con el panel abierto y cuando alguna carta de poder ha cambiado algo.
        void Refresh()
        {
            dirty = false;
            if (stats == null) return;
            for (int i = 0; i < StatTypes.Count; i++)
            {
                var stat = (StatType)i;
                rows[i].SetValue(StatFormat.Value(stat, stats.Value(stat), stats.Base(stat)), stats.IsCapped(stat));
            }
        }

        void SetY(float y) => panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, y);
    }
}
