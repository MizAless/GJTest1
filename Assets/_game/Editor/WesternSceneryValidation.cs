using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WesternSceneryValidation
{
    private static bool _checked;

    public static void Run()
    {
        WesternPropsAuthoring.RebuildLayout();
        EditorApplication.update += Inspect;
        StaticShooterValidation.RunSavedScene();
    }

    private static void Inspect()
    {
        if (_checked || !Application.isPlaying || StaticShooterStage.Instance == null
            || G.Enemies.Count < 3)
        {
            return;
        }
        try
        {
            WesternBillboard[] props = UnityEngine.Object.FindObjectsByType<WesternBillboard>(FindObjectsSortMode.None);
            Require(props.Length == 54, "Expected all 54 scenery objects.");
            Require(props.All(prop => prop.GetComponent<SpriteRenderer>().sprite != null), "Scenery sprite missing.");
            Require(props.All(prop => prop.GetComponent<Collider>() == null), "Visual scenery must not block shots.");
            Camera camera = Camera.main;
            Transform near = props.OrderBy(prop => prop.transform.position.z).First().transform;
            Transform far = props.OrderByDescending(prop => prop.transform.position.z).First().transform;
            Vector3 rest = camera.transform.position;
            Vector3 nearBefore = camera.WorldToViewportPoint(near.position);
            Vector3 farBefore = camera.WorldToViewportPoint(far.position);
            camera.transform.position += Vector3.right * 0.4f;
            float nearMotion = Mathf.Abs(camera.WorldToViewportPoint(near.position).x - nearBefore.x);
            float farMotion = Mathf.Abs(camera.WorldToViewportPoint(far.position).x - farBefore.x);
            camera.transform.position = rest;
            Require(nearMotion > farMotion * 2, "Depth planes must produce distinct perspective parallax.");
            Capture(camera);
            _checked = true;
            Debug.Log("WESTERN_SCENERY_VALIDATION_PASSED: 54 billboard props, valid saved references, no shot blockers, near/far parallax");
            EditorApplication.update -= Inspect;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Capture(Camera camera)
    {
        RenderTexture target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        Texture2D texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        texture.Apply();
        Directory.CreateDirectory(".utmp/Artifacts");
        string file = ".utmp/Artifacts/western-scenery-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".png";
        File.WriteAllBytes(file, texture.EncodeToPNG());
        Debug.Log("WESTERN_SCENERY_CAPTURE: " + file);
        camera.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(texture);
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
