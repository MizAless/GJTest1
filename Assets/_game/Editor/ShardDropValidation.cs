using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using GameEconomy;
using GameEconomy.Shards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ShardDropValidation
{
    private static readonly MethodInfo Publish = typeof(RunEconomyContext).GetMethod("PublishDeath", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo Replay = typeof(ShardDropService).GetMethod("OnEnemyDied", BindingFlags.Instance | BindingFlags.NonPublic);
    private static ShardDropService _service;
    private static ShardDropSettings _settings;
    private static RunEconomyContext _scope;
    private static EnemyDeathInfo _lastDeath;
    private static int _phase;
    private static int _enemyId = -10000;
    private static double _started;
    private static float _time;
    private static Vector3 _pausedPosition;
    private static bool _oldOptionsEnabled;
    private static EnterPlayModeOptions _oldOptions;
    private static IEnumerator _firstWaiter;
    private static IEnumerator _secondWaiter;
    private static bool _captured;

    public static void Run()
    {
        ShardAssetsAuthoring.Build();
        _settings = Object.Instantiate(Resources.Load<ShardDropSettings>(ShardDropSettings.ResourcePath));
        Require(_settings != null && _settings.Kinds.Length == 3, "Missing drop assets");
        foreach (ShardAppearance kind in _settings.Kinds)
        {
            Require(kind.Prefab.GetComponentsInChildren<Collider>(true).Length == 0, "Pickup may intercept shots");
            Require(kind.Prefab.GetComponentsInChildren<Collider2D>(true).Length == 0, "Pickup may intercept 2D aim");
            Require(kind.Prefab.GetComponentsInChildren<SpriteRenderer>(true).Length == 1, "Ground shadow missing");
        }
        ValidateRolls();
        Object.DestroyImmediate(_settings);
        _settings = null;
        EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
        _oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _oldOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        _started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void ValidateRolls()
    {
        UnityEngine.Random.State random = UnityEngine.Random.state;
        UnityEngine.Random.InitState(42);
        _settings.Enemies[0].Chance = 0;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Require(!_settings.TryRoll(EnemyKind.Weak, out _), "Zero chance dropped loot");
        }
        _settings.Enemies[0].Chance = 1;
        _settings.Enemies[0].Minimum = 2;
        _settings.Enemies[0].Maximum = 4;
        bool sawTwo = false;
        bool sawFour = false;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            Require(_settings.TryRoll(EnemyKind.Weak, out int count) && count >= 2 && count <= 4, "Count outside inclusive range");
            sawTwo |= count == 2;
            sawFour |= count == 4;
        }
        Require(sawTwo && sawFour, "Count endpoints missing");
        _settings.GradeWeights = Vector3.zero;
        Require(!_settings.TryRollGrade(out _), "Zero grade weights must disable drops");
        _settings.GradeWeights = new Vector3(1, 0, 0);
        Require(_settings.TryRollGrade(out ShardGrade grade) && grade == ShardGrade.T1, "T1 is best and independently weighted");
        _settings.Enemies[0].Minimum = 1;
        _settings.Enemies[0].Maximum = 1;
        UnityEngine.Random.state = random;
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - _started < 180, "Shard validation timed out at phase " + _phase);
            if (EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
                return;
            }
            RunDirector director = RunDirector.Instance;
            if (!Application.isPlaying || director == null || StaticShooterStage.Instance == null || G.Enemies.Count == 0)
            {
                return;
            }
            // Isolate this test from contact damage and additional wave transitions.
            foreach (Enemy live in G.Enemies)
            {
                live.transform.position = new Vector3(0, 0, 1000);
            }
            if (_phase == 0 && G.Enemies.Count == 3 && !G.EnemySpawner.IsSpawning)
            {
                // Scene loading can unload an unsaved edit-mode fixture. Create the runtime copy now.
                _settings = Object.Instantiate(Resources.Load<ShardDropSettings>(ShardDropSettings.ResourcePath));
                ValidateRolls();
                director.StopAllCoroutines();
                _scope = director.Economy;
                _scope.EnemyDied += death => _lastDeath = death;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_game/Prefabs/Shards/ShardDropService.prefab");
                _service = Object.Instantiate(prefab).GetComponent<ShardDropService>();
                _service.Initialize(_scope, StaticShooterStage.Instance.CollectionTarget, _settings, StaticShooterStage.Instance.GroundSurface);
                Enemy enemy = G.Enemies[0];
                enemy.transform.position = new Vector3(0, 0, 9);
                enemy.TakeDamage(10000, Color.white, Vector3.zero);
                enemy.TakeDamage(10000, Color.white, Vector3.zero);
                Replay.Invoke(_service, new object[] { _lastDeath });
                Require(_service.PendingCount == 1 && Total() == 0,
                    "Real death must spawn once without instant credit. Pending=" + _service.PendingCount
                    + " total=" + Total() + " death=" + _lastDeath.DeathId + " scopeMatch=" + (_lastDeath.RunId == _scope.RunId)
                    + " chance=" + _settings.Enemies[0].Chance + " count=" + _settings.Enemies[0].Minimum + "/" + _settings.Enemies[0].Maximum);
                ShardPickup pickup = _service.GetComponentInChildren<ShardPickup>();
                Require(Vector2.Distance(new Vector2(pickup.transform.position.x, pickup.transform.position.z), new Vector2(0, 9)) < 0.4f,
                    "Shard must spawn at death in XZ");
                _time = Time.time;
                _phase = 1;
            }
            else if (_phase == 1 && Time.time - _time > 0.25f)
            {
                Require(Total() == 0, "Levitation should precede collection");
                _pausedPosition = _service.GetComponentInChildren<ShardPickup>().GetComponentInChildren<MeshRenderer>().transform.position;
                Time.timeScale = 0;
                _time = Time.realtimeSinceStartup;
                _phase = 2;
            }
            else if (_phase == 2 && Time.realtimeSinceStartup - _time > 0.25f)
            {
                Vector3 now = _service.GetComponentInChildren<ShardPickup>().GetComponentInChildren<MeshRenderer>().transform.position;
                Require(Vector3.Distance(now, _pausedPosition) < 0.001f && Total() == 0, "Pause must freeze flight and credit");
                Time.timeScale = 1;
                _phase = 3;
            }
            else if (_phase == 3 && _service.PendingCount == 0)
            {
                Require(Total() == 1, "Single pickup must credit exactly once");
                for (int kind = 0; kind < 3; kind++)
                {
                    for (int grade = 0; grade < 3; grade++)
                    {
                        for (int index = 0; index < 3; index++)
                        {
                            _settings.Kinds[index].Weight = index == kind ? 1 : 0;
                        }
                        _settings.GradeWeights = Vector3.zero;
                        _settings.GradeWeights = grade == 0 ? Vector3.right : grade == 1 ? Vector3.up : Vector3.forward;
                        Spawn(new Vector3(-3 + kind * 3, 0, 5 + grade * 4));
                    }
                }
                Require(_service.PendingCount == 9, "Nine simultaneous drops missing");
                _firstWaiter = _service.CollectAll();
                _secondWaiter = _service.CollectAll();
                Require(_firstWaiter.MoveNext() && _secondWaiter.MoveNext(), "Stage waiters completed too early");
                _time = Time.time;
                _phase = 4;
            }
            else if (_phase == 4)
            {
                if (!_captured && Time.time - _time > 0.3f)
                {
                    Capture();
                    _captured = true;
                }
                if (_service.PendingCount == 0)
                {
                    Require(Total() == 10, "Multiple drops must credit once each");
                    Require(!_firstWaiter.MoveNext() && !_secondWaiter.MoveNext(), "Collection contract failed to finish");
                    foreach (ShardKind kind in Enum.GetValues(typeof(ShardKind)))
                    {
                        foreach (ShardGrade grade in Enum.GetValues(typeof(ShardGrade)))
                        {
                            Require(_scope.Inventory.GetShardCount(new ShardId(kind, grade)) > 0, "Kind/grade lost");
                        }
                    }
                    Spawn(Vector3.forward * 9);
                    _service.Cancel();
                    Require(_service.PendingCount == 0 && Total() == 10, "Cancel credited unarrived loot");
                    Spawn(Vector3.forward * 9);
                    _scope.Dispose();
                    Require(_service.PendingCount == 0 && Total() == 10, "Defeat/dispose must cancel without credit");
                    RunEconomyContext next = new RunEconomyContext(_scope.Settings);
                    _service.Initialize(next, StaticShooterStage.Instance.CollectionTarget, _settings);
                    Replay.Invoke(_service, new object[] { _lastDeath });
                    Require(_service.PendingCount == 0 && next.Inventory.GetShardSnapshot().Count == 0, "Old run leaked after restart");
                    _scope = next;
                    Spawn(Vector3.forward * 9);
                    ShardPickup pending = _service.GetComponentsInChildren<ShardPickup>().Last();
                    Object.Destroy(pending.gameObject);
                    _phase = 5;
                }
            }
            else if (_phase == 5 && _service.PendingCount == 0)
            {
                Require(Total() == 0 && !_service.CollectAll().MoveNext(), "Destroyed pickup left a hanging waiter");
                Spawn(Vector3.forward * 9);
                _service.gameObject.SetActive(false);
                Require(_service.PendingCount == 0 && Total() == 0, "Stage unload left pending loot");
                _scope.Dispose();
                Debug.Log("SHARD_DROP_VALIDATION_PASSED: actual death, dedup, XZ, pause, nine concurrent pickups, all kinds/grades, stage waiters, cancel, defeat, restart, destroyed pickup, disable");
                Finish(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static void Spawn(Vector3 position)
    {
        _lastDeath = new EnemyDeathInfo(_scope.RunId, _enemyId--, EnemyKind.Weak, 30, position, position + Vector3.up, 1, 1, default);
        Publish.Invoke(_scope, new object[] { _lastDeath });
        Replay.Invoke(_service, new object[] { _lastDeath });
    }

    private static int Total()
    {
        return _scope.Inventory.GetShardSnapshot().Values.Sum();
    }

    private static void Capture()
    {
        Directory.CreateDirectory(".utmp/Artifacts");
        RenderTexture target = new RenderTexture(1280, 720, 24);
        Camera camera = Camera.main;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        Texture2D pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(".utmp/Artifacts/shards.png", pixels.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.Destroy(pixels);
        target.Release();
        Object.Destroy(target);
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        Time.timeScale = 1;
        EditorApplication.playModeStateChanged += ExitAfterPlay;
        EditorApplication.isPlaying = false;
        void ExitAfterPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }
            EditorApplication.playModeStateChanged -= ExitAfterPlay;
            EditorSettings.enterPlayModeOptionsEnabled = _oldOptionsEnabled;
            EditorSettings.enterPlayModeOptions = _oldOptions;
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
