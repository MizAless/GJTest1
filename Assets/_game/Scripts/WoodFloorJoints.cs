using System.Collections.Generic;
using UnityEngine;

// Draw the short end joints separately so the original wood stays continuous.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class WoodFloorJoints : MonoBehaviour
{
    [SerializeField] private float _boardLength = 3f;
    [SerializeField] private float _rowShift = 1f;
    [SerializeField] private Color _jointColor = new Color32(180, 92, 36, 255);
    private Mesh _mesh;

    private void OnEnable() => Rebuild();
    private void OnValidate()
    {
        _boardLength = Mathf.Max(1f, _boardLength);
        _rowShift = Mathf.Clamp(_rowShift, 0f, _boardLength);
        Rebuild();
    }

    private void Rebuild()
    {
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "Staggered board joints", hideFlags = HideFlags.HideAndDontSave };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var colors = new List<Color>();
        var uvs = new List<Vector2>();
        Color vertexColor = QualitySettings.activeColorSpace == ColorSpace.Linear
            ? _jointColor.linear : _jointColor;
        // Original 16px tile has horizontal joints at pixels 0, 5, and 11.
        int[] starts = { 1, 6, 12 };
        int[] heights = { 4, 5, 4 };
        for (int tileY = -9; tileY <= 9; tileY++)
        {
            for (int strip = 0; strip < 3; strip++)
            {
                int row = (tileY + 9) * 3 + strip;
                float shift = Mathf.Repeat(row * _rowShift, _boardLength);
                float bottom = tileY - 0.5f + starts[strip] / 16f;
                float top = bottom + heights[strip] / 16f;
                for (float x = -15.5f + shift; x < 15.5f; x += _boardLength)
                {
                    if (x <= -15.5f) continue;
                    int first = vertices.Count;
                    float right = x + 1f / 16f;
                    vertices.Add(new Vector3(x, bottom, 0));
                    vertices.Add(new Vector3(x, top, 0));
                    vertices.Add(new Vector3(right, top, 0));
                    vertices.Add(new Vector3(right, bottom, 0));
                    triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                    for (int i = 0; i < 4; i++)
                    {
                        colors.Add(vertexColor);
                        uvs.Add(Vector2.zero);
                    }
                }
            }
        }
        _mesh.Clear();
        _mesh.SetVertices(vertices);
        _mesh.SetTriangles(triangles, 0);
        _mesh.SetColors(colors);
        _mesh.SetUVs(0, uvs);
        _mesh.RecalculateBounds();
    }

    private void OnDisable()
    {
        if (_mesh == null) return;
        if (Application.isPlaying) Destroy(_mesh);
        else DestroyImmediate(_mesh);
        _mesh = null;
    }
}
