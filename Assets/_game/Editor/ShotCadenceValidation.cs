using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ShotCadenceValidation
{
    private static Shooter _shooter;
    private static BulletContainer _drum;
    private static int _stage;
    private static float _nextAttempt;
    private static double _started;
    private static bool _observedReload;
    private static bool _originalPlayModeOptionsEnabled;
    private static EnterPlayModeOptions _originalPlayModeOptions;

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        _originalPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _originalPlayModeOptions = EditorSettings.enterPlayModeOptions;
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
            double timeout = _stage == 0 ? 180 : 45;
            Require(EditorApplication.timeSinceStartup - _started < timeout, "Validation timed out");
            if (!EditorApplication.isPlaying || !Application.isPlaying)
            {
                return;
            }
            if (_stage == 0)
            {
                _started = EditorApplication.timeSinceStartup;
                Camera camera = new GameObject("Validation camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                GameObject root = new GameObject("Cadence validation");
                _drum = root.AddComponent<BulletContainer>();
                _drum.Init();
                for (int slot = 0; slot < _drum.MaxBullets; slot++)
                {
                    _drum.Cartridges[slot] = AmmoItem.Ordinary();
                    _drum.Sockets[slot] = null;
                }
                _drum.Cartridges[0].Properties.Fan = 2;
                _drum.Cartridges[1].Properties.Burst = 3;
                _drum.Rebuild();
                _shooter = root.AddComponent<Shooter>();
                SerializedObject settings = new SerializedObject(_shooter);
                settings.FindProperty("_bulletContainer").objectReferenceValue = _drum;
                settings.FindProperty("_bulletPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Bullet>("Assets/_game/Prefabs/Bullet.prefab");
                settings.ApplyModifiedPropertiesWithoutUndo();
                Require(_shooter.TryFire(), "Initial fan shot rejected");
                Require(!_shooter.TryFire(), "Immediate duplicate shot accepted");
                _nextAttempt = Time.time + 0.2f;
                _stage = 1;
                return;
            }
            if (_stage == 1 && Time.time >= _nextAttempt)
            {
                Require(_shooter.CompletedShots == 0, "Test requires fan projectiles still in flight");
                Require(_shooter.TryFire(), "Burst must launch while fan remains in flight");
                _nextAttempt = Time.time + 0.2f;
                _stage = 2;
                return;
            }
            if (_stage == 2 && Time.time >= _nextAttempt)
            {
                Require(_shooter.TryFire(), "Next shot blocked by burst or pending projectiles");
                _nextAttempt = Time.time + 0.2f;
                if (_drum.BulletsRemaining == 0)
                {
                    Require(!_shooter.TryFire(), "Empty drum accepted a shot");
                    _stage = 3;
                }
                return;
            }
            if (_stage == 3)
            {
                if (_drum.IsReloading)
                {
                    Require(_shooter.CompletedShots < 6, "Reload waited for projectile completion");
                    Require(!_shooter.TryFire(), "Reload accepted a shot");
                    _observedReload = true;
                }
                if (_observedReload && !_drum.IsReloading)
                {
                    Require(_drum.BulletsRemaining == 6, "Drum did not reload");
                    _stage = 4;
                }
            }
            if (_stage == 4 && _shooter.CompletedShots == 6)
            {
                Require(!_drum.IsReloading, "Finishing projectiles started a duplicate reload");
                Require(_drum.BulletsRemaining == 6, "Finishing projectiles changed the fresh drum");
                Debug.Log("SHOT_CADENCE_VALIDATION_PASSED");
                Finish(0);
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
        // Let runtime components and editor plugins leave Play Mode before shutting down Unity.
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.isPlaying = false;

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSettings.enterPlayModeOptionsEnabled = _originalPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = _originalPlayModeOptions;
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
