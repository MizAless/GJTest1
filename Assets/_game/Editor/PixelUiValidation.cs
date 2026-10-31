using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class PixelUiValidation
{
    private static double _started;
    private static int _phase;
    private static float _phaseStarted;
    private static Text _sample;
    private static Vector3[] _firstVertices;
    private static BandolierUpgradeScreen _screen;
    private static string _artifacts;
    private static bool _oldOptionsEnabled;
    private static EnterPlayModeOptions _oldOptions;

    public static void Run()
    {
        var importer = (TrueTypeFontImporter)AssetImporter.GetAtPath("Assets/_game/Resources/Fonts/m6x11cyrillic.ttf");
        importer.fontSize = 16;
        importer.fontTextureCase = FontTextureCase.Dynamic;
        importer.fontRenderingMode = FontRenderingMode.HintedRaster;
        importer.includeFontData = true;
        importer.SaveAndReimport();
        Require(GameScript.Round(1).Length == 3 && GameScript.Round(1).All(group => group.Length == 3),
            "First wave should contain three groups of three enemies");
        Font font = Resources.Load<Font>("Fonts/m6x11cyrillic");
        const string russian = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789";
        Require(russian.All(font.HasCharacter), "Font is missing a Russian glyph");
        EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
        _oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _oldOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        _artifacts = ".utmp/Artifacts/pixel-ui-integrated-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        _started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - _started < 180, "Pixel UI validation timed out");
            if (!Application.isPlaying)
            {
                return;
            }
            if (EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
                return;
            }
            RunDirector run = RunDirector.Instance;
            if (run == null || run.Shooter == null || RunUI.Instance == null)
            {
                return;
            }
            if (_phase == 0)
            {
                ValidateHealthBar();
                _phase = 1;
            }
            if (_phase == 1)
            {
                if (!run.AwaitingReward)
                {
                    Time.timeScale = 8;
                    foreach (Enemy enemy in G.Enemies.ToArray())
                    {
                        enemy.TakeDamage(99999, Color.white, Vector3.zero);
                    }
                    return;
                }
                Time.timeScale = 0;
                _screen = RunUI.Instance.UpgradeScreen;
                Require(_screen.IsOpen && !run.ChoosingModule, "Expected the real bandolier bullet upgrade stage");
                _sample = GameObject.Find("Upgrade title").GetComponent<Text>();
                _phaseStarted = Time.unscaledTime;
                _phase = 2;
                return;
            }
            if (_phase == 2 && Time.unscaledTime - _phaseStarted > 0.4f)
            {
                Require(_sample.font == PixelUiTheme.Font, "New upgrade panel did not inherit the pixel font");
                Require(_sample.GetComponent<WavyUpgradeText>() != null, "New upgrade panel did not get text wave");
                Text[] labels = GameObject.Find("Run interface").GetComponentsInChildren<Text>(true);
                Require(labels.All(label => label.font == PixelUiTheme.Font), "Default font remains in game UI");
                Transform reward = GameObject.Find("Bandolier upgrades").transform;
                Require(reward.GetComponentsInChildren<Text>().All(label => label.GetComponent<WavyUpgradeText>() != null),
                    "Current upgrade text did not receive the wave effect");
                ValidateColorPreservation();
                Canvas.ForceUpdateCanvases();
                _firstVertices = _sample.canvasRenderer.GetMesh().vertices;
                Capture("pixel-ui-wave-a.png");
                _phaseStarted = Time.unscaledTime;
                _phase = 3;
                return;
            }
            if (_phase == 3 && Time.unscaledTime - _phaseStarted > 0.4f)
            {
                Require(Time.timeScale == 0, "Test should remain paused");
                Canvas.ForceUpdateCanvases();
                Vector3[] vertices = _sample.canvasRenderer.GetMesh().vertices;
                Require(vertices.Length == _firstVertices.Length && vertices.Where((vertex, index) =>
                    Mathf.Abs(vertex.y - _firstVertices[index].y) > 0.05f).Any(), "Text wave does not animate while paused");
                Capture("pixel-ui-wave-b.png");
                int moduleSlot = Array.FindIndex(run.Drum.Sockets, item => item != null);
                GameObject.Find("Upgrade slot " + (moduleSlot + 1)).GetComponent<UpgradeSlotHover>()
                    .OnPointerEnter(new PointerEventData(EventSystem.current) { position = Input.mousePosition });
                _phaseStarted = Time.unscaledTime;
                _phase = 4;
                return;
            }
            if (_phase == 4 && Time.unscaledTime - _phaseStarted > 0.3f)
            {
                Require(_screen.Tooltips.HasModule, "Expected two slot tooltips");
                TooltipView[] tooltips = GameObject.Find("Run interface").GetComponentsInChildren<TooltipView>();
                Require(tooltips.Length >= 2 && tooltips.All(view => view.GetComponentsInChildren<Text>(true)
                    .All(label => label.font == PixelUiTheme.Font)), "Tooltip text is not using the pixel font");
                Capture("pixel-ui-stacked-tooltips.png");
                _screen.Tooltips.HideAll();
                _screen.Selection.SelectOffer(0);
                _screen.Selection.SelectSlot(0);
                _phaseStarted = Time.unscaledTime;
                _phase = 5;
                return;
            }
            if (_phase == 5 && Time.unscaledTime - _phaseStarted > 0.2f)
            {
                Text confirmation = GameObject.Find("Replacement details").GetComponent<Text>();
                Require(confirmation.font == PixelUiTheme.Font && confirmation.GetComponent<WavyUpgradeText>() != null,
                    "Confirmation text does not use pixel font and glyph wave");
                Capture("pixel-ui-confirmation.png");
                _screen.Selection.Confirm();
                Require(run.AwaitingReward && run.ChoosingModule && _screen.IsOpen, "Module stage did not open");
                _phaseStarted = Time.unscaledTime;
                _phase = 6;
                return;
            }
            if (_phase == 6 && Time.unscaledTime - _phaseStarted > 0.3f)
            {
                Text[] labels = GameObject.Find("Bandolier upgrades").GetComponentsInChildren<Text>(true);
                Require(labels.All(label => label.font == PixelUiTheme.Font && label.GetComponent<WavyUpgradeText>() != null),
                    "Module upgrade text did not retain pixel font and glyph wave");
                Capture("pixel-ui-modules.png");
                Debug.Log("PIXEL_UI_VALIDATION_PASSED: real bandolier bullet/module upgrades, confirmation, stacked tooltips, paused glyph wave, colors, Cyrillic and left-anchored HP");
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged += ExitAfterPlay;
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception)
        {
            EditorApplication.update -= Tick;
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ValidateHealthBar()
    {
        Enemy prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/_game/Prefabs/Enemy.prefab");
        Enemy enemy = UnityEngine.Object.Instantiate(prefab, new Vector3(1000, 1000, 0), Quaternion.identity);
        enemy.Configure(80);
        Transform back = enemy.transform.Find("Health background");
        SpriteRenderer fill = back.Find("Health").GetComponent<SpriteRenderer>();
        float left = fill.bounds.min.x;
        float originalRight = fill.bounds.max.x;
        enemy.TakeDamage(40, Color.white, Vector3.zero);
        Require(Mathf.Abs(fill.bounds.min.x - left) < 0.0001f, "HP left edge moved after damage");
        Require(fill.bounds.max.x < originalRight, "HP right edge did not shrink");
        Require(Mathf.Abs(fill.transform.localScale.x - 0.5f) < 0.0001f, "Half HP does not produce half a bar");
        enemy.TakeDamage(20, Color.white, Vector3.zero);
        Require(Mathf.Abs(fill.bounds.min.x - left) < 0.0001f, "HP left edge moved on subsequent damage");
        Require(Mathf.Abs(fill.transform.localScale.x - 0.25f) < 0.0001f, "Quarter HP does not produce a quarter bar");
        UnityEngine.Object.Destroy(enemy.gameObject);
        Debug.Log("LEFT_ANCHORED_HEALTH_BAR_PASSED");
    }

    private static void ValidateColorPreservation()
    {
        using (var vertices = new VertexHelper())
        {
            Color32 color = new Color32(255, 80, 20, 255);
            for (int index = 0; index < 8; index++)
            {
                var vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = new Vector3(index, index % 2, 0);
                vertex.uv0 = new Vector2(index / 8f, 0.5f);
                vertices.AddVert(vertex);
            }
            _sample.GetComponent<WavyUpgradeText>().ModifyMesh(vertices);
            for (int index = 0; index < vertices.currentVertCount; index++)
            {
                UIVertex vertex = new UIVertex();
                vertices.PopulateUIVertex(ref vertex, index);
                Require(vertex.color.Equals(color), "Wave changed a glyph color");
                Require(Mathf.Approximately(vertex.uv0.x, index / 8f), "Wave changed a glyph UV");
            }
        }
    }

    private static void Capture(string name)
    {
        Canvas canvas = GameObject.Find("Run interface").GetComponent<Canvas>();
        Camera camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        _screen.Presentation.RenderNow(1280, 720);
        _screen.RefreshLayout();
        _screen.Tooltips.RefreshPosition();
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var capture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        capture.Apply();
        Directory.CreateDirectory(_artifacts);
        File.WriteAllBytes(_artifacts + "/" + name, capture.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.Object.Destroy(capture);
        UnityEngine.Object.Destroy(target);
    }

    private static void ExitAfterPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }
        EditorApplication.playModeStateChanged -= ExitAfterPlay;
        EditorSettings.enterPlayModeOptionsEnabled = _oldOptionsEnabled;
        EditorSettings.enterPlayModeOptions = _oldOptions;
        EditorApplication.delayCall += () => EditorApplication.Exit(0);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
