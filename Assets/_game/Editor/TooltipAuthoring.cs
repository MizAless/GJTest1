using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class TooltipAuthoring
{
    public const string PrefabPath = "Assets/_game/Resources/UI/ItemTooltip.prefab";
    public const string ScenePath = "Assets/_game/Scenes/TooltipPreview.unity";
    private static readonly Color Paper = new Color(0.93f, 0.89f, 0.77f);
    private static readonly Color Gold = new Color(0.79f, 0.64f, 0.38f);
    private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    [MenuItem("Tools/Prototype/Tooltips/Create or update tooltip prefab")]
    public static void CreatePrefab()
    {
        Directory.CreateDirectory("Assets/_game/Resources/UI");
        AssetDatabase.Refresh();
        RectTransform root = Rect(null, "ItemTooltip");
        root.sizeDelta = new Vector2(370, 100);
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        Image background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.045f, 0.055f, 0.047f, 0.99f);
        background.raycastTarget = false;
        Outline border = root.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(0.37f, 0.40f, 0.28f, 1);
        border.effectDistance = new Vector2(1, -1);
        Shadow shadow = root.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(5, -6);
        VerticalLayoutGroup layout = Vertical(root);
        layout.padding = new RectOffset(18, 18, 16, 18);
        layout.spacing = 10;
        ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform accent = Rect(root, "Rarity rule");
        Image accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.color = Gold;
        accentImage.raycastTarget = false;
        Height(accent, 2);
        Text title = Label(root, "Title", "Ледяная пуля", 25, Paper);
        title.fontStyle = FontStyle.Bold;
        Text subtitle = Label(root, "Rarity", "Редкий", 14, Gold);

        RectTransform tags = Rect(root, "Tags");
        tags.gameObject.AddComponent<TooltipTagLayout>();
        RectTransform tagTemplate = Rect(tags, "Tag template");
        tagTemplate.gameObject.AddComponent<LayoutElement>();
        Image tagBackground = tagTemplate.gameObject.AddComponent<Image>();
        tagBackground.color = new Color(0.17f, 0.21f, 0.17f);
        tagBackground.raycastTarget = false;
        Text tagText = Label(tagTemplate, "Label", "холод", 13, Paper);
        Stretch(tagText.rectTransform, 8, 3);
        tagText.alignment = TextAnchor.MiddleCenter;
        tagText.horizontalOverflow = HorizontalWrapMode.Overflow;
        tagTemplate.gameObject.SetActive(false);

        Text intrinsic = IntrinsicSection(root);
        Text properties = Section(root, "Properties", "СВОЙСТВА", Gold);
        Text triggers = Section(root, "Triggers", "ТРИГГЕРЫ", new Color(0.77f, 0.67f, 0.88f));
        TooltipView view = root.gameObject.AddComponent<TooltipView>();
        var serialized = new SerializedObject(view);
        Set(serialized, "_title", title);
        Set(serialized, "_subtitle", subtitle);
        Set(serialized, "_accent", accentImage);
        Set(serialized, "_tags", tags);
        Set(serialized, "_tagTemplate", tagTemplate);
        Set(serialized, "_intrinsicSection", intrinsic.transform.parent.gameObject);
        Set(serialized, "_intrinsic", intrinsic);
        Set(serialized, "_propertiesSection", properties.transform.parent.gameObject);
        Set(serialized, "_properties", properties);
        Set(serialized, "_triggersSection", triggers.transform.parent.gameObject);
        Set(serialized, "_triggers", triggers);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root.gameObject);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Prototype/Tooltips/Create preview scene")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        CreatePrefab();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Camera camera = new GameObject("Preview camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.09f, 0.07f, 0.05f);
        RectTransform root = Rect(null, "Tooltip laboratory");
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        root.gameObject.AddComponent<GraphicRaycaster>();
        root.gameObject.AddComponent<TooltipPresenter>();
        Text title = Label(root, "Heading", "ПАТРОНЫ · ПОДСКАЗКИ", 28, Paper);
        Place(title.rectTransform, new Vector2(0, 285), new Vector2(900, 45));
        title.alignment = TextAnchor.MiddleCenter;
        Text hint = Label(root, "Instructions", "Наведите курсор на патрон. Слева нет места — подсказка появится справа.", 17, Gold);
        Place(hint.rectTransform, new Vector2(0, 235), new Vector2(1100, 35));
        hint.alignment = TextAnchor.MiddleCenter;

        string[] names = { "Обычная пуля", "Ледяная пуля", "Разгонная пуля", "Модуль «Веер»" };
        string[] sprites = { "Plain", "Ice", "Accelerator", "Fan" };
        for (int index = 0; index < names.Length; index++)
        {
            RectTransform card = Rect(root, names[index]);
            Place(card, new Vector2(-465 + index * 310, 0), new Vector2(260, 290));
            Image panel = card.gameObject.AddComponent<Image>();
            panel.color = new Color(0.14f, 0.13f, 0.10f);
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.36f, 0.31f, 0.20f);
            RectTransform art = Rect(card, "Art");
            Image image = art.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_game/Art/Bandolier/Cartridges/" + sprites[index] + ".png");
            image.preserveAspect = true;
            image.raycastTarget = false;
            Place(art, new Vector2(0, 30), new Vector2(160, 190));
            Text label = Label(card, "Name", names[index], 19, Paper);
            label.alignment = TextAnchor.MiddleCenter;
            Place(label.rectTransform, new Vector2(0, -100), new Vector2(240, 45));
            TooltipTrigger trigger = card.gameObject.AddComponent<TooltipTrigger>();
            var serialized = new SerializedObject(trigger);
            serialized.FindProperty("_content").FindPropertyRelative("Title").stringValue = names[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetPreviewContent(trigger, index);
        }

        Text footer = Label(root, "Scope", "Демонстрация структуры подсказки. Примеры имплиситов не меняют боевые правила.", 15, Gold);
        footer.alignment = TextAnchor.MiddleCenter;
        Place(footer.rectTransform, new Vector2(0, -270), new Vector2(1120, 40));
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
    }

    private static void SetPreviewContent(TooltipTrigger trigger, int index)
    {
        TooltipContent content;
        if (index == 0)
        {
            content = new TooltipContent { Title = "Обычная пуля", Subtitle = "Обычный", Tags = new[] { "снаряды" }, Properties = new[] { "10 урона при попадании." } };
        }
        else if (index == 1)
        {
            content = new TooltipContent
            {
                Title = "Ледяная пуля", Subtitle = "Редкий", Accent = new Color(0.47f, 0.80f, 0.87f),
                Tags = new[] { "снаряды", "холод", "область" },
                Intrinsic = "Накладывает <color=#A5E7EF>+1 холод</color> при попадании.",
                Properties = new[] { "+1 к пробитию", "×1,5 урона снарядов" },
                Triggers = new[] { "При перезарядке → усилить следующую пулю." }
            };
        }
        else if (index == 2)
        {
            content = new TooltipContent
            {
                Title = "Разгонная пуля", Subtitle = "Эпический", Accent = new Color(0.79f, 0.61f, 0.92f),
                Tags = new[] { "снаряды", "очередь", "перезарядка" },
                Intrinsic = "При перезарядке даёт <color=#F3D191>+2 к очереди</color> следующей пуле.",
                Properties = new[] { "×1,5 урона снарядов", "+1 к рикошету" },
                Triggers = new[] { "При выстреле → запустить эффект следующей пули." }
            };
        }
        else
        {
            content = new TooltipContent
            {
                Title = "Модуль «Веер»", Subtitle = "Для гнезда барабана", Tags = new[] { "снаряды", "огонь", "яд", "постепенный урон", "область", "перезарядка" },
                Properties = new[] { "+2 снаряда веером", "+1 поджог", "×1,5 постепенного урона" },
                Triggers = new[] { "При перезарядке → усилить два следующих гнезда.", "Эта секция поддерживает несколько триггеров." }
            };
        }

        trigger.SetContent(content);
        EditorUtility.SetDirty(trigger);
    }

    private static Text IntrinsicSection(Transform parent)
    {
        Color accent = new Color(0.76f, 0.85f, 0.77f);
        RectTransform block = Rect(parent, "Intrinsic");
        Image background = block.gameObject.AddComponent<Image>();
        background.color = new Color(0.10f, 0.15f, 0.12f, 1);
        background.raycastTarget = false;
        VerticalLayoutGroup layout = Vertical(block);
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 6;

        Text heading = Label(block, "Heading", "ВРОЖДЁННОЕ СВОЙСТВО", 13, accent);
        heading.fontStyle = FontStyle.Bold;
        Text value = Label(block, "Content", "", 18, Paper);
        Label(block, "Immutable hint", "Закреплено за типом пули · не изменяется", 12,
            new Color(0.58f, 0.68f, 0.61f));
        return value;
    }

    private static Text Section(Transform parent, string name, string heading, Color accent)
    {
        RectTransform section = Rect(parent, name);
        VerticalLayoutGroup layout = Vertical(section);
        layout.spacing = 5;
        Text label = Label(section, "Heading", heading, 11, accent);
        label.fontStyle = FontStyle.Bold;
        return Label(section, "Body", "Текст", 17, Paper);
    }

    private static VerticalLayoutGroup Vertical(RectTransform rect)
    {
        VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    private static RectTransform Rect(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchorMin = rect.anchorMax = rect.pivot;
        return rect;
    }

    private static Text Label(Transform parent, string name, string text, int size, Color color)
    {
        Text label = Rect(parent, name).gameObject.AddComponent<Text>();
        label.font = Font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.raycastTarget = false;
        label.supportRichText = true;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    private static void Height(RectTransform rect, float height)
    {
        LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private static void Stretch(RectTransform rect, float horizontal, float vertical)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Set(SerializedObject target, string name, UnityEngine.Object value)
    {
        target.FindProperty(name).objectReferenceValue = value;
    }
}
