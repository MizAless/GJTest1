using UnityEngine;

// Camera-facing core plus a persistent swept trail keeps fast shots readable at range.
public sealed class ProjectileVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _glow;
    [SerializeField] private SpriteRenderer _core;
    [SerializeField] private TrailRenderer _trail;
    [SerializeField] private ParticleSystem _sparks;
    [SerializeField, Min(0.01f)] private float _diameter = 0.20f;
    [SerializeField, Min(1)] private float _minimumPixels = 9;
    private Color _color;
    public TrailRenderer Trail => _trail;

    public void Initialize(CombatProfile profile)
    {
        _color = profile.Color;
        _glow.color = new Color(_color.r, _color.g, _color.b, 0.85f);
        _core.color = new Color(1, 0.98f, 0.86f, 1);
        _trail.startColor = _color;
        _trail.endColor = new Color(_color.r, _color.g, _color.b, 0);
        ParticleSystem.MainModule main = _sparks.main;
        main.startColor = _color;
        _sparks.Play();
        UpdateScale();
    }

    private void LateUpdate()
    {
        UpdateScale();
    }

    private void UpdateScale()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }
        float depth = Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward);
        float worldPerPixel = 2 * Mathf.Max(0.1f, depth) * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f)
            / Mathf.Max(1, camera.pixelHeight);
        float diameter = Mathf.Max(_diameter, worldPerPixel * _minimumPixels);
        _core.transform.rotation = camera.transform.rotation;
        _glow.transform.rotation = camera.transform.rotation;
        _core.transform.localScale = Vector3.one * diameter;
        _glow.transform.localScale = Vector3.one * diameter * 2.4f;
        _trail.widthMultiplier = diameter * 0.7f;
    }

    public void Release()
    {
        // Keep the path visible briefly after impact instead of deleting it with the projectile.
        _trail.transform.SetParent(null, true);
        _trail.emitting = false;
        Destroy(_trail.gameObject, _trail.time + 0.05f);
        _sparks.transform.SetParent(null, true);
        _sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(_sparks.gameObject, 0.5f);
    }
}
