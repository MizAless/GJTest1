using System.Collections.Generic;
using UnityEngine;

namespace GameEconomy
{
    // Native geometry: an edged gold disc, not a physics object or combat target.
    public sealed class CoinVisual : MonoBehaviour
    {
        private const float SizeGrowthPerValueDoubling = 0.15f;
        private const float MaximumSizeGrowth = 0.5f;
        private static Mesh _mesh;
        private static Material _material;
        private Transform _disc;
        private SpriteRenderer _shadow;
        private Vector3 _ground;
        private float _phase;
        private float _height;
        public int Value { get; private set; }
        public bool InFlight { get; set; }

        public void Initialize(int value, Vector3 ground, CoinDropSettings settings, float phase)
        {
            Value = value;
            _ground = ground;
            _height = settings.HoverHeight;
            _phase = phase;
            EnsureGeometry();
            GameObject disc = new GameObject("Gold coin", typeof(MeshFilter), typeof(MeshRenderer));
            _disc = disc.transform;
            _disc.SetParent(transform, false);
            // Logarithmic growth keeps high denominations readable without giant coins.
            float valueDoublings = Mathf.Log(Mathf.Max(1, value), 2);
            float sizeGrowth = Mathf.Min(valueDoublings * SizeGrowthPerValueDoubling, MaximumSizeGrowth);
            float sizeMultiplier = 1 + sizeGrowth;
            _disc.localScale = Vector3.one * settings.CoinDiameter * sizeMultiplier;
            disc.GetComponent<MeshFilter>().sharedMesh = _mesh;
            disc.GetComponent<MeshRenderer>().sharedMaterial = _material;
            _shadow = SoftGroundShadow.Create(transform, Vector3.zero, new Vector2(0.65f, 0.45f) * sizeMultiplier, 5);
            _shadow.transform.rotation = Quaternion.Euler(90, 0, 0);
            UpdatePose();
        }

        private void LateUpdate()
        {
            UpdatePose();
        }

        private void UpdatePose()
        {
            if (_disc == null)
            {
                return;
            }
            if (!InFlight)
            {
                transform.position = _ground + Vector3.up * (_height + Mathf.Sin(Time.time * 3 + _phase) * 0.07f);
            }
            _disc.rotation = Quaternion.Euler(0, Time.time * 145 + _phase * Mathf.Rad2Deg, 0);
            _shadow.transform.position = new Vector3(transform.position.x, _ground.y + 0.015f, transform.position.z);
            float altitude = transform.position.y - _ground.y;
            Color color = Color.white;
            color.a = Mathf.Clamp01(1 - altitude / 4);
            _shadow.color = color;
        }

        private static void EnsureGeometry()
        {
            if (_mesh != null)
            {
                return;
            }
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            const int segments = 16;
            Color gold = new Color(1, 0.63f, 0.06f);
            Color shine = new Color(1, 0.94f, 0.43f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int index = 0; index < segments; index++)
                {
                    float angle = index * Mathf.PI * 2 / segments;
                    float nextAngle = (index + 1) * Mathf.PI * 2 / segments;
                    Vector3 edge = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 0.5f;
                    Vector3 next = new Vector3(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle), 0) * 0.5f;
                    Vector3 depth = Vector3.forward * side * 0.055f;
                    AddTriangle(vertices, colors, triangles, depth, edge * 0.74f + depth, next * 0.74f + depth, gold);
                    AddTriangle(vertices, colors, triangles, edge * 0.74f + depth, edge + depth, next + depth, shine);
                    AddTriangle(vertices, colors, triangles, edge * 0.74f + depth, next + depth, next * 0.74f + depth, shine);
                    AddTriangle(vertices, colors, triangles, edge + depth, edge - depth, next + depth, gold * 0.7f);
                }
            }
            _mesh = new Mesh { name = "Faceted gold coin" };
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            _material = new Material(Shader.Find("Sprites/Default")) { name = "Coin vertex gold" };
        }

        private static void AddTriangle(List<Vector3> vertices, List<Color> colors, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            for (int index = 0; index < 3; index++)
            {
                colors.Add(color);
                triangles.Add(start + index);
            }
        }
    }
}
