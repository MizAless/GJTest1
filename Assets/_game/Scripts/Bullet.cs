using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [SerializeField] private List<ParticleSystem> _vfxs = new List<ParticleSystem>();
    private ShotContext _context;
    private CombatProfile _profile;
    private Rigidbody2D _body;
    private HashSet<int> _visited;
    private int _pierce;
    private int _bounces;
    private float _expires;
    private bool _destroying;
    private bool _threeDimensional;
    private Vector3 _velocity;
    private LineRenderer _tracer;
    private static Material _tracerMaterial;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _body.gravityScale = 0;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        foreach (Collider2D collider in GetComponents<Collider2D>())
        {
            collider.isTrigger = true;
        }
    }

    public void Configure(ShotContext context, CombatProfile profile, Vector3 direction, float speed, HashSet<int> visited)
    {
        _context = context;
        _profile = profile;
        _visited = new HashSet<int>(visited);
        _pierce = profile.Properties.Pierce;
        _bounces = profile.Properties.Ricochet;
        _threeDimensional = StaticShooterStage.Instance != null;
        _expires = Time.time + (_threeDimensional ? 2.5f : 1.2f);
        _velocity = direction.normalized * (_threeDimensional ? speed * 3 : speed);
        if (_threeDimensional)
        {
            _body.simulated = false;
            SpriteRenderer sprite = GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                sprite.enabled = false;
            }
            CreateTracer();
        }
        else
        {
            _body.velocity = _velocity;
            SpriteRenderer sprite = GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                sprite.color = profile.Color;
            }
        }
    }

    private void CreateTracer()
    {
        if (_tracerMaterial == null)
        {
            _tracerMaterial = new Material(Shader.Find("Sprites/Default"));
        }
        _tracer = gameObject.AddComponent<LineRenderer>();
        _tracer.sharedMaterial = _tracerMaterial;
        _tracer.positionCount = 2;
        _tracer.startWidth = 0.035f;
        _tracer.endWidth = 0.015f;
        _tracer.startColor = _profile.Color;
        _tracer.endColor = new Color(1, 0.9f, 0.5f, 0);
        _tracer.sortingOrder = 150;
        _tracer.SetPosition(0, transform.position);
        _tracer.SetPosition(1, transform.position);
    }

    public void ChangeDirection(Vector3 direction)
    {
        _velocity = direction.normalized * _velocity.magnitude;
        if (!_threeDimensional)
        {
            _body.velocity = _velocity;
        }
    }

    private void Update()
    {
        if (_context == null || !_context.Active || Time.time > _expires)
        {
            Finish();
            return;
        }
        if (!_threeDimensional || _destroying)
        {
            return;
        }
        Vector3 from = transform.position;
        float distance = _velocity.magnitude * Time.deltaTime;
        Vector3 direction = _velocity.normalized;
        RaycastHit[] hits = Physics.SphereCastAll(from, 0.045f, direction, distance,
            ~0, QueryTriggerInteraction.Collide).OrderBy(hit => hit.distance).ToArray();
        foreach (RaycastHit hit in hits)
        {
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null && !_visited.Contains(enemy.GetInstanceID()))
            {
                transform.position = hit.point;
                HandleHit(enemy, direction);
                // Continue with a fresh sweep after a bounce; piercing may cross several targets this frame.
                if (_destroying || Vector3.Dot(direction, _velocity.normalized) < 0.99f)
                {
                    return;
                }
            }
        }
        transform.position = from + direction * distance;
        _tracer.SetPosition(0, transform.position);
        _tracer.SetPosition(1, transform.position - _velocity.normalized * 0.8f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_threeDimensional && other.TryGetComponent(out Enemy enemy))
        {
            HandleHit(enemy, _body.velocity.normalized);
        }
    }

    private void HandleHit(Enemy enemy, Vector3 direction)
    {
        if (_destroying || _context == null || !_context.Active || !enemy.IsAlive
            || !_visited.Add(enemy.GetInstanceID()))
        {
            return;
        }
        _context.Hits++;
        if (_context.Hits > 160)
        {
            Finish();
            return;
        }
        Vector3 position = enemy.AimPosition;
        _context.HitTargets.Add(enemy);
        enemy.ReceiveHit(_profile, direction);
        Enemy[] branches = G.Enemies.Where(target => target != null && target.IsAlive
            && !_visited.Contains(target.GetInstanceID()))
            .OrderBy(target => (target.transform.position - position).sqrMagnitude)
            .Take(Mathf.Min(8, _profile.Properties.Split)).ToArray();
        foreach (Enemy next in branches)
        {
            _context.Owner.SpawnProjectile(_context, _profile, position,
                (next.AimPosition - position).normalized, _visited);
        }
        if (_pierce > 0)
        {
            _pierce--;
            return;
        }
        if (_bounces > 0)
        {
            Enemy next = Shooter.Nearest(position, _visited);
            if (next != null)
            {
                _bounces--;
                GameAudio.Play(SoundCue.Ricochet, position);
                ChangeDirection(next.AimPosition - transform.position);
                return;
            }
        }
        Finish();
    }

    private void Finish()
    {
        if (_destroying)
        {
            return;
        }
        _destroying = true;
        foreach (Collider2D collider in GetComponents<Collider2D>())
        {
            collider.enabled = false;
        }
        foreach (ParticleSystem effect in _vfxs)
        {
            if (effect != null)
            {
                effect.transform.SetParent(null);
                effect.Stop();
                Destroy(effect.gameObject, 1);
            }
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_context != null)
        {
            _context.Pending = Mathf.Max(0, _context.Pending - 1);
        }
    }
}
