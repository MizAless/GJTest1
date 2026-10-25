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
        foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
    }
}
