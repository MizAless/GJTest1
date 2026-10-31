using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BandolierAmmoArt
{
    public const string ArtFolder = "Assets/_game/Art/Bandolier/ModularAmmo/";
    public const string PrefabPath = "Assets/_game/Prefabs/CartridgeVisual.prefab";
    private const string WidthKey = "CartridgeVisualWidthScale=";
    private static readonly string[] PreviewTypes = { "Plain", "Needle", "Lava", "Ice", "Ricochet", "Fan" };

    private sealed class Calibration
    {
        public string TexturePath;
        public Vector2 PixelsToLocal;
        public Vector2 Offset;
        public Vector2[] PixelSamples;
        public Vector3[] ExpectedPositions;
        public int Slot;
    }

    [MenuItem("Tools/Prototype/Apply Cartridge Art To Bandolier Preview")]
    public static void ApplyToScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        var scene = EditorSceneManager.OpenScene(BandolierPreviewAuthoring.ScenePath);
        ApplyToController(UnityEngine.Object.FindAnyObjectByType<BandolierPreview>());
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void ApplyToController(BandolierPreview controller)
    {
        if (controller == null)
        {
            throw new InvalidOperationException("Bandolier preview controller is missing.");
        }
        var settings = new SerializedObject(controller);
        SerializedProperty cartridges = settings.FindProperty("_cartridges");
        SerializedProperty shadows = settings.FindProperty("_cartridgeShadows");
        var calibrations = new List<Calibration>();

        // Read every old mesh before reimporting any sprite. This captures the user's actual
        // local transforms, including the needle's non-uniform width, without legacy scale constants.
        for (int slot = 0; slot < cartridges.arraySize; slot++)
        {
            var root = (Transform)cartridges.GetArrayElementAtIndex(slot).objectReferenceValue;
            if (root.GetComponent<CartridgeVisual>() != null || root.Find("Tip") == null)
            {
                continue;
            }
            if (slot == 0)
            {
                calibrations.Add(Capture(root, root.GetComponent<MeshFilter>(), ArtFolder + "Casing2.png", -1));
            }
            calibrations.Add(Capture(root, root.Find("Tip").GetComponent<MeshFilter>(),
                ArtFolder + PreviewTypes[slot] + "Tip.png", slot));
        }
        foreach (Calibration calibration in calibrations)
        {
            ApplyCalibration(calibration);
        }
        EnsurePrefab();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        for (int slot = 0; slot < cartridges.arraySize; slot++)
        {
            var oldRoot = (Transform)cartridges.GetArrayElementAtIndex(slot).objectReferenceValue;
            CartridgeVisual visual = oldRoot.GetComponent<CartridgeVisual>();
            if (visual == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, oldRoot.parent);
                Transform replacement = instance.transform;
                replacement.name = oldRoot.name;
                replacement.SetSiblingIndex(oldRoot.GetSiblingIndex());
                replacement.localPosition = oldRoot.localPosition;
                replacement.localRotation = oldRoot.localRotation;
                replacement.localScale = oldRoot.localScale;
                visual = instance.GetComponent<CartridgeVisual>();
                Sprite tip = LoadSprite(PreviewTypes[slot] + "Tip");
                visual.SetSprites(LoadSprite("Casing2"), tip, GetSuggestedWidthScale(tip));
                cartridges.GetArrayElementAtIndex(slot).objectReferenceValue = replacement;
                shadows.GetArrayElementAtIndex(slot).objectReferenceValue = visual.CasingShadow;
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);
            }
            else
            {
                visual.Rebuild();
            }
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        controller.RefreshCartridgeLayers();
        controller.Scrub(1);
        ValidatePreservedFit(controller, calibrations);
        EditorUtility.SetDirty(controller);
    }

    private static Calibration Capture(Transform root, MeshFilter filter, string texturePath, int slot)
    {
        Mesh mesh = filter.sharedMesh;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = mesh.uv;
        var positions = new Vector3[vertices.Length];
        var pixels = new Vector2[vertices.Length];
        for (int index = 0; index < vertices.Length; index++)
        {
            positions[index] = root.InverseTransformPoint(filter.transform.TransformPoint(vertices[index]));
            pixels[index] = Vector2.Scale(uv[index], new Vector2(texture.width, texture.height));
        }
        Vector2 scale = new Vector2(
            (positions[2].x - positions[0].x) / (pixels[2].x - pixels[0].x),
            (positions[1].y - positions[0].y) / (pixels[1].y - pixels[0].y));
        if (scale.x <= 0 || scale.y <= 0)
        {
            throw new InvalidOperationException("Expected axis-aligned positive manual scaling: " + texturePath);
        }
        return new Calibration
        {
            TexturePath = texturePath,
            PixelsToLocal = scale,
            Offset = new Vector2(positions[0].x - pixels[0].x * scale.x,
                positions[0].y - pixels[0].y * scale.y),
            PixelSamples = pixels,
            ExpectedPositions = positions,
            Slot = slot
        };
    }

    private static void ApplyCalibration(Calibration calibration)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(calibration.TexturePath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(calibration.TexturePath);
        Vector2 pixelPivot = new Vector2(-calibration.Offset.x / calibration.PixelsToLocal.x,
            -calibration.Offset.y / calibration.PixelsToLocal.y);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(pixelPivot.x / texture.width, pixelPivot.y / texture.height);
        settings.spritePixelsPerUnit = 1 / calibration.PixelsToLocal.y;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        float widthScale = calibration.PixelsToLocal.x / calibration.PixelsToLocal.y;
        string[] userLines = importer.userData.Split('\n');
        importer.userData = string.Join("\n", userLines.Where(line => !line.StartsWith(WidthKey) && line.Length > 0))
            + "\n" + WidthKey + widthScale.ToString("R", CultureInfo.InvariantCulture);
        importer.SaveAndReimport();
    }

    public static float GetSuggestedWidthScale(Sprite sprite)
    {
        if (sprite == null)
        {
            return 1;
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
        foreach (string line in importer.userData.Split('\n'))
        {
            if (line.StartsWith(WidthKey))
            {
                return float.Parse(line.Substring(WidthKey.Length), CultureInfo.InvariantCulture);
            }
        }
        return 1;
    }

    private static void ValidatePreservedFit(BandolierPreview controller, List<Calibration> calibrations)
    {
        var settings = new SerializedObject(controller);
        var cartridges = settings.FindProperty("_cartridges");
        float maximumError = 0;
        foreach (Calibration calibration in calibrations)
        {
            int slot = Mathf.Max(0, calibration.Slot);
            var root = (Transform)cartridges.GetArrayElementAtIndex(slot).objectReferenceValue;
            CartridgeVisual visual = root.GetComponent<CartridgeVisual>();
            Sprite sprite = calibration.Slot < 0 ? visual.CasingSprite : visual.TipSprite;
            float width = calibration.Slot < 0 ? 1 : visual.TipWidthScale;
            for (int index = 0; index < calibration.PixelSamples.Length; index++)
            {
                Vector2 position = (calibration.PixelSamples[index] - sprite.pivot) / sprite.pixelsPerUnit;
                position.x *= width;
                Vector2 expected = calibration.ExpectedPositions[index];
                maximumError = Mathf.Max(maximumError, Vector2.Distance(position, expected));
            }
        }
        if (maximumError > 0.0001f)
        {
            throw new InvalidOperationException("Manual fitting changed by " + maximumError + " world units.");
        }
        Debug.Log("CARTRIDGE_MANUAL_FIT_PRESERVED: maximum error " + maximumError);
    }

    public static Sprite LoadSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + name + ".png");
    }

    private static void EnsurePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            return;
        }
        Shader shader = Shader.Find("Game/Bandolier Curl");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
        {
            throw new InvalidOperationException("Bandolier shader is missing or invalid.");
        }
        Material material = CreateMaterial("CartridgeVisual", shader, false);
        Material shadowMaterial = CreateMaterial("CartridgeVisualShadow", shader, true);
        GameObject root = new GameObject("Cartridge visual");
        MeshRenderer casing = CreateLayer(root.transform, "Casing", material);
        MeshRenderer tip = CreateLayer(root.transform, "Tip", material);
        Transform shadows = new GameObject("Shadows").transform;
        shadows.SetParent(root.transform, false);
        MeshRenderer casingShadow = CreateLayer(shadows, "Casing shadow", shadowMaterial);
        MeshRenderer tipShadow = CreateLayer(shadows, "Tip shadow", shadowMaterial);
        CartridgeVisual visual = root.AddComponent<CartridgeVisual>();
        Sprite plain = LoadSprite("PlainTip");
        visual.Configure(LoadSprite("Casing2"), plain, GetSuggestedWidthScale(plain),
            casing, tip, casingShadow, tipShadow, shadows);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static MeshRenderer CreateLayer(Transform parent, string name, Material material)
    {
        var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.SetParent(parent, false);
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static Material CreateMaterial(string name, Shader shader, bool shadow)
    {
        string path = ArtFolder + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.mainTexture = LoadSprite("Casing2").texture;
        material.color = shadow ? new Color(0, 0, 0, 0.52f) : Color.white;
        material.SetFloat("_Rigid", 1);
        material.SetFloat("_ZWrite", shadow ? 0 : 1);
        material.renderQueue = shadow ? 2451 : 2450;
        return material;
    }

    public static void ValidateInBatch()
    {
        CartridgeVisualValidation.Run();
    }
}
