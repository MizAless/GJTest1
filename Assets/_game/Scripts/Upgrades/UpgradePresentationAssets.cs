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
        int index = 0;
        AmmoProperties properties = item.Properties;
        if (properties.Puncture > 0)
        {
            index = 1;
        }
        else if (properties.Burn > 0)
        {
            index = 2;
        }
        else if (properties.Chill > 0)
        {
            index = 3;
        }
        else if (properties.Ricochet > 0)
        {
            index = 4;
        }
        else if (properties.Fan > 0)
        {
            index = 5;
        }
        else if (properties.Burst > 0)
        {
            index = 6;
        }
        visual.SetSprites(CartridgePrefab.CasingSprite, TipSprites[index], TipWidths[index]);
    }

    public MeshRenderer CreateClip(Transform parent, string name)
    {
        GameObject root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.SetParent(parent, false);
        root.GetComponent<MeshFilter>().sharedMesh = ClipMesh;
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = ClipMaterial;
        return renderer;
    }
}
