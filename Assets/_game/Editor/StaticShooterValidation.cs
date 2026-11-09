using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class StaticShooterValidation
{
    private static double _started;
    private static int _phase;
    private static float _phaseTime;
    private static Enemy _target;
    private static float _initialHealth;
    private static Vector3 _initialPosition;
    private static bool _ready;

    public static void Run()
    {
        StaticShooterAuthoring.Build();
        RunSavedScene();
    }

    public static void RunSavedScene()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
        StaticShooterArt art = AssetDatabase.LoadAssetAtPath<StaticShooterArt>(StaticShooterAuthoring.ArtAsset);
        Require(art.HeroPrefab != null && art.HeroPrefab.GetComponent<GunslingerVisual>() != null, "Saved hero prefab is invalid.");
        Require(art.RevolverPrefab != null && art.RevolverPrefab.GetComponent<RevolverVisual>() != null, "Saved revolver prefab is invalid.");
        Require(art.ProjectilePrefab != null && art.ProjectilePrefab.GetComponent<ProjectileVisual>() != null, "Saved projectile prefab is invalid.");
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        _started = EditorApplication.timeSinceStartup;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _ready = true;
            }
        };
        EditorApplication.update += Check;
        EditorApplication.isPlaying = true;
    }

    private static void Check()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - _started < 120, "Static shooter test timed out.");
            if (EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
            }
            if (!_ready || !Application.isPlaying || StaticShooterStage.Instance == null
                || G.Enemies.Count < 3)
            {
                return;
            }
            StaticShooterStage stage = StaticShooterStage.Instance;
            RunDirector run = RunDirector.Instance;
            if (_phase == 0)
            {
                Require(!Camera.main.orthographic, "Camera must have real perspective.");
                Require(stage.AutoAim, "Auto aim must be default.");
                _target = G.Enemies.First();
                Require(_target.transform.position.z > 10 && Mathf.Abs(_target.transform.position.y) < 0.1f,
                    "Enemy must spawn on ground at actual depth.");
                _initialPosition = _target.transform.position;
                _phaseTime = Time.time;
                _phase = 1;
                return;
            }
            if (_phase == 1 && Time.time - _phaseTime > 1)
            {
                Require(_target.transform.position.z < _initialPosition.z, "Enemy must approach along Z.");
                Require(_target.GetComponentInChildren<BoxCollider>() != null, "Enemy needs a 3D hitbox.");
                Physics.SyncTransforms();
                Vector3 screen = Camera.main.WorldToScreenPoint(_target.AimPosition);
                Vector3 aim = stage.AimAtScreen(screen);
                Require(Vector3.Distance(aim, _target.AimPosition) < 3, "Cursor ray must intersect real enemy hitbox.");
                foreach (AmmoItem cartridge in run.Drum.Cartridges)
                {
                    cartridge.Archetype = null;
                    cartridge.Properties = new AmmoProperties();
                    cartridge.Trigger = AmmoTrigger.None;
                }
                for (int index = 0; index < 6; index++)
                {
                    run.Drum.Sockets[index] = null;
                }
                run.Drum.Rebuild();
                stage.SetAutoAim(true);
                _initialHealth = G.Enemies.Sum(enemy => enemy.Health);
                run.Shooter.TryFire();
                _phaseTime = Time.time;
                _phase = 2;
                return;
            }
            if (_phase == 2 && Time.time - _phaseTime > 1.2f)
            {
                Require(G.Enemies.Sum(enemy => enemy.Health) < _initialHealth, "3D projectile must damage an enemy.");
                stage.SetAutoAim(false);
                Require(UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                    .Count(renderer => renderer.name == "Soft ground shadow") >= 8, "Actor and scenery shadows missing.");
                int index = 0;
                foreach (Enemy enemy in G.Enemies.ToArray())
                {
                    enemy.transform.position = new Vector3(-3 + index * 4, 0, 7 + index * 5);
                    index++;
                }
                _phaseTime = Time.time;
                _phase = 3;
                return;
            }
            if (_phase == 3 && Time.time - _phaseTime > 0.2f)
            {
                Capture();
                Debug.Log("STATIC_SHOOTER_VALIDATION_PASSED: perspective camera, XZ approach, 3D cursor ray, projectile hit, manual/auto modes, actor and scenery shadows");
                EditorApplication.update -= Check;
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.update -= Check;
            EditorApplication.Exit(1);
        }
    }

    private static void Capture()
    {
        Directory.CreateDirectory(".utmp/Artifacts");
        Camera camera = Camera.main;
        RenderTexture target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        Texture2D pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(".utmp/Artifacts/static-shooter.png", pixels.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(pixels);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
