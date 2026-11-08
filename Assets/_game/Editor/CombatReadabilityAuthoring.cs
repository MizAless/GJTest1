using System.IO;
using UnityEditor;
using UnityEngine;

public static class CombatReadabilityAuthoring
{
    private const string Folder = "Assets/_game/Prefabs/StaticShooter";
    private static Material _particles;
    private static Sprite _disc;

    [MenuItem("Tools/Prototype/Build combat presentation prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        StaticShooterArt art = AssetDatabase.LoadAssetAtPath<StaticShooterArt>(StaticShooterAuthoring.ArtAsset);
        BuildDisc();
        art.HeroPrefab = BuildHero(art);
        art.RevolverPrefab = BuildRevolver(art);
        art.ProjectilePrefab = BuildProjectile();
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
    }

    private static void BuildDisc()
    {
        string path = Folder + "/SoftDisc.png";
        if (!File.Exists(path))
        {
            Texture2D texture = new Texture2D(64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float radius = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - radius), 0.6f)));
                }
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        _disc = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        string materialPath = Folder + "/Particles.mat";
        _particles = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (_particles == null)
        {
            _particles = new Material(Shader.Find("Sprites/Default"));
            _particles.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            AssetDatabase.CreateAsset(_particles, materialPath);
        }
    }

    private static GameObject BuildHero(StaticShooterArt art)
    {
        string path = Folder + "/Gunslinger.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }
        GameObject root = new GameObject("Gunslinger", typeof(GunslingerVisual));
        SpriteRenderer body = Sprite(root.transform, "Body", art.Hero, 210);
        body.transform.localScale = new Vector3(2.27f / art.Hero.bounds.size.x, 3.4f / art.Hero.bounds.size.y, 1);
        Transform hand = Child(root.transform, "HandSocket");
        hand.localPosition = new Vector3(0.91f, 0.86f, -0.1f);
        Bind(root.GetComponent<GunslingerVisual>(), "_weaponSocket", hand);
        return Save(root, path);
    }

    private static GameObject BuildRevolver(StaticShooterArt art)
    {
        string path = Folder + "/Revolver.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }
        GameObject root = new GameObject("Revolver", typeof(RevolverVisual));
        Transform visual = Child(root.transform, "Visual recoil");
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Sprite support block";
        block.transform.SetParent(visual, false);
        block.transform.localPosition = new Vector3(0, 0, 0.28f);
        block.transform.localScale = new Vector3(0.085f, 0.1f, 0.56f);
        Object.DestroyImmediate(block.GetComponent<Collider>());
        Material steel = new Material(Shader.Find("Unlit/Color"));
        steel.color = new Color(0.24f, 0.25f, 0.27f);
        AssetDatabase.CreateAsset(steel, Folder + "/Steel.mat");
        block.GetComponent<Renderer>().sharedMaterial = steel;
        for (int side = 0; side < 2; side++)
        {
            SpriteRenderer image = Sprite(visual, side == 0 ? "Left sprite" : "Right sprite", art.Revolver, 220);
            image.transform.localPosition = new Vector3(side == 0 ? -0.045f : 0.045f, -0.16f, 0);
            image.transform.localRotation = Quaternion.Euler(0, side == 0 ? -90 : 90, 0);
            image.flipX = side != 0;
            image.transform.localScale = new Vector3(0.8f / art.Revolver.bounds.size.x, 0.53f / art.Revolver.bounds.size.y, 1);
        }
        Transform muzzle = Child(root.transform, "Muzzle");
        muzzle.localPosition = Vector3.forward * 0.65f;
        ParticleSystem flash = Particles(muzzle, "Muzzle flash", true);
        Bind(root.GetComponent<RevolverVisual>(), "_visual", visual);
        Bind(root.GetComponent<RevolverVisual>(), "_muzzle", muzzle);
        Bind(root.GetComponent<RevolverVisual>(), "_muzzleFlash", flash);
        return Save(root, path);
    }

    private static GameObject BuildProjectile()
    {
        string path = Folder + "/ProjectileVisual.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }
        GameObject root = new GameObject("Projectile visual", typeof(ProjectileVisual));
        SpriteRenderer glow = Sprite(root.transform, "Glow", _disc, 150);
        SpriteRenderer core = Sprite(root.transform, "Core", _disc, 151);
        TrailRenderer trail = Child(root.transform, "Trail").gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = _particles;
        trail.time = 0.2f;
        trail.minVertexDistance = 0.04f;
        trail.sortingOrder = 149;
        trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
        ParticleSystem sparks = Particles(root.transform, "Status sparks", false);
        ProjectileVisual view = root.GetComponent<ProjectileVisual>();
        Bind(view, "_glow", glow);
        Bind(view, "_core", core);
        Bind(view, "_trail", trail);
        Bind(view, "_sparks", sparks);
        return Save(root, path);
    }

    private static ParticleSystem Particles(Transform parent, string name, bool flash)
    {
        ParticleSystem system = Child(parent, name).gameObject.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = !flash;
        main.startLifetime = flash ? 0.09f : 0.22f;
        main.startSpeed = flash ? 0.2f : 0.4f;
        main.startSize = flash ? 0.7f : 0.16f;
        main.startColor = new Color(1, 0.85f, 0.25f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = flash ? 0 : 55;
        if (flash)
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 5) });
        }
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.04f;
        ParticleSystem.ColorOverLifetimeModule colors = system.colorOverLifetime;
        colors.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        colors.color = gradient;
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _particles;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingOrder = flash ? 225 : 150;
        return system;
    }

    private static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order)
    {
        SpriteRenderer renderer = Child(parent, name).gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static void Bind(Object owner, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(owner);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject Save(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }
}
