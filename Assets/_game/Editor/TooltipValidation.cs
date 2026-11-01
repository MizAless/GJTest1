using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TooltipValidation
{
    private static double _started;
    private static bool _ready;
    private static bool _previewPassed;

    public static void Run()
    {
        try
        {
            TooltipAuthoring.CreateScene();
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged += OnPlayMode;
            _started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _ready = true;
        }
    }

    private static void Check()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - _started > 90)
            {
                throw new Exception("Tooltip Play Mode timed out.");
            }

            if (!_ready)
            {
                return;
            }

            if (!_previewPassed)
            {
                Validate();
                _previewPassed = true;
                EditorSceneManager.LoadSceneInPlayMode("Assets/_game/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }

            if (RunUI.Instance == null)
            {
                return;
            }

            TooltipTrigger[] targets = Object.FindObjectsByType<TooltipTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert(targets.Count(target => target.name.StartsWith("Socket ")) == 6,
                "RunUI must expose six HUD cartridge tooltips.");
            TooltipTrigger socket = targets.First(target => target.name == "Socket 1");
            socket.OnPointerEnter(new PointerEventData(EventSystem.current) { position = Input.mousePosition });
            TooltipPresenter presenter = TooltipPresenter.For(socket.GetComponentInParent<Canvas>());
            Assert(presenter.IsVisible, "Actual game HUD did not show the tooltip.");
            Assert(presenter.View.GetComponentsInChildren<Text>().Any(label => label.text == RunDirector.Instance.Drum.Cartridges[0].Name), "HUD tooltip has the wrong cartridge.");
            socket.OnPointerExit(null);
            Assert(!presenter.IsVisible, "Actual game HUD did not hide the tooltip.");
            File.AppendAllText("Artifacts/tooltip-validation.txt", " Game scene: six HUD slots, actual cartridge data and hover enter/exit passed.");
            Debug.Log("TOOLTIP_GAME_UI_VALIDATION_PASSED");
            EditorApplication.update -= Check;
            EditorApplication.playModeStateChanged += ExitAfterPlayMode;
            EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void Validate()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        TooltipPresenter presenter = TooltipPresenter.For(canvas);
        var content = new TooltipContent
        {
            Title = "Ледяная пуля", Subtitle = "Редкий", Accent = new Color(0.5f, 0.8f, 0.9f),
            Tags = new[] { "снаряды", "холод", "пробитие", "область", "постепенный урон", "перезарядка" },
            Intrinsic = "Накладывает +1 холод при попадании.",
            Properties = new[] { "+1 к пробитию", "×1,5 урона снарядов" },
            Triggers = new[] { "Перезарядка → усилить следующий патрон" }
        };
        var owner = new object();
        Time.timeScale = 0;
        Vector2[] pointers =
        {
            new Vector2(1, 1), new Vector2(Screen.width - 1, 1),
            new Vector2(1, Screen.height - 1), new Vector2(Screen.width - 1, Screen.height - 1),
            new Vector2(Screen.width * 0.7f, Screen.height * 0.7f)
        };
        foreach (Vector2 pointer in pointers)
        {
            presenter.Show(owner, content, pointer);
            presenter.UpdatePosition(pointer, 0, true);
            AssertInside(presenter.View);
        }

        Rect bounds = new Rect(0, 0, 1280, 720);
        Vector2 size = new Vector2(370, 300);
        Assert(TooltipPresenter.CalculateTarget(new Vector2(800, 400), size, bounds, 20).x < 800, "Default side must be left.");
        Assert(TooltipPresenter.CalculateTarget(new Vector2(50, 400), size, bounds, 20).x > 50, "Must flip to the right.");

        Vector2 oldPosition = presenter.View.Rect.anchoredPosition;
        presenter.UpdatePosition(new Vector2(Screen.width * 0.9f, Screen.height * 0.7f), 0.01f);
        Vector2 interpolated = presenter.View.Rect.anchoredPosition;
        presenter.UpdatePosition(new Vector2(Screen.width * 0.9f, Screen.height * 0.7f), 0, true);
        Vector2 target = presenter.View.Rect.anchoredPosition;
        Assert(Vector2.Distance(oldPosition, interpolated) > 0.01f, "Follow must advance even when paused.");
        Assert(Vector2.Distance(interpolated, target) > 0.01f, "Follow must interpolate instead of snapping.");

        int tagCount = presenter.View.GetComponentsInChildren<LayoutElement>(true).Length;
        presenter.Show(owner, content, pointers[4]);
        Assert(tagCount == presenter.View.GetComponentsInChildren<LayoutElement>(true).Length, "Tags must reuse their pool.");
        Text[] labels = presenter.View.GetComponentsInChildren<Text>();
        Assert(labels.Any(label => label.text == content.Intrinsic), "Intrinsic missing.");
        Assert(labels.Any(label => label.text == "ВРОЖДЁННОЕ СВОЙСТВО"), "Intrinsic heading missing.");
        Assert(labels.Any(label => label.text == "Закреплено за типом пули · не изменяется"),
            "Intrinsic must explain its immutable origin.");
        Transform intrinsicBlock = presenter.View.transform.Find("Intrinsic");
        Assert(intrinsicBlock != null && intrinsicBlock.GetComponent<Image>() != null,
            "Intrinsic must have a separate background.");
        Assert(labels.Any(label => label.text == content.Triggers[0]), "Trigger missing.");
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, presenter.View.transform.position)
        }, results);
        Assert(results.All(result => !result.gameObject.transform.IsChildOf(presenter.View.transform)), "Tooltip blocks pointer input.");

        object newerOwner = new object();
        presenter.Show(newerOwner, content, pointers[4]);
        presenter.Hide(owner);
        Assert(presenter.IsVisible, "An old owner's exit hid the new tooltip.");
        presenter.Hide(newerOwner);
        Assert(!presenter.IsVisible, "Tooltip did not hide.");

        TooltipTrigger trigger = Object.FindObjectsByType<TooltipTrigger>(FindObjectsSortMode.None)[0];
        trigger.OnPointerEnter(new PointerEventData(EventSystem.current) { position = pointers[4] });
        Assert(presenter.IsVisible, "Pointer enter must display content.");
        trigger.gameObject.SetActive(false);
        Assert(!presenter.IsVisible, "Disabled target left a stale tooltip.");
        trigger.gameObject.SetActive(true);

        presenter.Show(owner, new TooltipContent { Title = "Минимальный предмет" }, pointers[4]);
        Assert(presenter.View.GetComponentsInChildren<Text>().All(label => !label.text.Contains("Текст")), "Empty section is visible.");
        var longContent = new TooltipContent
        {
            Title = "Длинное описание",
            Properties = Enumerable.Repeat("Очень длинное описание свойства, которое должно переноситься и оставаться на экране.", 20).ToArray()
        };
        presenter.Show(owner, longContent, pointers[0]);
        presenter.UpdatePosition(pointers[0], 0, true);
        AssertInside(presenter.View);

        var ammo = new AmmoItem { Name = "Тест", Properties = new AmmoProperties { Chill = 1 }, Trigger = AmmoTrigger.ReloadNext };
        TooltipContent actual = AmmoTooltipContent.Create(ammo);
        Assert(string.IsNullOrEmpty(actual.Intrinsic), "Current combat affixes must not become fabricated intrinsics.");
        Assert(actual.Triggers.Length == 1 && actual.Tags.Contains("холод"), "Ammo adapter lost tags or triggers.");

        presenter.Show(owner, content, pointers[4]);
        presenter.UpdatePosition(pointers[4], 0, true);
        Capture(canvas);
        Time.timeScale = 1;
        Directory.CreateDirectory("Artifacts");
        File.WriteAllText("Artifacts/tooltip-validation.txt", "PASS: Play Mode; left/right placement; four corners; paused exponential lerp; long content; tag pool; input passthrough; pointer enter/disable; owner handoff; ammo adapter.");
        Debug.Log("TOOLTIP_VALIDATION_PASSED");
    }

    private static void AssertInside(TooltipView view)
    {
        var corners = new Vector3[4];
        view.Rect.GetWorldCorners(corners);
        Rect bounds = Screen.safeArea;
        foreach (Vector3 corner in corners)
        {
            Assert(corner.x >= bounds.xMin - 1 && corner.x <= bounds.xMax + 1
                && corner.y >= bounds.yMin - 1 && corner.y <= bounds.yMax + 1, "Tooltip escaped the screen: " + corner);
        }
    }

    private static void Capture(Canvas canvas)
    {
        Camera camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        TooltipPresenter presenter = canvas.GetComponent<TooltipPresenter>();
        // Capture framing only. Cursor placement was verified in the real Game View above.
        presenter.View.Rect.localScale = Vector3.one;
        presenter.View.Rect.anchoredPosition = new Vector2(140, 20);
        camera.Render();
        RenderTexture.active = target;
        var capture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        capture.Apply();
        Directory.CreateDirectory("Artifacts");
        File.WriteAllBytes("Artifacts/tooltip-preview.png", capture.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.Destroy(capture);
        Object.Destroy(target);
    }

    private static void ExitAfterPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.Exit(0);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static void Fail(Exception exception)
    {
        Debug.LogException(exception);
        EditorApplication.update -= Check;
        EditorApplication.Exit(1);
    }
}
