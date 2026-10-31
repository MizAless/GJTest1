using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UpgradeAssetsAuthoring
{
    private const string Folder = "Assets/_game/Resources/Upgrades";
    private const string ClipPath = "Assets/_game/Art/Upgrades/ModuleClip.png";
    private static readonly string[] Tips = { "Plain", "Needle", "Lava", "Ice", "Ricochet", "Fan", "Accelerator", "Conductor", "Link" };

    [MenuItem("Tools/Prototype/Upgrades/Build Presentation Assets")]
    public static void BuildAssets()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        Shader shader = Shader.Find("Game/Bandolier Curl");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
        {
            throw new InvalidOperationException("Upgrade presentation shader failed to compile.");
        }
        ExportBandolier();
        Rect clipBounds = ImportSprite(ClipPath);
        Texture2D clip = AssetDatabase.LoadAssetAtPath<Texture2D>(ClipPath);
        Mesh clipMesh = CreateClipMesh(clip, clipBounds);
        Material clipMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/ModuleClip.mat");
        if (clipMaterial == null)
        {
            clipMaterial = new Material(shader);
            AssetDatabase.CreateAsset(clipMaterial, Folder + "/ModuleClip.mat");
        }
        clipMaterial.mainTexture = clip;
        clipMaterial.SetFloat("_Rigid", 1);
        clipMaterial.renderQueue = 2452;
        EditorUtility.SetDirty(clipMaterial);
        UpgradePresentationAssets assets = AssetDatabase.LoadAssetAtPath<UpgradePresentationAssets>(Folder + "/PresentationAssets.asset");
        if (assets == null)
        {
            assets = ScriptableObject.CreateInstance<UpgradePresentationAssets>();
            AssetDatabase.CreateAsset(assets, Folder + "/PresentationAssets.asset");
        }
        assets.BandolierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/BandolierPresentation.prefab");
        assets.CartridgePrefab = AssetDatabase.LoadAssetAtPath<CartridgeVisual>(BandolierAmmoArt.PrefabPath);
        assets.ClipMesh = clipMesh;
        assets.ClipMaterial = clipMaterial;
        assets.TipSprites = new Sprite[Tips.Length];
        assets.TipWidths = new float[Tips.Length];
        for (int index = 0; index < Tips.Length; index++)
        {
            string path = BandolierAmmoArt.ArtFolder + Tips[index] + "Tip.png";
            if (index >= 6)
            {
                FitUnusedTip(path);
            }
            assets.TipSprites[index] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            assets.TipWidths[index] = BandolierAmmoArt.GetSuggestedWidthScale(assets.TipSprites[index]);
        }
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        Debug.Log("UPGRADE_PRESENTATION_ASSETS_BUILT");
    }

    private static void ExportBandolier()
    {
        Scene scene = EditorSceneManager.OpenScene(BandolierPreviewAuthoring.ScenePath, OpenSceneMode.Additive);
        BandolierPreview controller = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BandolierPreview>()).First();
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject root = UnityEngine.Object.Instantiate(controller.gameObject);
        SceneManager.MoveGameObjectToScene(root, preview);
        var settings = new SerializedObject(root.GetComponent<BandolierPreview>());
        settings.FindProperty("_slider").objectReferenceValue = null;
        settings.FindProperty("_status").objectReferenceValue = null;
        settings.FindProperty("_autoPlay").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/BandolierPresentation.prefab");
        EditorSceneManager.ClosePreviewScene(preview);
        EditorSceneManager.CloseScene(scene, true);
    }

    private static Rect ImportSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Color32[] pixels = texture.GetPixels32();
        int minX = texture.width;
        int minY = texture.height;
        int maxX = 0;
        int maxY = 0;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a > 24)
                {
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
        }
        return Rect.MinMaxRect(minX, minY, maxX + 1, maxY + 1);
    }

    private static Mesh CreateClipMesh(Texture2D texture, Rect pixels)
    {
        string path = Folder + "/ModuleClipMesh.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create = mesh == null;
        if (create)
        {
            mesh = new Mesh { name = "Module clip - full loop width and 20 percent height" };
        }
        mesh.Clear();
        mesh.vertices = new[]
        {
            new Vector3(-0.585f, -0.119f, 0), new Vector3(-0.585f, 0.119f, 0),
            new Vector3(0.585f, -0.119f, 0), new Vector3(0.585f, 0.119f, 0)
        };
        mesh.uv = new[]
        {
            new Vector2(pixels.xMin / texture.width, pixels.yMin / texture.height),
            new Vector2(pixels.xMin / texture.width, pixels.yMax / texture.height),
            new Vector2(pixels.xMax / texture.width, pixels.yMin / texture.height),
            new Vector2(pixels.xMax / texture.width, pixels.yMax / texture.height)
        };
        mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        if (create)
        {
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            EditorUtility.SetDirty(mesh);
        }
        return mesh;
    }

    private static void FitUnusedTip(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.userData.Contains("CartridgeVisualWidthScale="))
        {
            return;
        }
        Rect bounds = ImportSprite(path);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        float pixelsPerUnit = bounds.height / 1.1f;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spritePixelsPerUnit = pixelsPerUnit;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(bounds.center.x / texture.width,
            (bounds.yMin - 0.3f * pixelsPerUnit) / texture.height);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.userData += "\nCartridgeVisualWidthScale=1";
        importer.SaveAndReimport();
    }
}
