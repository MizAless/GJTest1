using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnemyPressureValidation
{
    private static int _phase;
    private static float _time;
    private static double _started;
    private static bool _optionsEnabled;
    private static EnterPlayModeOptions _options;
    private static Enemy _target;
    private static Vector3 _position;
    private static int _defeatPack;

    public static void Run()
    {
        float previous = 5f;
        int count = 0;
        for (int wave = 1; wave <= GameScript.WaveCount; wave++)
        {
            for (int group = 0; group < GameScript.Round(wave).Length; group++)
            {
                float cooldown = GameScript.GroupCooldown(wave, group);
                Require(cooldown <= previous && cooldown >= 2f, "Cooldown must decrease toward two seconds");
                previous = cooldown;
                count++;
            }
        }
        Require(count == 37 && Mathf.Approximately(previous, 2f), "Incorrect final cooldown");
        Require(Mathf.Approximately(GameScript.GroupCooldown(1, 0), 5f), "Incorrect first cooldown");
        Enemy prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/_game/Prefabs/Enemy.prefab");
        Require(Mathf.Approximately(new SerializedObject(prefab).FindProperty("_speed").floatValue, 4.8f), "Prefab speed must triple");
        EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
        _optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _options = EditorSettings.enterPlayModeOptions;
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
            EditorApplication.QueuePlayerLoopUpdate();
            Require(EditorApplication.timeSinceStartup - _started < 180, "Pressure validation timed out");
            if (EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
                return;
            }
            RunDirector run = RunDirector.Instance;
            if (!Application.isPlaying || run == null || run.Shooter == null || run.PlayerHealth == null)
            {
                return;
            }
            if (_phase == 0 && G.Enemies.Count == 3 && !G.EnemySpawner.IsSpawning)
            {
                _target = G.Enemies[0];
                _position = _target.transform.position;
                _time = Time.time;
                _phase = 1;
            }
            else if (_phase == 1 && Time.time - _time >= 0.25f)
            {
                float speed = Vector3.Distance(_position, _target.transform.position) / (Time.time - _time);
                Require(Mathf.Abs(speed - 4.8f) < 0.6f, "Actual movement did not triple: " + speed);
                KillAll();
                _time = Time.time;
                _phase = 2;
            }
            else if (_phase == 2 && run.Pack == 2)
            {
                float delay = Time.time - _time;
                Require(delay >= 0.18f && delay < 0.5f, "Early clear delay incorrect: " + delay);
                _time = Time.time;
                _phase = 3;
            }
            else if (_phase == 3)
            {
                // Keep survivors distant so contact cannot interfere with the timer assertion.
                foreach (Enemy enemy in G.Enemies)
                {
                    enemy.transform.position = new Vector3(0, 0, 40);
                }
                if (run.Pack == 3)
                {
                    float delay = Time.time - _time;
                    Require(Mathf.Abs(delay - GameScript.GroupCooldown(1, 1)) < 0.3f, "Survivors blocked timed spawn");
                    Require(G.Enemies.Count >= 4 && !run.AwaitingReward, "Expected overlapping groups");
                    _phase = 4;
                }
            }
            else if (_phase == 4 && !G.EnemySpawner.IsSpawning)
            {
                Require(G.Enemies.Count == 6, "Expected two full overlapping groups");
                foreach (Enemy enemy in G.Enemies.Skip(3).ToArray())
                {
                    enemy.TakeDamage(10000, Color.white, Vector3.zero);
                }
                _time = Time.time;
                _phase = 5;
            }
            else if (_phase == 5 && Time.time - _time > 0.5f)
            {
                Require(!run.AwaitingReward && run.CombatActive, "Reward opened with older survivors");
                KillAll();
                _phase = 6;
            }
            else if (_phase == 6 && run.AwaitingReward)
            {
                run.Choose(0, 0);
                Require(run.ChoosingModule, "Bullet reward must precede module");
                run.Choose(0, 0);
                Require(run.ContinueAfterRewards(), "Next wave must start explicitly after rewards");
                _phase = 7;
            }
            else if (_phase == 7 && run.Defeated)
            {
                Require(run.PlayerHealth.IsDead && !run.CombatActive, "Idle player must lose from contact");
                _defeatPack = run.Pack;
                _time = Time.realtimeSinceStartup;
                _phase = 8;
            }
            else if (_phase == 8 && Time.realtimeSinceStartup - _time > 0.4f)
            {
                Require(run.Pack == _defeatPack && !run.AwaitingReward && !run.Victory, "Run continued after defeat");
                Debug.Log("ENEMY_PRESSURE_VALIDATION_PASSED: triple movement, 5-to-2 cooldowns, early clear, overlap, final survivors, rewards, natural defeat");
                Finish(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static void KillAll()
    {
        foreach (Enemy enemy in G.Enemies.ToArray())
        {
            enemy.TakeDamage(10000, Color.white, Vector3.zero);
        }
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged += ExitAfterPlay;
        EditorApplication.isPlaying = false;
        void ExitAfterPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }
            EditorApplication.playModeStateChanged -= ExitAfterPlay;
            EditorSettings.enterPlayModeOptionsEnabled = _optionsEnabled;
            EditorSettings.enterPlayModeOptions = _options;
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
