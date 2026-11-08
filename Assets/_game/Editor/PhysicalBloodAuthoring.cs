using UnityEditor;
using UnityEngine;

public static class PhysicalBloodAuthoring
{
    private const string Folder = "Assets/_game/Prefabs/StaticShooter/";

    [MenuItem("Tools/Prototype/Update physical blood and muzzle flash")]
    public static void Build()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "BloodSquares.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Game/Blood Squares"));
            AssetDatabase.CreateAsset(material, Folder + "BloodSquares.mat");
        }

        Mesh quad = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "BloodQuad.asset");
        if (quad == null)
        {
            quad = new Mesh { name = "Blood square" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            AssetDatabase.CreateAsset(quad, Folder + "BloodQuad.asset");
        }

        ParticleSystem hit = CreateBlood("BloodHit3D", 18, 0.065f, 0.14f, 2, 3.8f, material, quad);
        ParticleSystem death = CreateBlood("BloodDeath3D", 42, 0.08f, 0.19f, 2.5f, 4.6f, material, quad);
        GameObject enemy = PrefabUtility.LoadPrefabContents("Assets/_game/Prefabs/Enemy.prefab");
        var serialized = new SerializedObject(enemy.GetComponent<Enemy>());
        serialized.FindProperty("_hitBlood3D").objectReferenceValue = hit;
        serialized.FindProperty("_deathBlood3D").objectReferenceValue = death;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(enemy, "Assets/_game/Prefabs/Enemy.prefab");
        PrefabUtility.UnloadPrefabContents(enemy);
        ConfigureMuzzle();
        AssetDatabase.SaveAssets();
    }

    private static ParticleSystem CreateBlood(string name, int count, float minSize, float maxSize,
        float minSpeed, float maxSpeed, Material material, Mesh mesh)
    {
        var root = new GameObject(name, typeof(ParticleSystem));
        ParticleSystem particles = root.GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.008f, 0.012f), new Color(0.85f, 0.025f, 0.02f));
        main.gravityModifier = 1.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);

        var emission = particles.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.075f;
        shape.radiusThickness = 1;
        var rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-3, 3);
        rotation.y = new ParticleSystem.MinMaxCurve(-3, 3);
        rotation.z = new ParticleSystem.MinMaxCurve(-3, 3);
        var collision = particles.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.dampen = 0.8f;
        collision.bounce = 0;
        collision.lifetimeLoss = 0;
        collision.radiusScale = 0.35f;
        var color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(0.6f, 0.5f, 0.5f), 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.72f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 0;
        root.AddComponent<PhysicalBlood>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<ParticleSystem>();
    }

    private static void ConfigureMuzzle()
    {
        string path = Folder + "Revolver.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        RevolverVisual weapon = root.GetComponent<RevolverVisual>();
        weapon.Muzzle.localPosition = new Vector3(0, 0, 0.58f);
        ParticleSystem flash = weapon.Muzzle.GetComponentInChildren<ParticleSystem>();
        flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        flash.transform.localPosition = new Vector3(0, 0, 0.035f);
        flash.transform.localRotation = Quaternion.identity;
        var main = flash.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = flash.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, 3) });
        var shape = flash.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8;
        shape.radius = 0.012f;
        shape.position = Vector3.zero;
        var renderer = flash.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 221;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
