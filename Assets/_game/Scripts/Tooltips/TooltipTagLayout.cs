using UnityEngine;
using UnityEngine.UI;

// Small wrapping rows, unlike HorizontalLayoutGroup which cannot wrap long tag lists.
public class TooltipTagLayout : LayoutGroup
{
    [SerializeField] private Vector2 _spacing = new Vector2(6, 6);

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        SetLayoutInputForAxis(0, 0, -1, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        float height = Arrange(false);
        SetLayoutInputForAxis(height, height, -1, 1);
    }

    public override void SetLayoutHorizontal()
    {
        Arrange(true);
    }

    public override void SetLayoutVertical()
    {
        Arrange(true);
    }

    private float Arrange(bool apply)
    {
        float availableWidth = Mathf.Max(1, rectTransform.rect.width - padding.horizontal);
        float x = 0;
        float y = padding.top;
        float rowHeight = 0;
        foreach (RectTransform child in rectChildren)
        {
            float width = Mathf.Min(availableWidth, LayoutUtility.GetPreferredWidth(child));
            float height = LayoutUtility.GetPreferredHeight(child);
            if (x > 0 && x + width > availableWidth)
            {
                x = 0;
                y += rowHeight + _spacing.y;
                rowHeight = 0;
            }

            if (apply)
            {
                SetChildAlongAxis(child, 0, padding.left + x, width);
                SetChildAlongAxis(child, 1, y, height);
            }

            x += width + _spacing.x;
            rowHeight = Mathf.Max(rowHeight, height);
        }

        return y + rowHeight + padding.bottom;
    }
}
