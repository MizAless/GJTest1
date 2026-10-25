using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BandolierAmmoArt
{
    private const string ArtFolder = "Assets/_game/Art/Bandolier/ModularAmmo/";
    private const float ReferenceCartridgeHeight = 3.04f;
    private const float TipDepth = 0.004f;

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
        Texture2D casingTexture = ImportTexture("Casing");
        Texture2D plainTexture = ImportTexture("PlainTip");
        Rect casingBounds = FindVisibleBounds(casingTexture);
        Rect plainBounds = FindVisibleBounds(plainTexture);
        Vector2 casingBottom = ToSpriteUnits(casingTexture, casingBounds.min);
        Vector2 plainTop = ToSpriteUnits(plainTexture, plainBounds.max);
        float assemblyScale = ReferenceCartridgeHeight / (plainTop.y - casingBottom.y);
        float connectionHeight = -ReferenceCartridgeHeight * 0.5f - casingBottom.y * assemblyScale;
        Mesh casingMesh = CreateMesh("Casing", casingTexture, casingBounds, assemblyScale, connectionHeight, 0);
        Material casingMaterial = CreateMaterial("Casing", shader, casingTexture, isShadow: false);
        Material casingShadowMaterial = CreateMaterial("Casing", shader, casingTexture, isShadow: true);

        for (int index = 0; index < PreviewTypes.Length; index++)
        {
            string type = PreviewTypes[index];
            Texture2D texture = ImportTexture(type + "Tip");
            Mesh tipMesh = CreateMesh(type + "Tip", texture, FindVisibleBounds(texture),
                assemblyScale, connectionHeight, TipDepth);
            Material tipMaterial = CreateMaterial(type + "Tip", shader, texture, isShadow: false);
            Material tipShadowMaterial = CreateMaterial(type + "Tip", shader, texture, isShadow: true);

            var cartridge = (Transform)cartridges.GetArrayElementAtIndex(index).objectReferenceValue;
            var shadow = (MeshRenderer)shadows.GetArrayElementAtIndex(index).objectReferenceValue;
            cartridge.GetComponent<MeshFilter>().sharedMesh = casingMesh;
            cartridge.GetComponent<MeshRenderer>().sharedMaterial = casingMaterial;
            shadow.GetComponent<MeshFilter>().sharedMesh = casingMesh;
            shadow.sharedMaterial = casingShadowMaterial;
            SetPart(cartridge, "Tip", tipMesh, tipMaterial);
            SetPart(shadow.transform, "Tip shadow", tipMesh, tipShadowMaterial);
        }

        controller.RefreshCartridgeLayers();
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

    private static Vector2 ToSpriteUnits(Texture2D texture, Vector2 pixel)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
        Vector2 pivotPixels = Vector2.Scale(importer.spritePivot, new Vector2(texture.width, texture.height));
        return (pixel - pivotPixels) / importer.spritePixelsPerUnit;
    }

    private static void SetPart(Transform parent, string name, Mesh mesh, Material material)
    {
        Transform part = parent.Find(name);
        if (part == null)
        {
            part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
            part.SetParent(parent, false);
        }

        part.localPosition = Vector3.zero;
        part.localRotation = Quaternion.identity;
        part.localScale = Vector3.one;
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Mesh CreateMesh(string type, Texture2D texture, Rect bounds,
        float scale, float connectionHeight, float depth)
    {
        string path = ArtFolder + type + "Mesh.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew)
        {
            mesh = new Mesh();
        }

        Vector2 bottomLeft = ToSpriteUnits(texture, bounds.min) * scale;
        Vector2 topRight = ToSpriteUnits(texture, bounds.max) * scale;
        bottomLeft.y += connectionHeight;
        topRight.y += connectionHeight;
        Rect uv = new Rect(bounds.x / texture.width, bounds.y / texture.height,
            bounds.width / texture.width, bounds.height / texture.height);

        mesh.Clear();
        mesh.name = type + " rigid modular part";
        mesh.vertices = new[]
        {
            new Vector3(bottomLeft.x, bottomLeft.y, depth),
            new Vector3(bottomLeft.x, topRight.y, depth),
            new Vector3(topRight.x, bottomLeft.y, depth),
            new Vector3(topRight.x, topRight.y, depth)
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

    private static void ValidateAssembly(BandolierPreview controller)
    {
        var serialized = new SerializedObject(controller);
        SerializedProperty cartridges = serialized.FindProperty("_cartridges");
        Mesh sharedCasing = null;
        for (int index = 0; index < cartridges.arraySize; index++)
        {
            var cartridge = (Transform)cartridges.GetArrayElementAtIndex(index).objectReferenceValue;
            Mesh casing = cartridge.GetComponent<MeshFilter>().sharedMesh;
            if (sharedCasing != null && casing != sharedCasing)
            {
                throw new InvalidOperationException("All cartridges must share the same casing mesh.");
            }
            sharedCasing = casing;
            MeshRenderer[] layers = cartridge.GetComponentsInChildren<MeshRenderer>(true);
            if (layers.Length != 4 || cartridge.Find("Tip") == null)
            {
                throw new InvalidOperationException("Expected casing, tip and two shadow layers.");
            }
            foreach (MeshRenderer layer in layers)
            {
                if (layer.GetComponent<MeshFilter>().sharedMesh.vertexCount != 4)
                {
                    throw new InvalidOperationException("Each rigid part must be a quad.");
                }
            }
        }
    }

    // Runs in the isolated validation project; does not rebuild the user's scene.
    public static void ValidateInBatch()
    {
        try
        {
            ApplyToScene();
            BandolierPreview controller = UnityEngine.Object.FindAnyObjectByType<BandolierPreview>();
            ValidateAssembly(controller);
            controller.Scrub(0);
            foreach (MeshRenderer layer in controller.GetComponentsInChildren<MeshRenderer>())
            {
                if (layer.name == "Tip" || layer.name == "Tip shadow")
                {
                    if (layer.enabled)
                    {
                        throw new InvalidOperationException("Folded cartridge part is visible.");
                    }
                }
            }
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
                "PASS: six modular cartridges, shared casing mesh, six distinct tips, two shadow parts each; folded visibility, scrub and render completed.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}

