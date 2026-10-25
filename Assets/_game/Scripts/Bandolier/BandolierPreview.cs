using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class BandolierPreview : MonoBehaviour
{
    private const float OpeningEndTime = 0.4f;
    private const float SettledTime = 0.9999f;
    private const float FoldDurationMultiplier = 0.65f;
    private const float CenterSlotIndex = 2.5f;
    private const float CartridgeRevealMargin = 0.44f;

    [Header("Layers")]
    [SerializeField] private Transform _presentation;
    [SerializeField] private MeshRenderer _base;
    [SerializeField] private MeshRenderer _shadow;
    [SerializeField] private MeshRenderer[] _loops;
    [SerializeField] private Transform[] _cartridges;
    [SerializeField] private MeshRenderer[] _cartridgeShadows;

    [Header("Throw and leather impacts")]
    [SerializeField, Range(0, 1)] private float _timeline = 1;
    [SerializeField, Range(0, 1)] private float _progress = 1;
    [SerializeField, Min(0.3f)] private float _duration = 1.35f;
    [SerializeField, Range(0, 2)] private float _impactStrength = 1;
    [SerializeField, Range(0.35f, 1.3f)] private float _radius = 0.78f;
    [SerializeField, Range(0.001f, 0.015f)] private float _thickness = 0.006f;
    [SerializeField] private float _width = 12.6f;
    [SerializeField] private float _spacing = 1.7f;
    [SerializeField] private bool _autoPlay = true;

    [Header("Cartridge contact shadows")]
    [SerializeField] private Vector3 _cartridgeShadowOffset = new Vector3(0.085f, -0.065f, 0.028f);
    [SerializeField, Range(0, 1)] private float _cartridgeShadowOpacity = 0.52f;

    [Header("Controls")]
    [SerializeField] private Slider _slider;
    [SerializeField] private Text _status;

    private MaterialPropertyBlock _layerProperties;
    private float _playbackTime;
    private float _startTimeline;
    private float _targetTimeline = 1;
    private bool _isPlaying;
    private MeshRenderer[][] _cartridgeLayers;
    private MeshRenderer[][] _shadowLayers;

    public float Progress => _progress;
    public float Timeline => _timeline;
    public int CartridgeCount => _cartridges == null ? 0 : _cartridges.Length;
    public int CartridgeShadowCount => _cartridgeShadows == null ? 0 : _cartridgeShadows.Length;

    public void Configure(
        Transform presentation,
        MeshRenderer leather,
        MeshRenderer shadow,
        MeshRenderer[] loops,
        Transform[] cartridges,
        MeshRenderer[] cartridgeShadows,
        Slider slider,
        Text status)
    {
        _presentation = presentation;
        _base = leather;
        _shadow = shadow;
        _loops = loops;
        _cartridges = cartridges;
        _cartridgeShadows = cartridgeShadows;
        _slider = slider;
        _status = status;
        RefreshCartridgeLayers();
        Apply();
    }

    private void OnEnable()
    {
        RefreshCartridgeLayers();
        if (Application.isPlaying && _autoPlay)
        {
            StartReplay(delay: 0.18f);
        }

        Apply();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Replay();
            }

            UpdatePlayback();
        }

        Apply();
    }

    public void Replay()
    {
        StartReplay(delay: 0.06f);
        Apply();
    }

    public void Open()
    {
        AnimateTo(1);
    }

    public void Fold()
    {
        AnimateTo(0);
    }

    public void Scrub(float value)
    {
        _isPlaying = false;
        _timeline = Mathf.Clamp01(value);
        Apply();
    }

    public void SetDuration(float value)
    {
        _duration = Mathf.Clamp(value, 0.3f, 6);
    }

    private void StartReplay(float delay)
    {
        _timeline = 0;
        _startTimeline = 0;
        _targetTimeline = 1;
        _playbackTime = -delay;
        _isPlaying = true;
    }

    private void AnimateTo(float target)
    {
        // Settling follows the opening; starting from its end skips an idle tail.
        _startTimeline = Mathf.Min(_timeline, OpeningEndTime);
        _targetTimeline = target;
        _playbackTime = 0;
        _isPlaying = true;
    }

    private void UpdatePlayback()
    {
        if (!_isPlaying)
        {
            return;
        }

        _playbackTime += Time.unscaledDeltaTime;
        bool isFolding = _targetTimeline < _startTimeline;
        float duration = _duration * (isFolding ? FoldDurationMultiplier : 1);
        float playbackProgress = Mathf.Clamp01(_playbackTime / duration);

        _timeline = Mathf.Lerp(_startTimeline, _targetTimeline, playbackProgress);
        if (playbackProgress >= 1)
        {
            _isPlaying = false;
        }
    }

    // A short tug, a fast throw, two catches under tension, then the final snap.
    // The remaining timeline is reserved for impacts and settling.
    public static float Opening(float timeline)
    {
        if (timeline < 0.065f)
        {
            return 0.012f * Mathf.Pow(timeline / 0.065f, 2);
        }

        if (timeline < 0.20f)
        {
            return Mathf.Lerp(0.012f, 0.68f, (timeline - 0.065f) / 0.135f);
        }

        if (timeline < 0.235f)
        {
            return Mathf.Lerp(0.68f, 0.715f, (timeline - 0.20f) / 0.035f);
        }

        if (timeline < 0.315f)
        {
            return Mathf.Lerp(0.715f, 0.945f, (timeline - 0.235f) / 0.08f);
        }

        if (timeline < 0.345f)
        {
            return Mathf.Lerp(0.945f, 0.958f, (timeline - 0.315f) / 0.03f);
        }

        if (timeline < OpeningEndTime)
        {
            return Mathf.Lerp(0.958f, 1, (timeline - 0.345f) / 0.055f);
        }

        return 1;
    }

    private float GetSlotX(int index)
    {
        return (index - CenterSlotIndex) * _spacing;
    }

    private float GetNormalizedPosition(float x)
    {
        return Mathf.Clamp01((x + _width * 0.5f) / _width);
    }

    private float GetImpactAge(float normalizedPosition)
    {
        float arrivalTime = 0.09f + 0.30f * Mathf.Pow(normalizedPosition, 1.2f);
        return _timeline - arrivalTime;
    }

    // Keep surface/impact formulas in sync with BandolierCurl.shader.
    // Positive Z points into the table.
    private Vector3 EvaluateLeatherImpact(float x)
    {
        float normalizedPosition = GetNormalizedPosition(x);
        float impactAge = GetImpactAge(normalizedPosition);
        if (impactAge <= 0 || _timeline >= SettledTime)
        {
            return Vector3.zero;
        }

        float damping = Mathf.Exp(-impactAge * 11);
        return new Vector3(
            Mathf.Sin(impactAge * 57) * damping * 0.065f,
            Mathf.Sin(impactAge * 39 + normalizedPosition * 2) * damping * 0.075f,
            -Mathf.Abs(Mathf.Sin(impactAge * 48)) * damping * (0.20f + 0.30f * normalizedPosition)
        ) * _impactStrength;
    }

    private static float GetReleasedWeight(float distanceFromFront)
    {
        float transition = Mathf.Clamp01((distanceFromFront + 0.25f) / 0.4f);
        return 1 - transition * transition * (3 - 2 * transition);
    }

    public Vector3 Surface(float x, float offset, out float angle)
    {
        float rollFront = _progress * _width;
        float distanceFromFront = x + _width * 0.5f - rollFront;
        angle = 0;
        Vector3 point = new Vector3(x, 0, offset);

        if (distanceFromFront > 0)
        {
            float radius = Mathf.Min(_radius, Mathf.Max(0.04f, (_width - rollFront) / 4.2f));
            float spiralThickness = Mathf.Max(0.00001f, Mathf.Min(_thickness, radius * 0.005f));
            float discriminant = Mathf.Max(
                0.000001f,
                radius * radius - 2 * spiralThickness * distanceFromFront);

            angle = (radius - Mathf.Sqrt(discriminant)) / spiralThickness;
            float localRadius = radius - spiralThickness * angle;
            point = new Vector3(
                -_width * 0.5f + rollFront + localRadius * Mathf.Sin(angle) + offset * Mathf.Sin(angle),
                0,
                -radius + localRadius * Mathf.Cos(angle) + offset * Mathf.Cos(angle));
        }

        return point + EvaluateLeatherImpact(x) * GetReleasedWeight(distanceFromFront);
    }

    private void ApplyLayerProperties(
        MeshRenderer renderer,
        float anchorX = 0,
        bool isGroundShadow = false,
        bool isRigid = false,
        Color? tint = null)
    {
        if (renderer == null)
        {
            return;
        }

        if (_layerProperties == null)
        {
            _layerProperties = new MaterialPropertyBlock();
        }

        _layerProperties.Clear();
        _layerProperties.SetFloat("_Progress", _progress);
        _layerProperties.SetFloat("_Width", _width);
        _layerProperties.SetFloat("_Radius", _radius);
        _layerProperties.SetFloat("_Thickness", _thickness);
        _layerProperties.SetFloat("_AnchorX", anchorX);
        _layerProperties.SetFloat("_Shadow", isGroundShadow ? 1 : 0);
        _layerProperties.SetFloat("_BendAngle", 0);
        _layerProperties.SetFloat("_Rigid", isRigid ? 1 : 0);
        _layerProperties.SetFloat("_Gesture", _timeline);
        _layerProperties.SetFloat("_ImpactStrength", _impactStrength);

        if (tint.HasValue)
        {
            _layerProperties.SetColor("_Color", tint.Value);
        }

        renderer.SetPropertyBlock(_layerProperties);
    }

    private void Apply()
    {
        _progress = Opening(_timeline);
        UpdatePresentation();
        UpdateLeatherLayers();
        UpdateCartridges();
        UpdateControls();
    }

    private void UpdatePresentation()
    {
        if (_presentation == null)
        {
            return;
        }

        float envelope = 0;
        if (_timeline < SettledTime)
        {
            envelope = Mathf.Clamp01(_timeline / 0.065f)
                * Mathf.Exp(-Mathf.Max(0, _timeline - 0.12f) * 9);
        }

        _presentation.localPosition = new Vector3(
            Mathf.Sin(_timeline * 19) * 0.24f,
            Mathf.Sin(_timeline * 29) * 0.065f,
            0) * envelope * _impactStrength;
        _presentation.localRotation = Quaternion.Euler(
            0, 0, Mathf.Sin(_timeline * 23 - 0.7f) * 3 * envelope * _impactStrength);
    }

    private void UpdateLeatherLayers()
    {
        ApplyLayerProperties(_base);
        ApplyLayerProperties(_shadow, isGroundShadow: true);

        if (_loops == null)
        {
            return;
        }

        for (int index = 0; index < _loops.Length; index++)
        {
            ApplyLayerProperties(_loops[index], anchorX: GetSlotX(index));
        }
    }

    private void UpdateCartridges()
    {
        if (_cartridges == null)
        {
            return;
        }

        for (int index = 0; index < _cartridges.Length; index++)
        {
            Transform cartridge = _cartridges[index];
            if (cartridge == null)
            {
                continue;
            }

            UpdateCartridgePose(cartridge, index);
            float slotX = GetSlotX(index);
            bool isVisible = _progress * _width >= slotX + _width * 0.5f + CartridgeRevealMargin;
            foreach (MeshRenderer renderer in _cartridgeLayers[index])
            {
                renderer.enabled = isVisible;
                ApplyLayerProperties(renderer, isRigid: true);
            }
            UpdateCartridgeShadow(index, isVisible);
        }
    }

    private void UpdateCartridgePose(Transform cartridge, int index)
    {
        float slotX = GetSlotX(index);
        float impactAge = GetImpactAge(GetNormalizedPosition(slotX));
        float kick = 0;
        if (impactAge > 0 && _timeline < SettledTime)
        {
            kick = Mathf.Exp(-impactAge * 13) * _impactStrength;
        }

        Vector3 leatherImpact = EvaluateLeatherImpact(slotX);
        float bounce = Mathf.Abs(Mathf.Sin(impactAge * 62)) * kick * 0.035f;

        // Metal stays rigid: reveal only after the roll clears the whole cartridge.
        cartridge.localPosition = new Vector3(
            slotX + leatherImpact.x,
            0.08f + leatherImpact.y,
            -0.065f + leatherImpact.z - bounce);
        cartridge.localRotation = Quaternion.Euler(
            Mathf.Sin(impactAge * 51) * kick * 2,
            Mathf.Sin(impactAge * 48) * kick * 3,
            Mathf.Sin(impactAge * 60 + index * 0.61f) * kick * 3.2f);
        cartridge.localScale = Vector3.one;
    }

    private void UpdateCartridgeShadow(int index, bool isVisible)
    {
        if (_cartridgeShadows == null || index >= _cartridgeShadows.Length)
        {
            return;
        }

        MeshRenderer shadow = _cartridgeShadows[index];
        if (shadow == null)
        {
            return;
        }

        shadow.transform.localPosition = _cartridgeShadowOffset;
        shadow.transform.localRotation = Quaternion.identity;
        shadow.transform.localScale = Vector3.one;
        foreach (MeshRenderer layer in _shadowLayers[index])
        {
            layer.enabled = isVisible;
            ApplyLayerProperties(
                layer,
                isRigid: true,
                tint: new Color(0, 0, 0, _cartridgeShadowOpacity));
        }
    }

    // Cache once when assembling the hierarchy; playback must not allocate arrays each frame.
    public void RefreshCartridgeLayers()
    {
        int count = CartridgeCount;
        _cartridgeLayers = new MeshRenderer[count][];
        _shadowLayers = new MeshRenderer[count][];
        for (int index = 0; index < count; index++)
        {
            MeshRenderer shadow = null;
            if (_cartridgeShadows != null && index < _cartridgeShadows.Length)
            {
                shadow = _cartridgeShadows[index];
            }

            var visibleLayers = new System.Collections.Generic.List<MeshRenderer>();
            if (_cartridges[index] != null)
            {
                foreach (MeshRenderer layer in _cartridges[index].GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (shadow == null || !layer.transform.IsChildOf(shadow.transform))
                    {
                        visibleLayers.Add(layer);
                    }
                }
            }

            _cartridgeLayers[index] = visibleLayers.ToArray();
            _shadowLayers[index] = shadow == null
                ? new MeshRenderer[0]
                : shadow.GetComponentsInChildren<MeshRenderer>(true);
        }
    }

    private void UpdateControls()
    {
        if (_slider != null)
        {
            _slider.SetValueWithoutNotify(_timeline);
        }

        if (_status != null)
        {
            _status.text = GetStatusText();
        }
    }

    private string GetStatusText()
    {
        if (_isPlaying)
        {
            if (_targetTimeline < _startTimeline)
            {
                return "Сворачивается…";
            }

            if (_timeline < OpeningEndTime)
            {
                return "Резкий бросок";
            }

            if (_timeline < 0.8f)
            {
                return "Кожа оседает…";
            }

            return "Готово";
        }

        if (_timeline > 0.995f)
        {
            return "Патронташ раскрыт";
        }

        if (_timeline < 0.005f)
        {
            return "Патронташ свёрнут";
        }

        return "Пауза · " + Mathf.RoundToInt(_timeline * 100) + "%";
    }

    private void OnValidate()
    {
        _width = Mathf.Max(1, _width);
        _radius = Mathf.Max(0.1f, _radius);
    }
}
