using UnityEngine;

public class Explosion : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.52f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void Visual(float radius, Color color)
    {
        var shape = GetComponent<CircleCollider2D>();
        if (shape != null && shape.radius > 0) transform.localScale *= radius / (shape.radius * Mathf.Abs(transform.lossyScale.x));
        if (StaticShooterStage.Instance != null)
        {
            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                renderer.sortingOrder = 170;
                if (renderer.renderMode == ParticleSystemRenderMode.Billboard)
                {
                    renderer.alignment = ParticleSystemRenderSpace.View;
                }
            }
            transform.rotation = Camera.main.transform.rotation;
            // Lift the sphere's visual center above the floor without changing its damage footprint.
            transform.position += Vector3.up * (radius * 0.35f);
        }
        foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
    }
}
