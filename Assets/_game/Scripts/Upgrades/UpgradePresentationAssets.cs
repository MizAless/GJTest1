using UnityEngine;

public class UpgradePresentationAssets : ScriptableObject
{
    public GameObject BandolierPrefab;
    public CartridgeVisual CartridgePrefab;
    public Sprite[] TipSprites;
    public float[] TipWidths;
    public Mesh ClipMesh;
    public Material ClipMaterial;

    public void ApplyAmmo(CartridgeVisual visual, AmmoItem item)
    {
        int index = item.Archetype == null ? 0 : item.Archetype.VisualIndex;
        visual.SetSprites(CartridgePrefab.CasingSprite, TipSprites[index], TipWidths[index]);
    }

    public Mesh CreateModuleSurfaceMesh()
    {
        const int segments = 32;
        Vector3[] corners = ClipMesh.vertices;
        Vector2[] sourceUv = ClipMesh.uv;
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];
        for (int index = 0; index <= segments; index++)
        {
            float progress = (float)index / segments;
            int vertex = index * 2;
            vertices[vertex] = Vector3.Lerp(corners[0], corners[2], progress);
            vertices[vertex + 1] = Vector3.Lerp(corners[1], corners[3], progress);
            uv[vertex] = Vector2.Lerp(sourceUv[0], sourceUv[2], progress);
            uv[vertex + 1] = Vector2.Lerp(sourceUv[1], sourceUv[3], progress);
            if (index < segments)
            {
                int triangle = index * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }
        }
        Mesh mesh = new Mesh { name = "Module surface attachment", hideFlags = HideFlags.DontSave };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        // Shader deformation adds the slot anchor and moves the strip around the roll.
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(17, 6, 6));
        return mesh;
    }

    public MeshRenderer CreateClip(Transform parent, string name, Mesh mesh = null)
    {
        GameObject root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.SetParent(parent, false);
        root.GetComponent<MeshFilter>().sharedMesh = mesh == null ? ClipMesh : mesh;
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = ClipMaterial;
        return renderer;
    }
}
