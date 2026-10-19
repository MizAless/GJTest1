using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PrototypeArtValidation
{
    public static void Run()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
            int sprites = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (r.sprite == null) throw new Exception("Missing scene sprite: " + r.name);
                    sprites++;
                }
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                        throw new Exception("Missing script: " + t.name);
            }
            foreach (var path in new[] {"Enemy", "Bullet", "PrototypeDungeon", "ShootVFX", "OnDeadVFX", "Explosion"})
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_game/Prefabs/" + path + ".prefab");
                if (prefab == null) throw new Exception("Missing prefab " + path);
                foreach (var r in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                    if (r.sprite == null) throw new Exception("Missing prefab sprite " + path);
                foreach (var r in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    if (r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name.Contains("Error"))
                        throw new Exception("Invalid particle material " + path);
            }
            var card = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_game/Prefabs/UI/UpgradeTemplate.prefab");
            if (card.GetComponentInChildren<UnityEngine.UI.Image>(true).sprite == null)
                throw new Exception("Missing upgrade card sprite");
            var camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Artifacts");
            File.WriteAllBytes("Artifacts/prototype-art.png", image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("PROTOTYPE_ART_VALIDATED: " + sprites + " scene sprites; all prefab references and particle shaders valid.");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }
}
