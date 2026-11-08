using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WesternPropsAuthoring
{
    public const string ArtFolder = "Assets/_game/Art/WesternProps";
    public const string PrefabPath = "Assets/_game/Prefabs/StaticShooter/WesternScenery.prefab";
    private static readonly string[] Names =
    {
        "DryGrass", "Pebbles", "Boulder", "Barrel", "LanternPost",
        "Saloon", "FrontierHouse", "Fence", "Banner", "TimberPlatform"
    };

    [MenuItem("Tools/Prototype/Build western scenery")]
    public static void Build()
    {
        Build(false);
    }

    [MenuItem("Tools/Prototype/Rebuild western scenery layout")]
    public static void RebuildLayout()
    {
        Build(true);
    }

    private static void Build(bool rebuild)
    {
        foreach (string name in Names)
        {
            string path = SpritePath(name);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = GroundPivot(path);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        GameObject scenery = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (scenery == null || rebuild)
        {
            GameObject root = new GameObject("Western scenery");
            Place(root.transform, "Saloon", new Vector3(-19, 0, 38), 14, -220);
            Place(root.transform, "FrontierHouse", new Vector3(21, 0, 48), 13, -220);
            Place(root.transform, "FrontierHouse", new Vector3(-26, 0, 62), 14, -225);
            Place(root.transform, "Saloon", new Vector3(29, 0, 75), 16, -225);
            Place(root.transform, "LanternPost", new Vector3(8, 0, 6), 8, 74);
            Place(root.transform, "Banner", new Vector3(-8, 0, 16), 6.8f, 64);
            Place(root.transform, "Banner", new Vector3(10, 0, 31), 7.5f, 49);
            Place(root.transform, "Fence", new Vector3(-8.5f, 0, 12), 2.2f, 68);
            Place(root.transform, "Fence", new Vector3(9, 0, 25), 2.3f, 55);
            Place(root.transform, "Fence", new Vector3(-12, 0, 32), 2.7f, 48);
            Place(root.transform, "TimberPlatform", new Vector3(-9, 0, 22), 1.7f, 58);
            Place(root.transform, "TimberPlatform", new Vector3(10, 0, 39), 1.9f, 41);
            Place(root.transform, "Barrel", new Vector3(7.2f, 0, 13), 2.1f, 67);
            Place(root.transform, "Barrel", new Vector3(8.5f, 0, 15), 1.9f, 65);
            Place(root.transform, "Barrel", new Vector3(-10, 0, 23), 2.4f, 57);
            Place(root.transform, "Boulder", new Vector3(-7.8f, 0, 8), 1.25f, 72);
            Place(root.transform, "Boulder", new Vector3(11, 0, 21), 2, 59);
            Place(root.transform, "Boulder", new Vector3(-11, 0, 37), 2.5f, 43);
            for (int index = 0; index < 24; index++)
            {
                float side = index % 2 == 0 ? -1 : 1;
                float depth = 4 + index * 1.4f;
                float horizontal = side * (5 + index % 4 * 1.4f);
                Place(root.transform, "DryGrass", new Vector3(horizontal, 0.01f, depth),
                    1.0f + index % 3 * 0.25f, 80 - Mathf.RoundToInt(depth));
            }
            for (int index = 0; index < 12; index++)
            {
                float depth = 5 + index * 2.4f;
                float horizontal = Mathf.Sin(index * 2.3f) * 5.5f;
                Place(root.transform, "Pebbles", new Vector3(horizontal, 0.01f, depth),
                    0.45f + index % 3 * 0.15f, 80 - Mathf.RoundToInt(depth));
            }
            scenery = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }
        StaticShooterArt art = AssetDatabase.LoadAssetAtPath<StaticShooterArt>(StaticShooterAuthoring.ArtAsset);
        art.WesternSceneryPrefab = scenery;
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("WESTERN_SCENERY_BUILT");
    }

    private static void Place(Transform root, string name, Vector3 position, float height, int order)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(name));
        if (sprite == null)
        {
            throw new InvalidOperationException("Missing scenery sprite: " + name);
        }
        GameObject prop = new GameObject(name, typeof(SpriteRenderer), typeof(WesternBillboard));
        prop.transform.SetParent(root, false);
        prop.transform.localPosition = position;
        prop.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
        SpriteRenderer renderer = prop.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        float haze = Mathf.InverseLerp(20, 80, position.z);
        renderer.color = Color.Lerp(Color.white, new Color(0.69f, 0.60f, 0.59f), haze * 0.65f);
    }

    private static string SpritePath(string name)
    {
        bool corrected = name == "DryGrass" || name == "Pebbles" || name == "TimberPlatform";
        return ArtFolder + "/" + name + (corrected ? "-v2" : "") + ".png";
    }

    private static Vector2 GroundPivot(string path)
    {
        // Anchor the actual silhouette to the ground, ignoring generated transparent padding.
        Texture2D source = new Texture2D(2, 2);
        source.LoadImage(File.ReadAllBytes(path));
        Color32[] pixels = source.GetPixels32();
        int firstSolidRow = 0;
        bool found = false;
        for (int row = 0; row < source.height && !found; row++)
        {
            for (int column = 0; column < source.width; column++)
            {
                if (pixels[row * source.width + column].a > 64)
                {
                    firstSolidRow = row;
                    found = true;
                    break;
                }
            }
        }
        Vector2 pivot = new Vector2(0.5f, (float)firstSolidRow / source.height);
        UnityEngine.Object.DestroyImmediate(source);
        return pivot;
    }
}
