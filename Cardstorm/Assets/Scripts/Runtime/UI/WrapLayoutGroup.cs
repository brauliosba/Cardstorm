using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Layout group con la misma lógica que HorizontalLayoutGroup, pero que envuelve
    // los hijos en filas nuevas (N filas, no solo 2) cuando no entran en el ancho disponible.
    [AddComponentMenu("Layout/Wrap Layout Group", 151)]
    public class WrapLayoutGroup : LayoutGroup
    {
        [SerializeField] protected float m_SpacingX = 0f;
        public float SpacingX { get => m_SpacingX; set => SetProperty(ref m_SpacingX, value); }

        [SerializeField] protected float m_SpacingY = 0f;
        public float SpacingY { get => m_SpacingY; set => SetProperty(ref m_SpacingY, value); }

        [SerializeField] protected bool m_ChildControlWidth = true;
        public bool ChildControlWidth { get => m_ChildControlWidth; set => SetProperty(ref m_ChildControlWidth, value); }

        [SerializeField] protected bool m_ChildControlHeight = true;
        public bool ChildControlHeight { get => m_ChildControlHeight; set => SetProperty(ref m_ChildControlHeight, value); }

        [SerializeField] protected bool m_ChildForceExpandWidth = false;
        public bool ChildForceExpandWidth { get => m_ChildForceExpandWidth; set => SetProperty(ref m_ChildForceExpandWidth, value); }

        [SerializeField] protected bool m_ChildForceExpandHeight = false;
        public bool ChildForceExpandHeight { get => m_ChildForceExpandHeight; set => SetProperty(ref m_ChildForceExpandHeight, value); }

        [SerializeField] protected bool m_ChildScaleWidth = false;
        public bool ChildScaleWidth { get => m_ChildScaleWidth; set => SetProperty(ref m_ChildScaleWidth, value); }

        [SerializeField] protected bool m_ChildScaleHeight = false;
        public bool ChildScaleHeight { get => m_ChildScaleHeight; set => SetProperty(ref m_ChildScaleHeight, value); }

        private class Row
        {
            public readonly List<RectTransform> children = new List<RectTransform>();
            public readonly List<float> minWidths = new List<float>();
            public readonly List<float> preferredWidths = new List<float>();
            public readonly List<float> flexibleWidths = new List<float>();
            public float totalMinWidth;
            public float totalPreferredWidth;
            public float preferredHeight;
        }

        private readonly List<Row> m_Rows = new List<Row>();

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CalculateRows();

            float minWidth = 0f;
            float preferredWidth = 0f;
            for (int i = 0; i < m_Rows.Count; i++)
            {
                minWidth = Mathf.Max(minWidth, m_Rows[i].totalMinWidth);
                preferredWidth = Mathf.Max(preferredWidth, m_Rows[i].totalPreferredWidth);
            }

            SetLayoutInputForAxis(minWidth + padding.horizontal, preferredWidth + padding.horizontal, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float totalHeight = padding.vertical;
            for (int i = 0; i < m_Rows.Count; i++)
            {
                if (i > 0)
                    totalHeight += m_SpacingY;
                totalHeight += m_Rows[i].preferredHeight;
            }

            SetLayoutInputForAxis(totalHeight, totalHeight, -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
            float availableWidth = rectTransform.rect.width - padding.horizontal;

            for (int r = 0; r < m_Rows.Count; r++)
            {
                Row row = m_Rows[r];

                float totalFlexible = 0f;
                for (int i = 0; i < row.flexibleWidths.Count; i++)
                    totalFlexible += row.flexibleWidths[i];

                float surplusSpace = availableWidth - row.totalPreferredWidth;
                float pos = padding.left;
                float itemFlexibleMultiplier = 0f;

                if (surplusSpace > 0f)
                {
                    if (totalFlexible <= 0f)
                        pos = padding.left + surplusSpace * GetAlignmentOnAxis(0);
                    else
                        itemFlexibleMultiplier = surplusSpace / totalFlexible;
                }

                float minMaxLerp = 0f;
                if (row.totalPreferredWidth != row.totalMinWidth)
                    minMaxLerp = Mathf.Clamp01((availableWidth - row.totalMinWidth) / (row.totalPreferredWidth - row.totalMinWidth));

                for (int i = 0; i < row.children.Count; i++)
                {
                    RectTransform child = row.children[i];
                    float scaleFactor = m_ChildScaleWidth ? child.localScale.x : 1f;

                    float childSize = Mathf.Lerp(row.minWidths[i], row.preferredWidths[i], minMaxLerp);
                    childSize += row.flexibleWidths[i] * itemFlexibleMultiplier;

                    if (i > 0)
                        pos += m_SpacingX;

                    if (m_ChildControlWidth)
                    {
                        SetChildAlongAxisWithScale(child, 0, pos, childSize, scaleFactor);
                    }
                    else
                    {
                        float offsetInCell = (childSize - child.sizeDelta.x) * GetAlignmentOnAxis(0);
                        SetChildAlongAxisWithScale(child, 0, pos + offsetInCell, scaleFactor);
                    }

                    pos += childSize * scaleFactor;
                }
            }
        }

        public override void SetLayoutVertical()
        {
            float totalPreferredHeight = padding.vertical;
            for (int r = 0; r < m_Rows.Count; r++)
            {
                if (r > 0)
                    totalPreferredHeight += m_SpacingY;
                totalPreferredHeight += m_Rows[r].preferredHeight;
            }

            float surplusSpace = rectTransform.rect.height - totalPreferredHeight;
            float y = padding.top + Mathf.Max(0f, surplusSpace) * GetAlignmentOnAxis(1);

            for (int r = 0; r < m_Rows.Count; r++)
            {
                Row row = m_Rows[r];
                if (r > 0)
                    y += m_SpacingY;

                float rowHeight = row.preferredHeight;

                for (int i = 0; i < row.children.Count; i++)
                {
                    RectTransform child = row.children[i];
                    GetChildSizes(child, 1, m_ChildControlHeight, m_ChildForceExpandHeight, out _, out float preferred, out _);
                    float scaleFactor = m_ChildScaleHeight ? child.localScale.y : 1f;

                    float childSize = m_ChildForceExpandHeight ? rowHeight : preferred;
                    float offset = (rowHeight - childSize) * GetAlignmentOnAxis(1);

                    if (m_ChildControlHeight)
                        SetChildAlongAxisWithScale(child, 1, y + offset, childSize, scaleFactor);
                    else
                        SetChildAlongAxisWithScale(child, 1, y + offset, scaleFactor);
                }

                y += rowHeight;
            }
        }

        private void CalculateRows()
        {
            m_Rows.Clear();

            float availableWidth = rectTransform.rect.width - padding.horizontal;
            bool unbounded = availableWidth <= 0f;

            Row currentRow = new Row();

            for (int i = 0; i < rectChildren.Count; i++)
            {
                RectTransform child = rectChildren[i];
                GetChildSizes(child, 0, m_ChildControlWidth, m_ChildForceExpandWidth, out float min, out float preferred, out float flexible);

                float spacingBefore = currentRow.children.Count > 0 ? m_SpacingX : 0f;
                bool fits = unbounded || currentRow.children.Count == 0 ||
                    (currentRow.totalPreferredWidth + spacingBefore + preferred) <= availableWidth;

                if (!fits)
                {
                    m_Rows.Add(currentRow);
                    currentRow = new Row();
                    spacingBefore = 0f;
                }

                currentRow.children.Add(child);
                currentRow.minWidths.Add(min);
                currentRow.preferredWidths.Add(preferred);
                currentRow.flexibleWidths.Add(flexible);
                currentRow.totalMinWidth += spacingBefore + min;
                currentRow.totalPreferredWidth += spacingBefore + preferred;

                GetChildSizes(child, 1, m_ChildControlHeight, m_ChildForceExpandHeight, out _, out float preferredHeight, out _);
                currentRow.preferredHeight = Mathf.Max(currentRow.preferredHeight, preferredHeight);
            }

            if (currentRow.children.Count > 0)
                m_Rows.Add(currentRow);
        }

        private void GetChildSizes(RectTransform child, int axis, bool controlSize, bool childForceExpand,
            out float min, out float preferred, out float flexible)
        {
            if (!controlSize)
            {
                min = child.sizeDelta[axis];
                preferred = min;
                flexible = 0f;
            }
            else
            {
                min = LayoutUtility.GetMinSize(child, axis);
                preferred = LayoutUtility.GetPreferredSize(child, axis);
                flexible = LayoutUtility.GetFlexibleSize(child, axis);
            }

            if (childForceExpand)
                flexible = Mathf.Max(flexible, 1f);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetDirty();
        }
#endif
    }
}
