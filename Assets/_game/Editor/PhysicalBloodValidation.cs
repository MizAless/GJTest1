using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PhysicalBloodValidation
{
    private static bool _ready;
    private static double _started;
    private static float _phaseTime;
    private static int _phase;
    private static ParticleSystem _blood;
    private static Vector3 _origin;

    public static void Run()
    {
        try
        {
            PhysicalBloodAuthoring.Build();
            EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    _ready = true;
                }
                if (state == PlayModeStateChange.EnteredEditMode && _phase == 3)
                {
                    EditorApplication.Exit(0);
                }
            };
            _started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - _started < 100, "VFX validation timed out.");
            if (!_ready || Time.time < 0.2f || StaticShooterStage.Instance == null || G.Enemies.Count == 0)
            {
                return;
            }

            StaticShooterStage stage = StaticShooterStage.Instance;
            if (_phase == 0)
            {
                Require(stage.GroundSurface != null, "Missing ground plane.");
                Enemy target = G.Enemies.First();
                target.TakeDamage(1, Color.red, Vector3.zero);
                Require(Object.FindObjectsByType<PhysicalBlood>(FindObjectsSortMode.None).Length > 0, "Actual hit did not spawn 3D blood.");
                target.TakeDamage(10000, Color.red, Vector3.zero);
                Require(Object.FindObjectsByType<PhysicalBlood>(FindObjectsSortMode.None).Any(effect => effect.name.StartsWith("BloodDeath3D")), "Actual death did not spawn 3D blood.");
                _origin = new Vector3(-1, 1.8f, 3);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_game/Prefabs/StaticShooter/BloodDeath3D.prefab");
                _blood = Object.Instantiate(prefab, _origin, Quaternion.identity).GetComponent<ParticleSystem>();
                _blood.useAutoRandomSeed = false;
                _blood.randomSeed = 1234;
                _blood.Play();
                Require(_blood.collision.GetPlane(0) == stage.GroundSurface, "Particle collision plane was not bound.");
                Require(_blood.main.simulationSpace == ParticleSystemSimulationSpace.World && _blood.main.gravityModifier.constant > 0, "Missing world gravity.");
                ParticleSystem flash = stage.Weapon.Muzzle.GetComponentInChildren<ParticleSystem>();
                Require(flash.main.startSize.constantMax <= 0.16f && flash.main.startLifetime.constantMax <= 0.055f, "Muzzle flash is still too large/long.");
                Require(stage.Weapon.Muzzle.localPosition.z >= 0.56f && flash.transform.localPosition.z > 0, "Flash must sit beyond the barrel.");
                stage.SetAutoAim(true);
                stage.Weapon.AimAt(G.Enemies.First().AimPosition, Camera.main.transform.up);
                stage.Weapon.Fire();
                flash.Simulate(0.02f, true, false, false);
                Require(flash.particleCount > 0, "Weapon fire did not emit the small flash.");
                Capture("muzzle-flash");
                _phaseTime = Time.time;
                _phase = 1;
            }
            else if (_phase == 1 && Time.time - _phaseTime > 0.16f)
            {
                var particles = new ParticleSystem.Particle[64];
                int count = _blood.GetParticles(particles);
                Require(count >= 35, "Death burst is missing particles.");
                Vector3[] positions = particles.Take(count).Select(particle => particle.position).ToArray();
                Require(positions.Min(point => point.x) < _origin.x - 0.15f && positions.Max(point => point.x) > _origin.x + 0.15f, "Blood lacks horizontal sphere spread.");
                Require(positions.Min(point => point.z) < _origin.z - 0.15f && positions.Max(point => point.z) > _origin.z + 0.15f, "Blood lacks actual depth spread.");
                Capture("blood-airborne");
                _phase = 2;
            }
            else if (_phase == 2 && Time.time - _phaseTime > 1.65f)
            {
                var particles = new ParticleSystem.Particle[64];
                int count = _blood.GetParticles(particles);
                Require(count >= 35, "Blood was destroyed before landing.");
                Require(_blood.GetComponent<PhysicalBlood>().SettledCount >= 35, "Blood did not settle on the floor.");
                foreach (ParticleSystem.Particle particle in particles.Take(count))
                {
                    Require(particle.position.y >= stage.GroundSurface.position.y, "Blood fell through the floor.");
                    Require(Mathf.Abs(particle.rotation3D.x - 90) < 1, "Landed blood squares must lie flat.");
                }
                Capture("blood-landed");
                Directory.CreateDirectory(".utmp/Artifacts");
                File.WriteAllText(".utmp/Artifacts/physical-blood-validation.txt", "PASS: saved scene; actual hit/death bindings; spherical XYZ spread; gravity and floor collision plane; landed flat quads; visible lifetime; muzzle position/size/lifetime and firing.");
                Debug.Log("PHYSICAL_BLOOD_VALIDATION_PASSED");
                _phase = 3;
                EditorApplication.update -= Tick;
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void Capture(string name)
    {
        Directory.CreateDirectory(".utmp/Artifacts");
        Camera camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(".utmp/Artifacts/" + name + ".png", pixels.EncodeToPNG());
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

    private static void Fail(Exception exception)
    {
        Debug.LogException(exception);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(1);
    }
}
