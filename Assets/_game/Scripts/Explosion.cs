using UnityEngine;

public class Explosion : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.52f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<Enemy>(out var enemy))
            enemy.TakeDamage();
    }
}
