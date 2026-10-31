using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CartridgeVisualValidation
{
    private static double _started;
    private static float _phaseStarted;
    private static int _phase;
    private static BandolierPreview _controller;
    private static bool _originalOptionsEnabled;
    private static EnterPlayModeOptions _originalOptions;
    private static CartridgeVisual _importTestVisual;
    private static float _originalPixelsPerUnit;
    private static float _originalTipHeight;
    private static string _importTestPath;

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene(BandolierPreviewAuthoring.ScenePath);
            _controller = UnityEngine.Object.FindAnyObjectByType<BandolierPreview>();
            _controller.Scrub(1);
            Render("cartridge-before.png");
            var settings = new SerializedObject(_controller);
            var loops = settings.FindProperty("_loops");
            Vector3[] loopScales = Enumerable.Range(0, loops.arraySize).Select(index =>
                ((MeshRenderer)loops.GetArrayElementAtIndex(index).objectReferenceValue).transform.localScale).ToArray();
            Vector3[] loopPositions = Enumerable.Range(0, loops.arraySize).Select(index =>
                ((MeshRenderer)loops.GetArrayElementAtIndex(index).objectReferenceValue).transform.localPosition).ToArray();
            BandolierAmmoArt.ApplyToController(_controller);
            ValidateLayers();
            for (int index = 0; index < loops.arraySize; index++)
            {
                Transform loop = ((MeshRenderer)loops.GetArrayElementAtIndex(index).objectReferenceValue).transform;
                Require(loop.localScale == loopScales[index] && loop.localPosition == loopPositions[index], "Manual loop fitting changed");
            }
            ValidateSpriteSwap();
            _controller.Scrub(0);
            Require(_controller.GetComponentsInChildren<CartridgeVisual>().All(visual =>
                visual.GetComponentsInChildren<MeshRenderer>().All(layer => !layer.enabled)), "Folded layers are visible");
            _controller.Scrub(1);
            Render("cartridge-four-meshes.png");
            EditorSceneManager.SaveScene(_controller.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("CARTRIDGE_EDITOR_VALIDATED: prefab instances, four layers, shared shadows, sprite swap, loops and fitting");
            BeginImportSettingsTest();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void BeginImportSettingsTest()
    {
        _importTestVisual = _controller.GetComponentsInChildren<CartridgeVisual>()[0];
        _importTestPath = AssetDatabase.GetAssetPath(_importTestVisual.TipSprite);
        var importer = (TextureImporter)AssetImporter.GetAtPath(_importTestPath);
        _originalPixelsPerUnit = importer.spritePixelsPerUnit;
        _originalTipHeight = _importTestVisual.Tip.GetComponent<MeshFilter>().sharedMesh.bounds.size.y;
        importer.spritePixelsPerUnit *= 1.25f;
        importer.SaveAndReimport();
        EditorApplication.delayCall += CheckImportSettingsRefresh;
    }

    private static void CheckImportSettingsRefresh()
    {
        try
        {
            float height = _importTestVisual.Tip.GetComponent<MeshFilter>().sharedMesh.bounds.size.y;
            Require(Mathf.Abs(height - _originalTipHeight / 1.25f) < 0.0001f,
                "Changing sprite PPU did not automatically refresh the mesh");
            var importer = (TextureImporter)AssetImporter.GetAtPath(_importTestPath);
            importer.spritePixelsPerUnit = _originalPixelsPerUnit;
            importer.SaveAndReimport();
            EditorApplication.delayCall += StartPlayMode;
        }
        catch (Exception exception)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(_importTestPath);
            importer.spritePixelsPerUnit = _originalPixelsPerUnit;
            importer.SaveAndReimport();
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void StartPlayMode()
    {
        float restoredHeight = _importTestVisual.Tip.GetComponent<MeshFilter>().sharedMesh.bounds.size.y;
        Require(Mathf.Abs(restoredHeight - _originalTipHeight) < 0.0001f, "Sprite import settings were not restored");
        _controller.Scrub(1);
        EditorSceneManager.SaveScene(_controller.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("CARTRIDGE_IMPORT_REFRESH_PASSED: real PPU reimport automatically rebuilt and restored geometry");
        _originalOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _originalOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        _started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void ValidateLayers()
    {
        CartridgeVisual[] visuals = _controller.GetComponentsInChildren<CartridgeVisual>();
        Require(visuals.Length == 6, "Expected six cartridge instances");
        foreach (CartridgeVisual visual in visuals)
        {
            Require(PrefabUtility.IsPartOfPrefabInstance(visual), "Cartridge is not a prefab instance");
            Require(visual.GetComponentsInChildren<MeshRenderer>(true).Length == 4, "Expected exactly four mesh renderers");
            Require(visual.transform.Find("Mouth back") == null, "Mouth back still exists");
            Mesh casing = visual.Casing.GetComponent<MeshFilter>().sharedMesh;
            Mesh tip = visual.Tip.GetComponent<MeshFilter>().sharedMesh;
            Require(casing == visual.CasingShadow.GetComponent<MeshFilter>().sharedMesh, "Casing shadow geometry mismatch");
            Require(tip == visual.TipShadow.GetComponent<MeshFilter>().sharedMesh, "Tip shadow geometry mismatch");
            Require(visual.Tip.transform.localPosition == Vector3.zero && visual.Tip.transform.localScale == Vector3.one,
                "Manual fitting was not transferred out of child transforms");
            Require(visual.CasingSprite.name == "Casing2", "Wrong casing sprite");
            ValidateTexture(visual.Casing, visual.CasingSprite);
            ValidateTexture(visual.Tip, visual.TipSprite);
        }
    }

    private static void ValidateTexture(MeshRenderer renderer, Sprite sprite)
    {
        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        Require(properties.GetTexture("_MainTex") == sprite.texture, "Sprite texture was overwritten by controller");
    }

    private static void ValidateSpriteSwap()
    {
        CartridgeVisual visual = _controller.GetComponentsInChildren<CartridgeVisual>()[0];
        Sprite original = visual.TipSprite;
        float originalWidth = visual.TipWidthScale;
        Mesh originalMesh = visual.Tip.GetComponent<MeshFilter>().sharedMesh;
        visual.SetSprites(visual.CasingSprite, BandolierAmmoArt.LoadSprite("FanTip"));
        Require(visual.Tip.GetComponent<MeshFilter>().sharedMesh != originalMesh, "Swapping sprite did not rebuild geometry");
        ValidateTexture(visual.Tip, visual.TipSprite);
        visual.SetSprites(visual.CasingSprite, original, originalWidth);
        Require(visual.Tip.GetComponent<MeshFilter>().sharedMesh == originalMesh, "Sprite restore did not reuse persistent mesh");
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - _started < 180, "Cartridge Play Mode test timed out");
            if (!Application.isPlaying)
            {
                return;
            }
            if (EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
                return;
            }
            _controller = UnityEngine.Object.FindAnyObjectByType<BandolierPreview>();
            if (_phase == 0)
            {
                _controller.Scrub(0);
                _controller.Replay();
                _phaseStarted = Time.time;
                _phase = 1;
            }
            else if (_phase == 1 && Time.time - _phaseStarted > 2.2f)
            {
                Require(_controller.Progress > 0.999f, "Replay did not open the bandolier");
                foreach (CartridgeVisual visual in _controller.GetComponentsInChildren<CartridgeVisual>())
                {
                    Require(visual.GetComponentsInChildren<MeshRenderer>().All(layer => layer.enabled), "Open cartridge layers hidden");
                    ValidateTexture(visual.Tip, visual.TipSprite);
                }
                ValidateRuntimeSpriteSwap();
                Render("cartridge-play-mode.png");
                _controller.Fold();
                _phaseStarted = Time.time;
                _phase = 2;
            }
            else if (_phase == 2 && Time.time - _phaseStarted > 2.2f)
            {
                Require(_controller.Progress < 0.001f, "Fold did not close the bandolier");
                Require(_controller.GetComponentsInChildren<CartridgeVisual>().All(visual =>
                    visual.GetComponentsInChildren<MeshRenderer>().All(layer => !layer.enabled)), "Folded runtime layers are visible");
                Debug.Log("CARTRIDGE_VISUAL_VALIDATION_PASSED: editor and Play Mode");
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged += ExitAfterPlayMode;
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

    private static void ValidateRuntimeSpriteSwap()
    {
        CartridgeVisual visual = _controller.GetComponentsInChildren<CartridgeVisual>()[0];
        Sprite original = visual.TipSprite;
        float widthScale = visual.TipWidthScale;
        Mesh runtimeMesh = visual.Tip.GetComponent<MeshFilter>().sharedMesh;
        Require(!EditorUtility.IsPersistent(runtimeMesh), "Runtime instance is modifying persistent geometry");
        visual.SetSprites(visual.CasingSprite, BandolierAmmoArt.LoadSprite("FanTip"));
        ValidateTexture(visual.Tip, visual.TipSprite);
        Require(visual.Tip.GetComponent<MeshFilter>().sharedMesh == visual.TipShadow.GetComponent<MeshFilter>().sharedMesh,
            "Runtime sprite swap broke the shadow");
        visual.SetSprites(visual.CasingSprite, original, widthScale);
        _controller.Scrub(1);
    }

    private static void ExitAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }
        EditorApplication.playModeStateChanged -= ExitAfterPlayMode;
        EditorSettings.enterPlayModeOptionsEnabled = _originalOptionsEnabled;
        EditorSettings.enterPlayModeOptions = _originalOptions;
        EditorApplication.delayCall += () => EditorApplication.Exit(0);
    }

    private static void Render(string name)
    {
        Camera camera = Camera.main;
        var target = new RenderTexture(1600, 900, 24);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var capture = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
        capture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        capture.Apply();
        Directory.CreateDirectory(".utmp/Artifacts");
        File.WriteAllBytes(".utmp/Artifacts/" + name, capture.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(capture);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
