using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Text))]
public class WavyUpgradeText : BaseMeshEffect
{
    [SerializeField, Min(0)] private float _amplitude = 1.5f;
    [SerializeField, Min(0)] private float _speed = 2.3f;
    [SerializeField, Min(0)] private float _phasePerGlyph = 0.55f;
    private UpgradeTextMotionScope _scope;

    public float OffsetForGlyph(int glyph, float time)
    {
        float amplitude = _scope == null ? _amplitude : _scope.Amplitude;
        float speed = _scope == null ? _speed : _scope.Speed;
        float phase = _scope == null ? _phasePerGlyph : _scope.PhasePerGlyph;
        return Mathf.Sin(time * speed + glyph * phase) * amplitude;
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive())
        {
            return;
        }
        // uGUI has already removed rich-text markup. Move each rendered glyph's whole quad,
        // preserving its UVs, vertex colors, and the shape of Cyrillic characters.
        for (int first = 0; first + 3 < vertices.currentVertCount; first += 4)
        {
            float offset = OffsetForGlyph(first / 4, Time.unscaledTime);
            for (int corner = 0; corner < 4; corner++)
            {
                UIVertex vertex = new UIVertex();
                vertices.PopulateUIVertex(ref vertex, first + corner);
                vertex.position.y += offset;
                vertices.SetUIVertex(vertex, first + corner);
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _scope = GetComponentInParent<UpgradeTextMotionScope>();
    }

    private void Update()
    {
        if (graphic != null && graphic.isActiveAndEnabled)
        {
            graphic.SetVerticesDirty();
        }
    }
}
