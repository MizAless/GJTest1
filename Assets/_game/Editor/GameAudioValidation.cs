using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class GameAudioValidation
{
    private static readonly Dictionary<SoundCue, int> Counts = new Dictionary<SoundCue, int>();
    private static bool _playing;
    private static int _stage;
    private static double _started;
    private static double _stageStart;
    private static Enemy _first;
    private static Enemy _second;

    public static void Run()
    {
        try
        {
            GameAudioAuthoring.CreateBank();
            EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.update += Tick;
            _started = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _playing = true;
            GameAudio.Played += cue => Counts[cue] = Count(cue) + 1;
        }
    }

    private static int Count(SoundCue cue)
    {
        return Counts.TryGetValue(cue, out int count) ? count : 0;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - _started > 150)
            {
                throw new Exception("Audio validation timed out at stage " + _stage);
            }
            if (!_playing)
            {
                return;
            }

            RunDirector run = RunDirector.Instance;
            if (_stage == 0)
            {
                if (run == null || run.Shooter == null || !run.CombatActive)
                {
                    return;
                }
                ValidateBank();
                run.StopAllCoroutines();
                G.EnemySpawner.StopAllCoroutines();
                foreach (Enemy enemy in G.Enemies.ToArray())
                {
                    Object.DestroyImmediate(enemy.gameObject);
                }

                _first = CreateEnemy(run.Shooter.transform.position + Vector3.right * 2);
                _second = CreateEnemy(run.Shooter.transform.position + Vector3.right * 4);
                for (int index = 0; index < 6; index++)
                {
                    run.Drum.Cartridges[index] = AmmoItem.Ordinary();
                    run.Drum.Sockets[index] = null;
                }
                run.Drum.Cartridges[0].Properties.Ricochet = 1;
                run.Drum.Rebuild();
                Assert(run.Shooter.TryFire(), "Real shot failed.");
                NextStage();
            }
            else if (_stage == 1 && _first.Health < 50 && _second.Health < 50)
            {
                Assert(Count(SoundCue.Shot) > 0 && Count(SoundCue.EnemyHit) > 0
                    && Count(SoundCue.Ricochet) > 0 && Count(SoundCue.EnemyPain) > 0, "Projectile hooks did not emit their sounds.");
                CombatEffects.Area(_second.transform.position, 1, Color.yellow);
                Assert(Count(SoundCue.Explosion) > 0, "Explosion hook missing.");
                run.Drum.SwapCartridges(0, 1);
                Assert(Count(SoundCue.Swap) > 0, "Swap hook missing.");
                _first.transform.position = run.Shooter.transform.position + Vector3.right * 0.3f;
                NextStage();
            }
            else if (_stage == 2 && run.PlayerHealth.Health < 100)
            {
                Assert(Mathf.Approximately(run.PlayerHealth.Health, 90), "Contact damage must be 10.");
                Assert(!run.PlayerHealth.TryTakeDamage(10), "Invulnerability did not block a duplicate hit.");
                Assert(Count(SoundCue.HeroImpact) > 0 && Count(SoundCue.HeroPain) > 0, "Hero damage layers missing.");
                _first.TakeDamage(10000, Color.white, Vector3.zero);
                _second.TakeDamage(10000, Color.white, Vector3.zero);
                Assert(Count(SoundCue.EnemyDeath) > 0, "Enemy death hook missing.");
                ValidatePoolAndRandom();
                NextStage();
            }
            else if (_stage == 3 && EditorApplication.timeSinceStartup - _stageStart > 0.9)
            {
                Assert(run.PlayerHealth.TryTakeDamage(10000), "Fatal damage failed.");
                Assert(run.Defeated && run.PlayerHealth.IsDead && Time.timeScale == 0, "Defeat state did not pause combat.");
                Assert(Count(SoundCue.HeroDeath) == 1 && Count(SoundCue.Defeat) == 1, "Death/defeat must play exactly once.");
                Assert(!run.PlayerHealth.TryTakeDamage(10000), "Dead hero accepted another hit.");
                GameAudio.Play(SoundCue.Select);
                Assert(GameAudio.Instance.ActiveVoiceCount > 0, "Paused audio did not stay scheduled.");
                EditorSceneManager.LoadSceneInPlayMode("Assets/_game/Scenes/BandolierPreview.unity", new LoadSceneParameters(LoadSceneMode.Single));
                NextStage();
            }
            else if (_stage == 4 && EditorApplication.timeSinceStartup - _stageStart > 0.5)
            {
                Assert(Count(SoundCue.BandolierOpen) > 0, "Bandolier animation audio did not start.");
                EditorSceneManager.LoadSceneInPlayMode("Assets/_game/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
                NextStage();
            }
            else if (_stage == 5 && run != null && run.Shooter != null)
            {
                if (run.AwaitingReward)
                {
                    run.Choose(0, 0);
                }
                else if (!run.Victory)
                {
                    Time.timeScale = 4;
                    foreach (Enemy enemy in G.Enemies.ToArray())
                    {
                        if (enemy != null)
                        {
                            enemy.TakeDamage(10000, Color.white, Vector3.zero);
                        }
                    }
                }

                if (run.Victory)
                {
                    Assert(Count(SoundCue.WaveWin) >= 4 && Count(SoundCue.GameWin) == 1, "Wave/game victory hooks missing.");
                    Assert(!run.Defeated, "Victory run was also defeated.");
                    Directory.CreateDirectory("Artifacts");
                    File.WriteAllText("Artifacts/audio-validation.txt", "PASS: 16 cue definitions; finite PCM with headroom; real shot/hit/ricochet/explosion/death; contact damage; invulnerability; one defeat; paused UI audio; animation cue; fixed pool/rate limits; gameplay RNG unchanged; 5 waves and final victory.\n"
                        + string.Join("\n", Counts.Select(pair => pair.Key + ": " + pair.Value)));
                    Debug.Log("GAME_AUDIO_VALIDATION_PASSED");
                    EditorApplication.update -= Tick;
                    EditorApplication.playModeStateChanged += ExitAfterPlay;
                    EditorApplication.isPlaying = false;
                }
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static Enemy CreateEnemy(Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_game/Prefabs/Enemy.prefab");
        Enemy enemy = Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<Enemy>();
        enemy.Configure(50);
        var serialized = new SerializedObject(enemy);
        serialized.FindProperty("_speed").floatValue = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        G.Enemies.Add(enemy);
        return enemy;
    }

    private static void ValidateBank()
    {
        GameAudioBank bank = Resources.Load<GameAudioBank>("Audio/GameAudioBank");
        Assert(bank != null && bank.Sounds.Length == Enum.GetValues(typeof(SoundCue)).Length, "Audio bank is incomplete.");
        foreach (SoundDefinition definition in bank.Sounds)
        {
            Assert(definition.Clips.Length > 0, "Empty cue: " + definition.Cue);
            foreach (AudioClip clip in definition.Clips)
            {
                Assert(clip != null && clip.length > 0.025f, "Missing/empty clip.");
                clip.LoadAudioData();
                var data = new float[clip.samples * clip.channels];
                Assert(clip.GetData(data, 0), "Could not read PCM: " + clip.name);
                Assert(data.All(value => !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value) < 0.91f), "Invalid/clipped source: " + clip.name);
            }
        }
    }

    private static void ValidatePoolAndRandom()
    {
        AudioSafetyLimiter limiter = Object.FindAnyObjectByType<AudioSafetyLimiter>();
        Assert(limiter != null, "Listener peak protection is missing.");
        float[] excessiveMix = { 2, -1.8f, 1.4f, -1.2f };
        typeof(AudioSafetyLimiter).GetMethod("OnAudioFilterRead", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(limiter, new object[] { excessiveMix, 2 });
        Assert(excessiveMix.All(value => Mathf.Abs(value) <= 0.881f), "Master peak protection failed.");
        UnityEngine.Random.State state = UnityEngine.Random.state;
        float expected = UnityEngine.Random.value;
        UnityEngine.Random.state = state;
        GameAudio.Play(SoundCue.Select);
        Assert(UnityEngine.Random.value == expected, "Audio variation changed gameplay RNG.");
        UnityEngine.Random.state = state;
        int before = GameAudio.Instance.DroppedVoices;
        for (int index = 0; index < 200; index++)
        {
            GameAudio.Play(SoundCue.EnemyHit);
        }
        Assert(GameAudio.Instance.DroppedVoices > before, "Rate limiter did not reject the hit storm.");
        Assert(GameAudio.Instance.ActiveVoiceCount <= GameAudio.Instance.PoolSize, "Voice pool overflow.");
    }

    private static void NextStage()
    {
        _stage++;
        _stageStart = EditorApplication.timeSinceStartup;
        Debug.Log("AUDIO_VALIDATION_STAGE: " + _stage);
    }

    private static void ExitAfterPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.Exit(0);
        }
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
        {
            throw new Exception(message);
        }
    }

    private static void Fail(Exception exception)
    {
        Debug.LogException(exception);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(1);
    }
}
