using UnityEngine;

[ExecuteAlways]
public class CartridgeVisual : MonoBehaviour
{
    [Header("Sprites — size and placement come from their import settings")]
    [SerializeField] private Sprite _casingSprite;
    [SerializeField] private Sprite _tipSprite;
    [SerializeField, Min(0.01f)] private float _tipWidthScale = 1;

    [Header("Four mesh layers")]
    [SerializeField] private MeshRenderer _casing;
    [SerializeField] private MeshRenderer _tip;
    [SerializeField] private MeshRenderer _casingShadow;
    [SerializeField] private MeshRenderer _tipShadow;
    [SerializeField] private Transform _shadows;

    [Header("Contact shadow")]
    [SerializeField] private Vector3 _shadowOffset = new Vector3(0.085f, -0.065f, 0.028f);
    [SerializeField, Range(0, 1)] private float _shadowOpacity = 0.52f;

    private MaterialPropertyBlock _properties;
    private bool _refreshPending;
    private Sprite _lastCasingSprite;
    private Sprite _lastTipSprite;
    private Vector2 _lastCasingPivot;
    private Vector2 _lastTipPivot;
    private float _lastCasingPixelsPerUnit;
    private float _lastTipPixelsPerUnit;
    private float _lastWidthScale;
    private Mesh _runtimeCasingMesh;
    private Mesh _runtimeTipMesh;

#if UNITY_EDITOR
    public static event System.Action<CartridgeVisual> EditorRefreshRequested;
#endif

    public Sprite CasingSprite => _casingSprite;
    public Sprite TipSprite => _tipSprite;
    public float TipWidthScale => _tipWidthScale;
    public MeshRenderer Casing => _casing;
    public MeshRenderer Tip => _tip;
    public MeshRenderer CasingShadow => _casingShadow;
    public MeshRenderer TipShadow => _tipShadow;

    public void Configure(Sprite casingSprite, Sprite tipSprite, float tipWidthScale,
        MeshRenderer casing, MeshRenderer tip, MeshRenderer casingShadow,
        MeshRenderer tipShadow, Transform shadows)
    {
        _casingSprite = casingSprite;
        _tipSprite = tipSprite;
        _tipWidthScale = tipWidthScale;
        _casing = casing;
        _tip = tip;
        _casingShadow = casingShadow;
        _tipShadow = tipShadow;
        _shadows = shadows;
        Rebuild();
    }

    public void SetSprites(Sprite casingSprite, Sprite tipSprite, float tipWidthScale = 1)
    {
        _casingSprite = casingSprite;
        _tipSprite = tipSprite;
        _tipWidthScale = tipWidthScale;
        Rebuild();
    }

    public void SetMeshes(Mesh casingMesh, Mesh tipMesh)
    {
        SetMesh(_casing, casingMesh);
        SetMesh(_casingShadow, casingMesh);
        SetMesh(_tip, tipMesh);
        SetMesh(_tipShadow, tipMesh);
        RefreshTextures();
        RememberGeometrySettings();
    }

    public void SetShadow(Vector3 offset, float opacity)
    {
        if (_shadows != null)
        {
            _shadows.localPosition = offset;
        }

        Color tint = new Color(0, 0, 0, opacity);
        SetLayer(_casingShadow, _casingSprite, tint);
        SetLayer(_tipShadow, _tipSprite, tint);
    }

    public void RefreshTextures()
    {
        SetLayer(_casing, _casingSprite, Color.white);
        SetLayer(_tip, _tipSprite, Color.white);
        SetShadow(_shadowOffset, _shadowOpacity);
        _refreshPending = false;
    }

    [ContextMenu("Rebuild From Sprite Import Settings")]
    public void Rebuild()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            _refreshPending = true;
            EditorRefreshRequested?.Invoke(this);
            return;
        }
#endif
        if (_runtimeCasingMesh == null)
        {
            _runtimeCasingMesh = new Mesh { name = "Runtime casing", hideFlags = HideFlags.DontSave };
            _runtimeTipMesh = new Mesh { name = "Runtime tip", hideFlags = HideFlags.DontSave };
        }
        BuildSpriteMesh(_runtimeCasingMesh, _casingSprite, 1, 0);
        BuildSpriteMesh(_runtimeTipMesh, _tipSprite, _tipWidthScale, 0.004f);
        SetMeshes(_runtimeCasingMesh, _runtimeTipMesh);
    }

    public static void BuildSpriteMesh(Mesh mesh, Sprite sprite, float widthScale, float depth)
    {
        mesh.Clear();
        if (sprite == null)
        {
            return;
        }
        Vector2[] spriteVertices = sprite.vertices;
        Vector3[] vertices = new Vector3[spriteVertices.Length];
        for (int index = 0; index < vertices.Length; index++)
        {
            Vector2 vertex = spriteVertices[index];
            vertices[index] = new Vector3(vertex.x * widthScale, vertex.y, depth);
        }
        mesh.vertices = vertices;
        mesh.uv = sprite.uv;
        mesh.triangles = System.Array.ConvertAll(sprite.triangles, index => (int)index);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private bool GeometrySettingsChanged()
    {
        return _lastCasingSprite != _casingSprite || _lastTipSprite != _tipSprite
            || _lastWidthScale != _tipWidthScale
            || (_casingSprite != null && (_lastCasingPivot != _casingSprite.pivot
                || _lastCasingPixelsPerUnit != _casingSprite.pixelsPerUnit))
            || (_tipSprite != null && (_lastTipPivot != _tipSprite.pivot
                || _lastTipPixelsPerUnit != _tipSprite.pixelsPerUnit));
    }

    private void RememberGeometrySettings()
    {
        _lastCasingSprite = _casingSprite;
        _lastTipSprite = _tipSprite;
        _lastWidthScale = _tipWidthScale;
        if (_casingSprite != null)
        {
            _lastCasingPivot = _casingSprite.pivot;
            _lastCasingPixelsPerUnit = _casingSprite.pixelsPerUnit;
        }
        if (_tipSprite != null)
        {
            _lastTipPivot = _tipSprite.pivot;
            _lastTipPixelsPerUnit = _tipSprite.pixelsPerUnit;
        }
    }

    private static void SetMesh(MeshRenderer renderer, Mesh mesh)
    {
        if (renderer != null)
        {
            renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
        }
    }

    private void SetLayer(MeshRenderer renderer, Sprite sprite, Color tint)
    {
        if (renderer == null)
        {
            return;
        }

        if (_properties == null)
        {
            _properties = new MaterialPropertyBlock();
        }

        renderer.GetPropertyBlock(_properties);
        _properties.SetTexture("_MainTex", sprite == null ? Texture2D.whiteTexture : sprite.texture);
        _properties.SetColor("_Color", tint);
        _properties.SetFloat("_Rigid", 1);
        renderer.SetPropertyBlock(_properties);
    }

    private void OnEnable()
    {
        RefreshTextures();
        _refreshPending = true;
        // Editor geometry is refreshed by Update, outside asset deserialization callbacks.
        if (Application.isPlaying)
        {
            Rebuild();
        }
    }

    private void OnValidate()
    {
        _tipWidthScale = Mathf.Max(0.01f, _tipWidthScale);
        _refreshPending = true;
    }

    private void Update()
    {
        if (_refreshPending || GeometrySettingsChanged())
        {
            Rebuild();
        }
    }

    private void OnDestroy()
    {
        ReleaseRuntimeMesh(_runtimeCasingMesh);
        ReleaseRuntimeMesh(_runtimeTipMesh);
    }

    private static void ReleaseRuntimeMesh(Mesh mesh)
    {
        if (mesh == null)
        {
            return;
        }
        if (Application.isPlaying)
        {
            Destroy(mesh);
        }
        else
        {
            DestroyImmediate(mesh);
        }
    }
}
