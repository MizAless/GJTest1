using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CartridgeVisual))]
public class CartridgeVisualEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CartridgeVisual visual = (CartridgeVisual)target;
        Sprite previousTip = visual.TipSprite;
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();
        if (GUILayout.Button("Rebuild From Sprite Import Settings") || changed)
        {
            if (visual.TipSprite != previousTip)
            {
                visual.SetSprites(visual.CasingSprite, visual.TipSprite,
                    BandolierAmmoArt.GetSuggestedWidthScale(visual.TipSprite));
            }
            else
            {
                visual.Rebuild();
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        }
        EditorGUILayout.HelpBox("Sprite Pivot and Pixels Per Unit control placement and size. "
            + "Tip Width Scale preserves non-uniform silhouettes. Both shadows reuse the visible meshes.", MessageType.Info);
    }
}

[InitializeOnLoad]
public static class CartridgeVisualMeshes
{
    public const string MeshFolder = "Assets/_game/Art/Bandolier/ModularAmmo/VisualMeshes";

    static CartridgeVisualMeshes()
    {
        CartridgeVisual.EditorRefreshRequested += Rebuild;
    }

    public static void Rebuild(CartridgeVisual visual)
    {
        if (visual.Casing == null || visual.Tip == null)
        {
            return;
        }
        if (!AssetDatabase.IsValidFolder(MeshFolder))
        {
            AssetDatabase.CreateFolder("Assets/_game/Art/Bandolier/ModularAmmo", "VisualMeshes");
        }
        Mesh casing = GetMesh(visual.CasingSprite, 1, 0);
        Mesh tip = GetMesh(visual.TipSprite, visual.TipWidthScale, 0.004f);
        visual.SetMeshes(casing, tip);
    }

    private static Mesh GetMesh(Sprite sprite, float widthScale, float depth)
    {
        if (sprite == null)
        {
            return null;
        }
        string spritePath = AssetDatabase.GetAssetPath(sprite);
        string guid = AssetDatabase.AssetPathToGUID(spritePath);
        string width = widthScale.ToString("R", CultureInfo.InvariantCulture).Replace('.', '_');
        string path = MeshFolder + "/" + sprite.name + "_" + guid.Substring(0, 8) + "_" + width + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = sprite.name + " sprite mesh" };
            CartridgeVisual.BuildSpriteMesh(mesh, sprite, widthScale, depth);
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            CartridgeVisual.BuildSpriteMesh(mesh, sprite, widthScale, depth);
            EditorUtility.SetDirty(mesh);
        }
        return mesh;
    }
}

public class CartridgeSpriteReimport : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        bool spriteChanged = Array.Exists(imported, path =>
            AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D));
        if (spriteChanged)
        {
            EditorApplication.delayCall += RefreshSceneVisuals;
        }
    }

    private static void RefreshSceneVisuals()
    {
        if (Application.isPlaying)
        {
            return;
        }
        foreach (CartridgeVisual visual in Resources.FindObjectsOfTypeAll<CartridgeVisual>())
        {
            if (!EditorUtility.IsPersistent(visual))
            {
                visual.Rebuild();
            }
        }
    }
}
