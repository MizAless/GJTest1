using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FiveWaveRunValidation
{
    private static readonly HashSet<string> VisitedGroups = new HashSet<string>();
    private static Enemy[] _previousGroup = new Enemy[0];
    private static string _currentGroup;
    private static int _rewards;
    private static int _stage;
    private static double _started;
    private static bool _originalOptionsEnabled;
    private static EnterPlayModeOptions _originalOptions;
    private static bool _sawBoss;
    private static double _nextProgressLog;

    public static void Run()
    {
        ValidateRules();
        EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
        _originalOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _originalOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        _started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void ValidateRules()
    {
        int[] groupCounts = { 3, 4, 5, 10, 15 };
        int[] groupSizes = { 6, 5, 4, 5, 8 };
        int totalEnemies = 0;
        for (int wave = 1; wave <= 5; wave++)
        {
            int[][] groups = GameScript.Round(wave);
            Require(groups.Length == groupCounts[wave - 1], "Incorrect group count in wave " + wave);
            for (int group = 0; group < groups.Length; group++)
            {
                int[] health = groups[group];
                Require(health.Length == groupSizes[wave - 1], "Incorrect group size");
                int expectedMedium = 0;
                int expectedStrong = 0;
                int expectedBoss = 0;
                if (wave == 2 && group >= 2)
                {
                    expectedMedium = 2;
                }
                if (wave == 3)
                {
                    expectedMedium = group == 4 ? 3 : 1;
                }
                if (wave >= 4)
                {
                    expectedMedium = 2;
                }
                if ((wave == 4 && group == 9) || wave == 5)
                {
                    expectedStrong = 1;
                }
                if (wave == 5 && group == 14)
                {
                    expectedBoss = 1;
                }
                Require(health.Count(value => value == 50) == expectedMedium, "Incorrect medium enemies");
                Require(health.Count(value => value == 80) == expectedStrong, "Incorrect strong enemies");
                Require(health.Count(value => value == 200) == expectedBoss, "Incorrect boss placement");
                Require(health.All(value => value == 30 || value == 50 || value == 80 || value == 200), "Unexpected HP");
                totalEnemies += health.Length;
            }
        }
        Require(totalEnemies == 228, "Expected 228 enemies in the full run");

        HashSet<int> moduleSlots = new HashSet<int>();
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(32791);
        for (int attempt = 0; attempt < 100; attempt++)
        {
            GameObject root = new GameObject("Starter validation");
            BulletContainer drum = root.AddComponent<BulletContainer>();
            drum.Init();
            ValidateStarter(drum);
            moduleSlots.Add(Array.FindIndex(drum.Sockets, item => item != null));
            UnityEngine.Object.DestroyImmediate(root);
        }
        UnityEngine.Random.state = randomState;
        Require(moduleSlots.Count == 6, "Starter module slot is not randomized");
        Enemy prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/_game/Prefabs/Enemy.prefab");
        float speed = new SerializedObject(prefab).FindProperty("_speed").floatValue;
        Require(Mathf.Approximately(speed, 1.6f), "Expected enemy movement slowed by 20 percent");
        Debug.Log("FIVE_WAVE_RULES_PASSED: 37 groups, 228 enemies, common starter loadouts");
    }

    private static void ValidateStarter(BulletContainer drum)
    {
        Require(drum.Cartridges[0].Rarity == BulletRarity.Common, "Starter bullet must be common");
        Require(drum.Cartridges[0].Trigger == AmmoTrigger.None, "Starter bullet must not gain a rare trigger");
        Require(drum.Cartridges[0].Properties.Description().Split('\n').Length == 1, "Expected one starter bullet property");
        Require(drum.Cartridges[0].Properties.Description() != "Обычное попадание", "Starter bullet needs a property");
        Require(drum.Cartridges.Skip(1).All(item => item.Properties.Description() == "Обычное попадание"), "Only first bullet should be special");
        AmmoItem[] modules = drum.Sockets.Where(item => item != null).ToArray();
        Require(modules.Length == 1, "Expected exactly one starter module");
        Require(modules[0].Rarity == BulletRarity.Common, "Starter module must be common");
        Require(modules[0].Properties.Description() != "Обычное попадание", "Starter module needs a property");
        Require(modules[0].Properties.Description().Split('\n').Length == 1, "Expected one starter module property");
        Require(modules[0].Trigger == AmmoTrigger.None, "Common starter must not gain a rare trigger");
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - _started < 300, "Full run validation timed out");
            if (!Application.isPlaying)
            {
                return;
            }
            if (EditorApplication.isPaused)
            {
                // Batch validation must not be held on the console's pause-on-error setting.
                Debug.Log("FIVE_WAVE_VALIDATION: resuming paused Play Mode");
                EditorApplication.isPaused = false;
                return;
            }
            RunDirector run = RunDirector.Instance;
            if (run == null || run.Shooter == null || RunUI.Instance == null)
            {
                return;
            }
            if (EditorApplication.timeSinceStartup >= _nextProgressLog)
            {
                _nextProgressLog = EditorApplication.timeSinceStartup + 10;
                Debug.Log("FIVE_WAVE_PROGRESS: stage=" + _stage + " wave=" + run.Round
                    + " group=" + run.Pack + " reward=" + run.AwaitingReward
                    + " active=" + run.CombatActive + " scale=" + Time.timeScale
                    + " firing=" + run.Shooter.IsFiring + " shots=" + run.Shooter.CompletedShots
                    + " gameTime=" + Time.time + " spawning=" + G.EnemySpawner.IsSpawning
                    + " remaining=" + run.Drum.BulletsRemaining + " reload=" + run.Drum.IsReloading
                    + " enemies=" + string.Join(",", G.Enemies.Select(enemy => enemy.Health.ToString("0.#"))));
            }
            if (_stage == 0)
            {
                if (run.Round == 0)
                {
                    return;
                }
                Require(!run.AwaitingReward && run.Round == 1, "Run should start without a reward selection");
                ValidateStarter(run.Drum);
                _started = EditorApplication.timeSinceStartup;
                _stage = 1;
            }
            if (_stage == 1)
            {
                if (G.Enemies.Count < 6 || G.EnemySpawner.IsSpawning)
                {
                    return;
                }
                for (int slot = 0; slot < 6; slot++)
                {
                    run.Drum.Cartridges[slot] = AmmoItem.Ordinary();
                    run.Drum.Sockets[slot] = null;
                }
                run.Drum.Cartridges[0].Properties.Pierce = 1;
                run.Drum.Rebuild();
                Require(run.Shooter.TryFire(), "Piercing validation shot rejected");
                _stage = 2;
                return;
            }
            if (_stage == 2)
            {
                if (run.Shooter.CompletedShots == 0)
                {
                    return;
                }
                Require(G.Enemies.Count(enemy => Mathf.Approximately(enemy.Health, 20)) == 2,
                    "Piercing projectile must damage two aligned enemies");
                Debug.Log("REAL_PIERCING_HIT_PASSED: one projectile damaged two enemies");
                for (int slot = 0; slot < 6; slot++)
                {
                    run.Drum.Cartridges[slot] = new AmmoItem
                    {
                        Name = "Validation ammo",
                        Properties = new AmmoProperties { More = 400, Pierce = 3, Ricochet = 2, Fan = 2 }
                    };
                }
                run.Drum.Rebuild();
                _stage = 3;
            }
            if (run.AwaitingReward)
            {
                Require(run.Round >= 1 && run.Round < 5, "Reward outside a wave break");
                Require(run.Offers.Length == 3, "Expected three reward offers");
                bool expectedModule = _rewards % 2 == 1;
                Require(run.ChoosingModule == expectedModule, "Expected bullet then module reward");
                run.Choose(0, 0);
                _rewards++;
                return;
            }
            if (run.Victory)
            {
                Require(_rewards == 8, "Expected eight rewards between five waves");
                Require(VisitedGroups.Count == 37, "Not all groups were played");
                Require(_sawBoss, "Final boss did not appear");
                Debug.Log("FIVE_WAVE_RUN_VALIDATION_PASSED: 5 waves, 37 sequential groups, 8 rewards, real piercing");
                Finish(0);
                return;
            }
            if (run.CombatActive)
            {
                Time.timeScale = 8;
                string group = run.Round + ":" + run.Pack;
                if (group != _currentGroup)
                {
                    Require(_previousGroup.All(enemy => enemy == null || !enemy.IsAlive), "Groups overlapped");
                    _currentGroup = group;
                    VisitedGroups.Add(group);
                    _previousGroup = new Enemy[0];
                }
                _previousGroup = _previousGroup.Concat(G.Enemies).Distinct().ToArray();
                _sawBoss |= G.Enemies.Any(enemy => enemy.MaxHealth == 200);
                if (G.Enemies.Count > 0)
                {
                    run.Shooter.TryFire();
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static void Finish(int exitCode)
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.isPlaying = false;

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSettings.enterPlayModeOptionsEnabled = _originalOptionsEnabled;
            EditorSettings.enterPlayModeOptions = _originalOptions;
            EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
