using UnityEngine;

public sealed class ApproachingEnemyVisual : MonoBehaviour
{
    private Enemy _enemy;
    private SpriteRenderer _renderer;
    private Transform _health;
    private float _phase;
    private SpriteRenderer _shadow;

    private void Start()
    {
        _enemy = GetComponent<Enemy>();
        PrototypeSpriteAnimation oldAnimation = GetComponent<PrototypeSpriteAnimation>();
        if (oldAnimation != null)
        {
            oldAnimation.enabled = false;
        }
        _renderer = GetComponent<SpriteRenderer>();
        _renderer.sprite = StaticShooterStage.Instance.EnemySprite;
        _phase = Random.value * 6;
        foreach (Collider2D collider in GetComponents<Collider2D>())
        {
            collider.enabled = false;
        }
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.simulated = false;
        Vector3 size = _renderer.sprite.bounds.size;
        GameObject hitboxRoot = new GameObject("3D hitbox");
        hitboxRoot.transform.SetParent(transform, false);
        BoxCollider hitbox = hitboxRoot.AddComponent<BoxCollider>();
        hitbox.size = new Vector3(size.x * 0.52f, size.y * 0.85f, 0.75f);
        hitbox.center = new Vector3(0, size.y * 0.45f, 0);
        hitbox.isTrigger = true;
        _shadow = SoftGroundShadow.Create(transform, Vector3.up * 0.025f, new Vector2(3, 1.7f), -250);
        _shadow.transform.rotation = Quaternion.Euler(90, 0, 0);
        _health = transform.Find("Health background");
    }

    private void LateUpdate()
    {
        // Perspective supplies depth scaling. Only the enemy type changes world size.
        _renderer.sortingOrder = 20 + Mathf.RoundToInt(60 - transform.position.z);
        if (_health != null)
        {
            _health.localPosition = Vector3.up * (_renderer.sprite.bounds.size.y + 0.2f);
            _health.localScale = new Vector3(1.4f, 0.055f, 1);
            foreach (SpriteRenderer bar in _health.GetComponentsInChildren<SpriteRenderer>())
            {
                bar.sortingOrder = _renderer.sortingOrder + 1;
            }
        }
        float rock = _enemy.Frozen ? 0 : Mathf.Sin(Time.time * 8 + _phase) * 2;
        transform.rotation = Quaternion.Euler(0, 0, rock);
    }
}
