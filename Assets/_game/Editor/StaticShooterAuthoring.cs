using System.IO;
using UnityEditor;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StaticShooterAuthoring
{
    public const string ArtFolder = "Assets/_game/Art/StaticShooter";
    public const string ArtAsset = ArtFolder + "/StaticShooterArt.asset";

    [MenuItem("Tools/Prototype/Build static shooter scene")]
    public static void Build()
    {
        string[] names = { "Backdrop", "Ground", "SideRuins", "Foreground", "Hero", "Revolver", "Enemy" };
        foreach (string name in names)
        {
            string path = ArtFolder + "/" + name + ".png";
            AssetDatabase.ImportAsset(path);
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
            settings.spritePivot = name == "Enemy" ? new Vector2(0.5f, 0.035f) : new Vector2(0.5f, 0.5f);
            if (name == "Revolver")
            {
                settings.spritePivot = new Vector2(0.23f, 0.30f);
            }
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
        StaticShooterArt art = AssetDatabase.LoadAssetAtPath<StaticShooterArt>(ArtAsset);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<StaticShooterArt>();
            AssetDatabase.CreateAsset(art, ArtAsset);
        }
        SerializedObject serializedArt = new SerializedObject(art);
        foreach (string name in names)
        {
            string path = ArtFolder + "/" + name + ".png";
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                throw new System.InvalidOperationException("Sprite import did not produce an asset: " + path);
            }
            serializedArt.FindProperty(name).objectReferenceValue = sprite;
        }
        serializedArt.ApplyModifiedPropertiesWithoutUndo();
        Sprite enemy = art.Enemy;
        TextureImporter enemyImporter = (TextureImporter)AssetImporter.GetAtPath(ArtFolder + "/Enemy.png");
        enemyImporter.spritePixelsPerUnit = enemy.texture.height / 3.2f;
        enemyImporter.SaveAndReimport();
        BuildGroundMaterial(art);
        CombatReadabilityAuthoring.Build();
        EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
        StaticShooterStage stage = Object.FindAnyObjectByType<StaticShooterStage>();
        if (stage == null)
        {
            stage = new GameObject("Static western stage").AddComponent<StaticShooterStage>();
        }
        SerializedObject serializedStage = new SerializedObject(stage);
        serializedStage.FindProperty("_art").objectReferenceValue = art;
        serializedStage.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(stage.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("STATIC_SHOOTER_ASSETS_BUILT");
    }

    private static void BuildGroundMaterial(StaticShooterArt art)
    {
        string texturePath = ArtFolder + "/GroundTile.png";
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        string path = ArtFolder + "/Ground.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Unlit/Texture"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        material.mainTextureScale = new Vector2(15, 15);
        art.GroundMaterial = material;
        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(art);
    }
}
