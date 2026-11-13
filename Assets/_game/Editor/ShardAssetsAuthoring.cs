using System.Collections.Generic;
using GameEconomy;
using GameEconomy.Shards;
using UnityEditor;
using UnityEngine;

public static class ShardAssetsAuthoring
{
    private const string Art = "Assets/_game/Art/Shards/";
    private const string Prefabs = "Assets/_game/Prefabs/Shards/";
    private const string Settings = "Assets/_game/Resources/Economy/Shards/";

    [MenuItem("Tools/Prototype/Shards/Build missing assets")]
    public static void Build()
    {
        ShardMotionSettings motion = AssetDatabase.LoadAssetAtPath<ShardMotionSettings>(Settings + "ShardMotion.asset");
        if (motion == null)
        {
            motion = ScriptableObject.CreateInstance<ShardMotionSettings>();
            AssetDatabase.CreateAsset(motion, Settings + "ShardMotion.asset");
        }
        ShardDropSettings drops = AssetDatabase.LoadAssetAtPath<ShardDropSettings>(Settings + "ShardDrops.asset");
        if (drops == null)
        {
            drops = ScriptableObject.CreateInstance<ShardDropSettings>();
            drops.Motion = motion;
            drops.Kinds = new[]
            {
                new ShardAppearance { Kind = ShardKind.Chaos, Prefab = BuildPickup(ShardKind.Chaos, new Color(0.85f, 0.25f, 1)) },
                new ShardAppearance { Kind = ShardKind.Ice, Prefab = BuildPickup(ShardKind.Ice, new Color(0.2f, 0.8f, 1)) },
                new ShardAppearance { Kind = ShardKind.Lava, Prefab = BuildPickup(ShardKind.Lava, new Color(1, 0.38f, 0.08f)) }
            };
            AssetDatabase.CreateAsset(drops, Settings + "ShardDrops.asset");
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "ShardDropService.prefab") == null)
        {
            GameObject root = new GameObject("Shard drop service");
            ShardDropService service = root.AddComponent<ShardDropService>();
            SerializedObject serialized = new SerializedObject(service);
            serialized.FindProperty("_settings").objectReferenceValue = drops;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "ShardDropService.prefab");
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("SHARD_ASSETS_BUILT");
    }

    private static ShardPickup BuildPickup(ShardKind kind, Color color)
    {
        string path = Prefabs + kind + "Shard.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing.GetComponent<ShardPickup>();
        }
        GameObject root = new GameObject(kind + " shard");
        ShardPickup pickup = root.AddComponent<ShardPickup>();
        Transform body = new GameObject("Crystal and quality marks").transform;
        body.SetParent(root.transform, false);
        Material[] facets =
        {
            Material(kind + " dark", color * 0.45f),
            Material(kind + " bright", color),
            Material(kind + " highlight", Color.Lerp(color, Color.white, 0.65f))
        };
        if (kind == ShardKind.Ice)
        {
            Crystal(body, "Tall spear", new Vector3(0, 0, 0), new Vector3(0.23f, 0.65f, 0.22f), facets);
            Crystal(body, "Side spear", new Vector3(0.24f, -0.1f, 0), new Vector3(0.13f, 0.38f, 0.16f), facets);
        }
        else if (kind == ShardKind.Chaos)
        {
            Crystal(body, "Split left", new Vector3(-0.18f, 0.08f, 0), new Vector3(0.24f, 0.42f, 0.25f), facets);
            Crystal(body, "Split right", new Vector3(0.19f, -0.05f, 0.03f), new Vector3(0.25f, 0.36f, 0.25f), facets);
        }
        else
        {
            Crystal(body, "Broad coal", Vector3.zero, new Vector3(0.45f, 0.29f, 0.37f), facets);
            Crystal(body, "Hot fracture", new Vector3(0.18f, 0.16f, 0), new Vector3(0.18f, 0.24f, 0.2f), facets);
        }
        Material white = Material("Quality ivory", new Color(1, 0.92f, 0.65f));
        GameObject[] marks = new GameObject[3];
        for (int index = 0; index < marks.Length; index++)
        {
            Transform mark = Crystal(body, "Quality notch " + (index + 1),
                new Vector3((index - 1) * 0.2f, 0.88f, 0), Vector3.one * 0.08f,
                new[] { white, white, white });
            marks[index] = mark.gameObject;
        }
        GameObject shadow = new GameObject("Soft XZ shadow", typeof(SpriteRenderer));
        shadow.transform.SetParent(root.transform, false);
        shadow.transform.localRotation = Quaternion.Euler(90, 0, 0);
        SpriteRenderer renderer = shadow.GetComponent<SpriteRenderer>();
        renderer.sprite = ShadowSprite();
        renderer.color = new Color(0.08f, 0.035f, 0.025f, 0.7f);
        renderer.sortingOrder = 1;
        SerializedObject serialized = new SerializedObject(pickup);
        serialized.FindProperty("_body").objectReferenceValue = body;
        serialized.FindProperty("_shadow").objectReferenceValue = shadow.transform;
        SerializedProperty list = serialized.FindProperty("_qualityMarks");
        list.arraySize = marks.Length;
        for (int index = 0; index < marks.Length; index++)
        {
            list.GetArrayElementAtIndex(index).objectReferenceValue = marks[index];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<ShardPickup>();
    }

    private static Transform Crystal(Transform parent, string name, Vector3 position, Vector3 scale, Material[] materials)
    {
        GameObject root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        root.transform.localScale = scale;
        string path = Art + "FacetedCrystal.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = "Faceted octahedron" };
            Vector3[] points = { Vector3.up, Vector3.down, Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            int[] indices = { 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 2, 5, 1, 2, 3, 1, 3, 4, 1, 4, 5, 1, 5, 2 };
            List<Vector3> vertices = new List<Vector3>();
            List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>() };
            for (int index = 0; index < indices.Length; index++)
            {
                vertices.Add(points[indices[index]]);
                triangles[(index / 3) % 3].Add(index);
            }
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 3;
            for (int index = 0; index < 3; index++)
            {
                mesh.SetTriangles(triangles[index], index);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
        }
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return root.transform;
    }

    private static Material Material(string name, Color color)
    {
        string path = Art + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Unlit/Color")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static Sprite ShadowSprite()
    {
        string path = Art + "SoftShadow.asset";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Shard soft shadow";
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f) / size, Vector2.one * 0.5f) * 2;
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - distance), 2)));
            }
        }
        texture.Apply();
        AssetDatabase.CreateAsset(texture, Art + "ShadowTexture.asset");
        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        AssetDatabase.CreateAsset(sprite, path);
        return sprite;
    }
}
