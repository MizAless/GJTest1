using System;
using System.Collections.Generic;
using UnityEngine;

// Shop thumbnails use the same four-mesh prefab and sprite mapping as the bandolier.
internal sealed class CartridgeIconCache : IDisposable
{
    private readonly Dictionary<Guid, RenderTexture> _textures = new Dictionary<Guid, RenderTexture>();

    public RenderTexture Get(Guid id, AmmoItem item)
    {
        if (_textures.TryGetValue(id, out RenderTexture existing))
        {
            return existing;
        }
        UpgradePresentationAssets assets = Resources.Load<UpgradePresentationAssets>("Upgrades/PresentationAssets");
        GameObject root = new GameObject("Shop thumbnail studio");
        root.transform.position = new Vector3(-2000, 2000, 0);
        CartridgeVisual visual = UnityEngine.Object.Instantiate(assets.CartridgePrefab, root.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        assets.ApplyAmmo(visual, item);
        visual.SetPreviewSpin(0, 0);
        foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = 30;
        }
        Bounds bounds = visual.Casing.bounds;
        foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>())
        {
            bounds.Encapsulate(renderer.bounds);
        }
        Camera camera = root.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(0.1f, bounds.size.y * 0.57f, bounds.size.x * 0.76f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << 30;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 30;
        // The camera shares the root but the visual remains at the original world position.
        visual.transform.SetParent(null, true);
        root.transform.position = bounds.center + Vector3.back * 10;
        var texture = new RenderTexture(96, 128, 24, RenderTextureFormat.ARGB32);
        texture.name = "Shop cartridge icon";
        camera.targetTexture = texture;
        camera.Render();
        camera.targetTexture = null;
        visual.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(visual.gameObject);
        UnityEngine.Object.Destroy(root);
        _textures.Add(id, texture);
        return texture;
    }

    public void Dispose()
    {
        foreach (RenderTexture texture in _textures.Values)
        {
            texture.Release();
            UnityEngine.Object.Destroy(texture);
        }
        _textures.Clear();
    }
}
