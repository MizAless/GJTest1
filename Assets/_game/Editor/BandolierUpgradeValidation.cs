using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class BandolierUpgradeValidation
{
    private static double _started;
    private static float _phaseStarted;
    private static int _phase;
    private static AmmoItem _expectedReward;
    private static AmmoItem _beforeConfirmation;
    private static BandolierUpgradeScreen _screen;
    private static bool _originalOptionsEnabled;
    private static EnterPlayModeOptions _originalOptions;
    private static string _artifactFolder;
    private static int _exitCode;

    public static void Run()
    {
        _artifactFolder = ".utmp/Artifacts/upgrade-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        UpgradeAssetsAuthoring.BuildAssets();
        EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
        _originalOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _originalOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        _started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - _started < 180, "Upgrade test timed out at phase " + _phase);
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
            _screen = RunUI.Instance.UpgradeScreen;
            if (_phase == 0)
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
                Require(!run.ChoosingModule && _screen.IsOpen && Time.timeScale == 0, "Expected paused bullet upgrade stage");
                _phaseStarted = Time.unscaledTime;
                _phase = 1;
            }
            else if (_phase == 1 && Time.unscaledTime - _phaseStarted > 1.8f)
            {
                Require(_screen.Presentation.Bandolier.Progress > 0.99f, "Bandolier did not unroll while paused");
                Capture("upgrades-bullets.png");
                AmmoItem first = run.Drum.Cartridges[0];
                AmmoItem module = run.Drum.Sockets[0];
                Click("Upgrade slot 1");
                Click("Upgrade slot 2");
                Require(ReferenceEquals(run.Drum.Cartridges[1], first), "Two slot clicks did not swap cartridges");
                Require(ReferenceEquals(run.Drum.Sockets[0], module), "Module moved with a cartridge");
                Click("Upgrade slot 1");
                _phaseStarted = Time.unscaledTime;
                _phase = 2;
            }
            else if (_phase == 2 && Time.unscaledTime - _phaseStarted > 0.35f)
            {
                Transform cartridge = _screen.Presentation.Bandolier.GetCartridge(0);
                Require(cartridge.localPosition.y > 1.5f, "Selected cartridge did not lift out of the loop");
                Capture("upgrades-picked-cartridge.png");
                _screen.Selection.Reset();
                Click("Upgrade offer 1");
                _phaseStarted = Time.unscaledTime;
                _phase = 3;
            }
            else if (_phase == 3 && Time.unscaledTime - _phaseStarted > 0.35f)
            {
                var properties = new MaterialPropertyBlock();
                _screen.Presentation.transform.Find("Offer visual 0").GetComponent<CartridgeVisual>().Casing.GetPropertyBlock(properties);
                Require(properties.GetFloat("_SpinAmount") > 0.8f, "Selected offer has no animated surface spin");
                _beforeConfirmation = run.Drum.Cartridges[2];
                Click("Upgrade slot 3");
                Require(_screen.Selection.AwaitingConfirmation, "Offer-first selection did not request confirmation");
                Require(ReferenceEquals(run.Drum.Cartridges[2], _beforeConfirmation), "Replacement happened before confirmation");
                Capture("upgrades-confirmation.png");
                Click("Cancel replacement");
                Require(ReferenceEquals(run.Drum.Cartridges[2], _beforeConfirmation), "Cancel changed the drum");
                Click("Upgrade slot 1");
                _expectedReward = run.Offers[1];
                Click("Upgrade offer 2");
                Require(_screen.Selection.AwaitingConfirmation, "Slot-first selection did not request confirmation");
                Click("Confirm replacement");
                Require(ReferenceEquals(run.Drum.Cartridges[0], _expectedReward), "Bullet reward not installed");
                Require(run.AwaitingReward && run.ChoosingModule && _screen.IsOpen && Time.timeScale == 0,
                    "Bullet confirmation closed the upgrade screen or resumed combat");
                Require(_screen.Presentation.Bandolier.Progress > 0.99f, "Module stage restarted the opening animation");
                AmmoItem installed = run.Drum.Cartridges[0];
                Click("Upgrade slot 1");
                Click("Upgrade slot 4");
                Require(ReferenceEquals(run.Drum.Cartridges[3], installed), "Rearranging became locked in module stage");
                Capture("upgrades-modules.png");
                int moduleSlot = Array.FindIndex(run.Drum.Sockets, item => item != null);
                GameObject.Find("Upgrade slot " + (moduleSlot + 1)).GetComponent<UpgradeSlotHover>()
                    .OnPointerEnter(new PointerEventData(EventSystem.current) { position = Input.mousePosition });
                Require(_screen.Tooltips.HasModule, "Slot did not show a separate module tooltip");
                Capture("upgrades-stacked-tooltips.png");
                _screen.Tooltips.HideAll();
                _expectedReward = run.Offers[0];
                Click("Upgrade offer 1");
                Click("Upgrade slot 5");
                _screen.Selection.SelectSlot(1);
                Require(_screen.Selection.Slot == 4, "Confirmation allowed selecting another slot");
                Click("Confirm replacement");
                Require(ReferenceEquals(run.Drum.Sockets[4], _expectedReward), "Module reward not installed");
                Require(!run.AwaitingReward && !_screen.IsOpen && Time.timeScale == 1, "Screen did not close after both rewards");
                Require(!run.SwapRewardCartridges(0, 1), "Rearranging is allowed outside the upgrade screen");
                Debug.Log("BANDOLIER_UPGRADE_VALIDATION_PASSED: unscaled unroll, lift, swaps, spin, both selection orders, cancel, confirmation, stages and stacked tooltips");
                Finish();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static void Click(string name)
    {
        GameObject.Find(name).GetComponent<Button>().onClick.Invoke();
    }

    private static void Capture(string name)
    {
        Canvas canvas = GameObject.Find("Run interface").GetComponent<Canvas>();
        Camera camera = Camera.main;
        var texture = new RenderTexture(1280, 720, 24);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        // Capture uses a different framebuffer from the editor's Game View.
        _screen.Presentation.RenderNow(1280, 720);
        _screen.RefreshLayout();
        _screen.Tooltips.RefreshPosition();
        Canvas.ForceUpdateCanvases();
        _screen.Tooltips.RefreshPosition();
        Debug.Log("UPGRADE_CAPTURE: " + name + " cameraAspect=" + _screen.Presentation.Camera.aspect
            + " canvas=" + ((RectTransform)canvas.transform).rect.size
            + " stage=" + _screen.Presentation.Bandolier.transform.localScale);
        Require(Mathf.Abs(_screen.Presentation.Camera.aspect - 1280f / 720) < 0.001f,
            "Capture changed the presentation aspect ratio");
        camera.Render();
        RenderTexture.active = texture;
        var capture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        capture.Apply();
        Directory.CreateDirectory(_artifactFolder);
        File.WriteAllBytes(_artifactFolder + "/" + name, capture.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.Object.Destroy(capture);
        UnityEngine.Object.Destroy(texture);
    }

    private static void Finish(int exitCode = 0)
    {
        _exitCode = exitCode;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged += ExitAfterPlay;
        EditorApplication.isPlaying = false;
    }

    private static void ExitAfterPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }
        EditorApplication.playModeStateChanged -= ExitAfterPlay;
        EditorSettings.enterPlayModeOptionsEnabled = _originalOptionsEnabled;
        EditorSettings.enterPlayModeOptions = _originalOptions;
        EditorApplication.delayCall += () => EditorApplication.Exit(_exitCode);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
