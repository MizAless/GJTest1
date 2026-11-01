using UnityEngine;
using UnityEngine.UI;

public class UpgradePresentation : MonoBehaviour
{
    private const int PresentationLayer = 30;
    private readonly CartridgeVisual[] _offers = new CartridgeVisual[3];
    private readonly MeshRenderer[] _offerClips = new MeshRenderer[3];
    private readonly MeshRenderer[] _slotClips = new MeshRenderer[6];
    private readonly float[] _spinAmounts = new float[3];
    private readonly float[] _liftAmounts = new float[6];
    private CartridgeVisual[] _cartridges;
    private UpgradePresentationAssets _assets;
    private BandolierPreview _bandolier;
    private Camera _camera;
    private RawImage _image;
    private RenderTexture _texture;
    private RunDirector _run;
    private UpgradeSelection _selection;
    private int _textureWidth;
    private int _textureHeight;
    private MaterialPropertyBlock _clipProperties;
    private Mesh _moduleSurfaceMesh;

    public BandolierPreview Bandolier => _bandolier;
    public Camera Camera => _camera;

    public void Initialize(RunDirector run, UpgradeSelection selection, RawImage image)
    {
        _run = run;
        _clipProperties = new MaterialPropertyBlock();
        _selection = selection;
        _image = image;
        _assets = Resources.Load<UpgradePresentationAssets>("Upgrades/PresentationAssets");
        if (_assets == null)
        {
            throw new System.InvalidOperationException("Upgrade presentation assets have not been authored.");
        }
        transform.position = new Vector3(1000, 1000, 0);
        GameObject cameraRoot = new GameObject("Upgrade camera", typeof(Camera));
        cameraRoot.transform.SetParent(transform, false);
        cameraRoot.transform.localPosition = new Vector3(0, 0, -20);
        _camera = cameraRoot.GetComponent<Camera>();
        _camera.orthographic = true;
        _camera.orthographicSize = 3.6f;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.clear;
        _camera.cullingMask = 1 << PresentationLayer;
        _camera.nearClipPlane = 0.1f;
        _camera.farClipPlane = 50;
        GameObject bandolier = Instantiate(_assets.BandolierPrefab, transform);
        _bandolier = bandolier.GetComponent<BandolierPreview>();
        _moduleSurfaceMesh = _assets.CreateModuleSurfaceMesh();
        _cartridges = new CartridgeVisual[6];
        for (int index = 0; index < _cartridges.Length; index++)
        {
            _cartridges[index] = _bandolier.GetCartridge(index).GetComponent<CartridgeVisual>();
            _slotClips[index] = _assets.CreateClip(_cartridges[index].transform.parent, "Module clip " + index, _moduleSurfaceMesh);
            _bandolier.UpdateModuleAttachment(_slotClips[index], index);
        }
        for (int index = 0; index < 3; index++)
        {
            _offers[index] = Instantiate(_assets.CartridgePrefab, transform);
            _offers[index].name = "Offer visual " + index;
            _offerClips[index] = _assets.CreateClip(transform, "Offer module " + index);
            _offers[index].transform.localScale = Vector3.one * 0.42f;
        }
        ApplyLayer(transform);
        Resize();
    }

    public void RefreshItems()
    {
        for (int index = 0; index < 6; index++)
        {
            _assets.ApplyAmmo(_cartridges[index], _run.Drum.Cartridges[index]);
            _slotClips[index].enabled = _run.Drum.Sockets[index] != null;
        }
        for (int index = 0; index < 3; index++)
        {
            bool module = _run.ChoosingModule;
            _offers[index].gameObject.SetActive(!module);
            _offerClips[index].gameObject.SetActive(module);
            if (!module)
            {
                _assets.ApplyAmmo(_offers[index], _run.Offers[index]);
            }
        }
    }

    public void Open(bool replay)
    {
        gameObject.SetActive(true);
        RefreshItems();
        if (replay)
        {
            _bandolier.Scrub(0);
            _bandolier.Replay();
        }
    }

    public Vector2 SlotViewportPosition(int index)
    {
        Vector3 position = _cartridges[index].transform.position;
        return _camera.WorldToViewportPoint(position);
    }

    public void RenderNow(int width = 0, int height = 0)
    {
        Resize(width, height);
        UpdateModuleAttachments();
        _camera.Render();
    }

    private void Update()
    {
        Resize();
        float blend = 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime);
        for (int index = 0; index < 3; index++)
        {
            float target = _selection.Offer == index ? 1 : 0;
            _spinAmounts[index] = Mathf.Lerp(_spinAmounts[index], target, blend);
            float amount = _spinAmounts[index];
            float time = Time.unscaledTime;
            Quaternion rotation = Quaternion.Euler(Mathf.Sin(time * 1.7f) * 9 * amount,
                0, Mathf.Sin(time * 2.1f) * 5 * amount);
            _offers[index].transform.localRotation = rotation;
            _offerClips[index].transform.localRotation = rotation;
            _offers[index].SetPreviewSpin(time * 0.35f, amount);
            _offerClips[index].GetPropertyBlock(_clipProperties);
            _clipProperties.SetFloat("_SpinPhase", time * 0.35f);
            _clipProperties.SetFloat("_SpinAmount", amount);
            _offerClips[index].SetPropertyBlock(_clipProperties);
        }
        for (int index = 0; index < 6; index++)
        {
            float target = _selection.Slot == index && _selection.Offer < 0 ? 1 : 0;
            _liftAmounts[index] = Mathf.Lerp(_liftAmounts[index], target, blend);
            _bandolier.SetSlotLift(index, _liftAmounts[index]);
        }
    }

    private void LateUpdate()
    {
        UpdateModuleAttachments();
    }

    private void UpdateModuleAttachments()
    {
        for (int index = 0; index < _slotClips.Length; index++)
        {
            _slotClips[index].enabled = _run.Drum.Sockets[index] != null;
            _bandolier.UpdateModuleAttachment(_slotClips[index], index);
        }
    }

    private void Resize(int requestedWidth = 0, int requestedHeight = 0)
    {
        // The image fills the root canvas. Its own stretch rect can be stale during a stage switch.
        Rect imageRect = ((RectTransform)_image.canvas.rootCanvas.transform).rect;
        float scale = _image.canvas.scaleFactor;
        int width = Mathf.Max(1, Mathf.RoundToInt(imageRect.width * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(imageRect.height * scale));
        if (requestedWidth > 0 && requestedHeight > 0)
        {
            width = requestedWidth;
            height = requestedHeight;
        }
        if (_texture == null || width != _textureWidth || height != _textureHeight)
        {
            ReleaseTexture();
            _textureWidth = width;
            _textureHeight = height;
            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            _texture.name = "Upgrade presentation";
            _camera.targetTexture = _texture;
            _image.texture = _texture;
        }
        _camera.aspect = (float)width / height;
        float worldWidth = _camera.orthographicSize * 2 * _camera.aspect;
        float halfHeight = _camera.orthographicSize;
        _camera.projectionMatrix = Matrix4x4.Ortho(-worldWidth / 2, worldWidth / 2,
            -halfHeight, halfHeight, _camera.nearClipPlane, _camera.farClipPlane);
        _bandolier.transform.localScale = Vector3.one * (worldWidth * 0.58f / 12.6f);
        _bandolier.transform.localPosition = new Vector3(worldWidth * 0.205f, 0, 0);
        for (int index = 0; index < 3; index++)
        {
            Vector3 position = new Vector3(-worldWidth * 0.405f, 1.55f - index * 1.6f, 0);
            _offers[index].transform.localPosition = position;
            _offerClips[index].transform.localPosition = position;
            _offerClips[index].transform.localScale = Vector3.one * 1.2f;
        }
    }

    private static void ApplyLayer(Transform root)
    {
        root.gameObject.layer = PresentationLayer;
        foreach (Transform child in root)
        {
            ApplyLayer(child);
        }
    }

    private void ReleaseTexture()
    {
        if (_texture == null)
        {
            return;
        }
        if (_camera != null)
        {
            _camera.targetTexture = null;
        }
        if (_image != null)
        {
            _image.texture = null;
        }
        _texture.Release();
        Destroy(_texture);
        _texture = null;
    }

    private void OnDestroy()
    {
        ReleaseTexture();
        if (_moduleSurfaceMesh != null)
        {
            Destroy(_moduleSurfaceMesh);
        }
    }
}
