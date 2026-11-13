using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using GameEconomy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CoinCollectionValidation
{
    private static CoinCollectionService _service;
    private static RunEconomyContext _run;
    private static CoinDropSettings _settings;
    private static IEnumerator _collection;
    private static IEnumerator _joined;
    private static int _phase;
    private static int _id;
    private static float _time;
    private static double _started;
    private static int _strikes;
    private static int _drum;
    private static int _completedShots;
    private static bool _hasPrevious;
    private static bool _captured;
    private static Vector3 _previous;
    private static bool _oldOptionsEnabled;
    private static EnterPlayModeOptions _oldOptions;

    public static void Run()
    {
        _oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _oldOptions = EditorSettings.enterPlayModeOptions;
        EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
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
            Require(EditorApplication.timeSinceStartup - _started < 120, "Coin test timed out.");
            if (!EditorApplication.isPlaying || StaticShooterStage.Instance == null || Time.time < 0.3f)
            {
                return;
            }
            StaticShooterStage stage = StaticShooterStage.Instance;
            if (_phase == 0)
            {
                RunDirector.Instance.StopAllCoroutines();
                foreach (Enemy enemy in G.Enemies.ToArray())
                {
                    enemy.enabled = false;
                }
                _settings = Object.Instantiate(Resources.Load<CoinDropSettings>(CoinDropSettings.ResourcePath));
                Require(_settings != null, "Missing configured coin asset.");
                _run = new RunEconomyContext(Resources.Load<RunEconomySettings>(RunEconomySettings.ResourcePath));
                _service = new GameObject("Coin validation", typeof(CoinCollectionService)).GetComponent<CoinCollectionService>();
                _service.Initialize(_run, _settings);
                _collection = _service.CollectAll(Request());
                Require(!_collection.MoveNext(), "Empty collection should finish immediately.");
                foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
                {
                    Publish(kind);
                }
                Require(_service.PendingCount == 14 && _run.Inventory.Coins == 0, "Drop counts or early credit incorrect.");
                Require(_service.GetComponentsInChildren<Collider>().Length == 0, "Coins intercept combat raycasts.");
                _time = Time.time;
                _phase = 1;
            }
            else if (_phase == 1 && Time.time - _time > 0.4f)
            {
                Require(_service.PendingCount == 14 && _run.Inventory.Coins == 0, "Coins disappeared between groups.");
                Capture("coins-idle");
                Shooter shooter = Object.FindObjectOfType<Shooter>();
                _drum = shooter.Container.BulletsRemaining;
                _completedShots = shooter.CompletedShots;
                _service.CoinStruck += CheckRoute;
                _collection = _service.CollectAll(Request());
                _joined = _service.CollectAll(Request());
                Require(_collection.MoveNext() && _joined.MoveNext(), "Collection did not start/join.");
                _time = Time.time;
                _phase = 2;
            }
            else if (_phase == 2)
            {
                bool running = _collection.MoveNext();
                bool joined = _joined.MoveNext();
                if (Time.time - _time > 0.4f && _strikes > 0 && !_captured)
                {
                    Capture("coins-collection");
                    _captured = true;
                }
                if (running)
                {
                    Require(joined, "Concurrent waiter finished before collection.");
                    return;
                }
                Require(!joined && _run.Inventory.Coins == 49 && _strikes == 14 && _service.PendingCount == 0,
                    "Wrong total, duplicate award, or early completion.");
                Shooter shooter = Object.FindObjectOfType<Shooter>();
                Require(shooter.Container.BulletsRemaining == _drum && shooter.CompletedShots == _completedShots,
                    "Cinematic consumed combat drum or executed a shot.");
                _collection = _service.CollectAll(Request());
                Require(!_collection.MoveNext() && _run.Inventory.Coins == 49, "Repeated collection credited twice.");
                _service.CoinStruck -= CheckRoute;
                _settings.GetRule(EnemyKind.Boss).Count = 500;
                Publish(EnemyKind.Boss);
                _collection = _service.CollectAll(Request());
                Require(_collection.MoveNext(), "Stress collection did not start.");
                _time = Time.time;
                _phase = 3;
            }
            else if (_phase == 3 && !_collection.MoveNext())
            {
                Require(_run.Inventory.Coins == 2549 && _service.PendingCount == 0, "Stress collection lost rewards.");
                Require(Time.time - _time < 5, "500 coins take too long.");
                Publish(EnemyKind.Boss);
                _collection = _service.CollectAll(Request());
                _collection.MoveNext();
                _service.Cancel();
                Require(!_collection.MoveNext() && _run.Inventory.Coins == 2549 && _service.PendingCount == 0,
                    "Cancel credited unarrived coins or left waiter blocked.");
                Publish(EnemyKind.Weak);
                _collection = _service.CollectAll(Request());
                _collection.MoveNext();
                _run.Dispose();
                Require(!_collection.MoveNext() && !_service.IsCollecting && _run.Inventory.Coins == 2549,
                    "Defeat/unload did not cancel collection.");
                RunEconomyContext replacement = new RunEconomyContext(_run.Settings);
                _service.Initialize(replacement, _settings);
                _run = replacement;
                Publish(EnemyKind.Weak);
                _collection = _service.CollectAll(Request());
                _collection.MoveNext();
                _phase = 4;
            }
            else if (_phase == 4 && !_collection.MoveNext())
            {
                Require(_run.Inventory.Coins == 1, "Restart reused previous wallet or pending rewards.");
                _run.Dispose();
                Debug.Log("COIN_COLLECTION_VALIDATION_PASSED: exact total; farthest route; idle retention; concurrent/repeated calls; 500 coins under 5s; no combat shot; cancel/end/restart.");
                Exit(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Exit(1);
        }
    }

    private static CoinCollectionRequest Request()
    {
        StaticShooterStage stage = StaticShooterStage.Instance;
        return new CoinCollectionRequest(_run, stage.Weapon.Muzzle, stage.CollectionTarget);
    }

    private static void Publish(EnemyKind kind)
    {
        int id = ++_id;
        var info = new EnemyDeathInfo(_run.RunId, id, kind, 30,
            new Vector3((id % 4 - 1.5f) * 2, 0, 6 + id % 3), Vector3.up, 5, id, default);
        MethodInfo publisher = typeof(RunEconomyContext).GetMethod("PublishDeath", BindingFlags.NonPublic | BindingFlags.Instance);
        publisher.Invoke(_run, new object[] { info });
        // A repeated death event must not mint another drop.
        publisher.Invoke(_run, new object[] { info });
    }

    private static void CheckRoute(CoinVisual struck)
    {
        if (_hasPrevious)
        {
            CoinVisual[] eligible = _service.GetComponentsInChildren<CoinVisual>()
                .Where(coin => !coin.InFlight || coin == struck).ToArray();
            Require(CoinCollectionService.Farthest(eligible, _previous) == struck, "Ricochet did not choose the farthest coin.");
        }
        _previous = struck.transform.position;
        _hasPrevious = true;
        _strikes++;
    }

    private static void Capture(string name)
    {
        System.IO.Directory.CreateDirectory(".utmp/Artifacts");
        Camera camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        pixels.Apply();
        System.IO.File.WriteAllBytes(".utmp/Artifacts/" + name + ".png", pixels.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.Destroy(pixels);
        Object.Destroy(target);
    }

    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Exit(int code)
    {
        EditorApplication.update -= Tick;
        EditorSettings.enterPlayModeOptionsEnabled = _oldOptionsEnabled;
        EditorSettings.enterPlayModeOptions = _oldOptions;
        EditorApplication.Exit(code);
    }
}
