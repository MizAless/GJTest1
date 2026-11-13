using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

internal static class IntermissionWidgets
{
    public static Color Gold => new Color(1, 0.76f, 0.4f);
    public static Color Paper => new Color(0.94f, 0.89f, 0.79f);
    public static Color Card => new Color(0.16f, 0.115f, 0.08f);

    public static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Text Label(Transform parent, string name, Vector2 min, Vector2 max, int size, string value = "")
    {
        Text text = Rect(parent, name, min, max).gameObject.AddComponent<Text>();
        text.font = PixelUiTheme.Font;
        text.fontSize = size;
        text.text = value;
        text.color = Paper;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    public static Button Button(Transform parent, string name, Vector2 min, Vector2 max,
        string title, UnityAction action)
    {
        RectTransform rect = Rect(parent, name, min, max);
        rect.gameObject.AddComponent<Image>().color = Card;
        Button button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        Text text = Label(rect, "Label", new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f), 18, title);
        text.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    public static void Select(Button button, bool selected)
    {
        button.GetComponent<Image>().color = selected ? new Color(0.34f, 0.23f, 0.12f) : Card;
        button.GetComponentInChildren<Text>().color = selected ? Gold : Paper;
    }
}
