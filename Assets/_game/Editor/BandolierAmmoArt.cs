using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BandolierAmmoArt
{
    private const string ArtFolder = "Assets/_game/Art/Bandolier/Cartridges/";
    private const float ReferenceCartridgeHeight = 3.04f;
    private const float CasingSampleHeight = 0.35f;

    private static readonly string[] PreviewTypes =
    {
        "Plain", "Needle", "Lava", "Ice", "Ricochet", "Fan"
    };

    [MenuItem("Tools/Prototype/Apply Cartridge Art To Bandolier Preview")]
    public static void ApplyToScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var scene = EditorSceneManager.OpenScene(BandolierPreviewAuthoring.ScenePath);
        BandolierPreview controller = UnityEngine.Object.FindAnyObjectByType<BandolierPreview>();
        ApplyToController(controller);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void ApplyToController(BandolierPreview controller)
    {
        if (controller == null)
        {
            throw new InvalidOperationException("Bandolier preview controller is missing.");
        }

        Shader shader = Shader.Find("Game/Bandolier Curl");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
        {
            throw new InvalidOperationException("Bandolier shader is missing or has compilation errors.");
        }

        var serializedController = new SerializedObject(controller);
        SerializedProperty cartridges = serializedController.FindProperty("_cartridges");
        SerializedProperty shadows = serializedController.FindProperty("_cartridgeShadows");
        Texture2D referenceTexture = ImportTexture("Plain");
        Rect referenceBounds = FindVisibleBounds(referenceTexture);
        float referenceUnitsPerPixel = ReferenceCartridgeHeight / referenceBounds.height;
        float casingWidth = FindCasingWidth(referenceTexture, referenceBounds) * referenceUnitsPerPixel;

        for (int index = 0; index < PreviewTypes.Length; index++)
        {
            string type = PreviewTypes[index];
            Texture2D texture = ImportTexture(type);
            Rect visibleBounds = FindVisibleBounds(texture);
            Mesh mesh = CreateMesh(type, texture, visibleBounds, casingWidth);
            Material material = CreateMaterial(type, shader, texture, isShadow: false);
            Material shadowMaterial = CreateMaterial(type, shader, texture, isShadow: true);

            var cartridge = (Transform)cartridges.GetArrayElementAtIndex(index).objectReferenceValue;
            var shadow = (MeshRenderer)shadows.GetArrayElementAtIndex(index).objectReferenceValue;
            cartridge.GetComponent<MeshFilter>().sharedMesh = mesh;
            cartridge.GetComponent<MeshRenderer>().sharedMaterial = material;
            shadow.GetComponent<MeshFilter>().sharedMesh = mesh;
            shadow.sharedMaterial = shadowMaterial;
        }

        controller.Scrub(1);
    }

    private static Texture2D ImportTexture(string type)
    {
        string path = ArtFolder + type + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            throw new FileNotFoundException("Generated cartridge sprite is missing.", path);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Rect FindVisibleBounds(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int minX = texture.width;
        int minY = texture.height;
        int maxX = -1;
        int maxY = -1;
        bool hasTransparency = false;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a <= 24)
                {
                    hasTransparency = true;
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < 0 || !hasTransparency)
        {
            throw new InvalidOperationException(texture.name + " must have a transparent background.");
        }

        // Crop through UVs; the generated PNG pixels remain unchanged.
        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static int FindCasingWidth(Texture2D texture, Rect bounds)
    {
        // Sample the straight lower casing, away from the tip, markings and base rim.
        int sampleY = Mathf.RoundToInt(bounds.yMin + bounds.height * CasingSampleHeight);
        int minX = texture.width;
        int maxX = -1;
        for (int x = 0; x < texture.width; x++)
        {
            // Exclude the faint halo that some generated sprites have around the metal.
            if (texture.GetPixel(x, sampleY).a < 0.8f)
            {
                continue;
            }

            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
        }

        if (maxX < minX)
        {
            throw new InvalidOperationException(texture.name + " has no opaque casing at the sample height.");
        }

        return maxX - minX + 1;
    }

    private static Mesh CreateMesh(string type, Texture2D texture, Rect bounds, float casingWidth)
    {
        string path = ArtFolder + type + "Mesh.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew)
        {
            mesh = new Mesh();
        }

        float unitsPerPixel = casingWidth / FindCasingWidth(texture, bounds);
        float width = bounds.width * unitsPerPixel;
        float height = bounds.height * unitsPerPixel;
        float halfWidth = width * 0.5f;
        float bottom = -ReferenceCartridgeHeight * 0.5f;
        float top = bottom + height;
        Rect uv = new Rect(
            bounds.x / texture.width, bounds.y / texture.height,
            bounds.width / texture.width, bounds.height / texture.height);

        mesh.Clear();
        mesh.name = type + " rigid cartridge";
        mesh.vertices = new[]
        {
            new Vector3(-halfWidth, bottom, 0),
            new Vector3(-halfWidth, top, 0),
            new Vector3(halfWidth, bottom, 0),
            new Vector3(halfWidth, top, 0)
        };
        mesh.uv = new[]
        {
            new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax),
            new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMax, uv.yMax)
        };
        mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
        mesh.RecalculateNormals();
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(17, 6, 6));

        if (isNew)
        {
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            EditorUtility.SetDirty(mesh);
        }

        return mesh;
    }

    private static Material CreateMaterial(string type, Shader shader, Texture2D texture, bool isShadow)
    {
        string suffix = isShadow ? "Shadow" : "";
        string path = ArtFolder + type + suffix + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.mainTexture = texture;
        material.color = isShadow ? new Color(0, 0, 0, 0.52f) : Color.white;
        material.SetFloat("_Rigid", 1);
        material.SetFloat("_ZWrite", isShadow ? 0 : 1);
        material.renderQueue = isShadow ? 2451 : 2450;
        EditorUtility.SetDirty(material);
        return material;
    }

    // Runs in the isolated validation project; does not rebuild the user's scene.
    public static void ValidateInBatch()
    {
        try
        {
            ApplyToScene();
            BandolierPreview controller = UnityEngine.Object.FindAnyObjectByType<BandolierPreview>();
            controller.Scrub(0.25f);
            controller.Scrub(0.65f);
            controller.Scrub(1);

            Camera camera = Camera.main;
            var target = new RenderTexture(1600, 900, 24);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var capture = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            capture.Apply();
            Directory.CreateDirectory("Artifacts");
            File.WriteAllBytes("Artifacts/bandolier-new-cartridges.png", capture.EncodeToPNG());
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(capture);
            File.WriteAllText("Artifacts/cartridge-art-validation.txt",
                "PASS: six distinct cartridge textures and matching shadow meshes assigned; scene saved; scrub and render completed.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}

