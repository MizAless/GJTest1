using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CombatPresentationValidation
{
    private static bool _checked;
    private static int _frames;
    public static void Run()
    {
        EditorApplication.update += Check;
        StaticShooterValidation.Run();
    }

    private static void Check()
    {
        if (!Application.isPlaying || StaticShooterStage.Instance == null || G.Enemies.Count < 3 || _checked)
        {
            return;
        }
        if (++_frames < 8)
        {
            return;
        }
        try
        {
            Enemy enemy = G.Enemies.First();
            Transform bar = enemy.transform.Find("Health background");
            SpriteRenderer background = bar.GetComponent<SpriteRenderer>();
            SpriteRenderer fill = bar.Find("Health").GetComponent<SpriteRenderer>();
            Require(fill.sortingOrder > background.sortingOrder, "HP fill must render in front of background.");
            Require(!bar.gameObject.activeSelf && !enemy.HasTakenDamage, "Untouched enemy HP must be hidden.");
            float edge = fill.transform.localPosition.x - fill.sprite.bounds.extents.x * fill.transform.localScale.x;
            enemy.TakeDamage(10, Color.white, Vector3.zero);
            Require(bar.gameObject.activeSelf && enemy.HasTakenDamage, "First damage must reveal HP.");
            Require(Mathf.Abs(enemy.Health - 20) < 0.01f, "First hit must leave 20/30 HP.");
            Require(Mathf.Abs(fill.transform.localScale.x - 2f / 3) < 0.001f, "Fill must show two thirds.");
            enemy.TakeDamage(10, Color.white, Vector3.zero);
            Require(Mathf.Abs(fill.transform.localScale.x - 1f / 3) < 0.001f, "Fill must show one third.");
            float newEdge = fill.transform.localPosition.x - fill.sprite.bounds.extents.x * fill.transform.localScale.x;
            Require(Mathf.Abs(edge - newEdge) < 0.001f, "Left edge of health fill must stay anchored.");
            RevolverVisual weapon = StaticShooterStage.Instance.Weapon;
            Require(weapon.GetComponentInParent<GunslingerVisual>() != null, "Weapon must attach to character prefab.");
            weapon.AimAt(enemy.AimPosition, Vector3.up);
            Require(Vector3.Dot(weapon.Muzzle.forward, (enemy.AimPosition - weapon.Muzzle.position).normalized) > 0.999f,
                "Muzzle must point along the actual 3D shot direction.");
            StaticShooterArt art = AssetDatabase.LoadAssetAtPath<StaticShooterArt>(StaticShooterAuthoring.ArtAsset);
            Camera camera = Camera.main;
            for (int index = 0; index < 3; index++)
            {
                ProjectileVisual visual = UnityEngine.Object.Instantiate(art.ProjectilePrefab).GetComponent<ProjectileVisual>();
                visual.transform.position = camera.ViewportToWorldPoint(new Vector3(0.48f + index * 0.14f, 0.53f, 14));
                AmmoProperties properties = new AmmoProperties();
                if (index == 0) properties.Burn = 1;
                if (index == 1) properties.Chill = 1;
                if (index == 2) properties.Poison = 1;
                CombatProfile profile = new CombatProfile();
                profile.Apply(properties);
                visual.Initialize(profile);
            }
            CaptureEffects();
            _checked = true;
            Debug.Log("COMBAT_PRESENTATION_VALIDATION_PASSED: HP fractions/anchored edge/sorting, nested prefabs, 3D muzzle alignment, three readable projectile profiles");
            EditorApplication.update -= Check;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void CaptureEffects()
    {
        Explosion explosion = UnityEngine.Object.Instantiate(G.DataBase.ExplosionPrefab, new Vector3(3, 0, 9), Quaternion.identity);
        explosion.Visual(CombatEffects.Radius, Color.yellow);
        foreach (ParticleSystem particles in explosion.GetComponentsInChildren<ParticleSystem>())
        {
            particles.Simulate(0.12f, false, true);
        }
        Camera camera = Camera.main;
        RenderTexture target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        Texture2D pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        pixels.Apply();
        Directory.CreateDirectory(".utmp/Artifacts");
        File.WriteAllBytes(".utmp/Artifacts/combat-effects.png", pixels.EncodeToPNG());
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
