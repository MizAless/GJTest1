using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    [SerializeField] Transform _player;
    [SerializeField] float _speed = 1.6f;
    [SerializeField] ParticleSystem _onDeadVFX;
    [SerializeField] ParticleSystem _hitVFX;
    [SerializeField] private ParticleSystem _hitBlood3D;
    [SerializeField] private ParticleSystem _deathBlood3D;
    [SerializeField] float _maxHealth = 30;
    public float Health { get; private set; }
    public float MaxHealth => _maxHealth;
    public bool HasTakenDamage { get; private set; }
    public Vector3 AimPosition => StaticShooterStage.Instance == null
        ? transform.position
        : transform.position + Vector3.up * (1.4f * transform.localScale.y);
    public bool IsAlive => !_dead && Health > 0;
    public int Cold { get; private set; }
    public int Punctures { get; private set; }
    public bool Frozen { get; private set; }
    public bool HasStatus => Cold > 0 || Punctures > 0 || Frozen || _dots.Count > 0;
    public string Kind { get; private set; } = "Хилый";
    readonly List<DotCharge> _dots = new List<DotCharge>();
    SpriteRenderer _sprite, _healthBar;
    Color _baseColor = Color.white;
    float _flashUntil;
    bool _dead;
    Vector3 _knockback;
    static Sprite _square;
    private PlayerHealth _playerHealth;
    private float _nextContactAttack;
    private float _nextPainReaction;
    private int _receivedHits;
    public const float NaturalOverloadThreshold = 5f;

    private float _fireOverloadProgress;
    private float _poisonOverloadProgress;

    public float FireOverloadProgress => _fireOverloadProgress;
    public float PoisonOverloadProgress => _poisonOverloadProgress;

    private class DotCharge
    {
        public string Type;
        public float Damage;
        public int Ticks;
        public float OverloadDamage;
        public float SpreadDamage;
    }
    public string StatusSummary => string.Join("  ", new[] {
        Count("огонь") > 0 ? "Огонь " + Count("огонь") + " [" + _fireOverloadProgress.ToString("0.#") + "/5]" : "",
        Count("яд") > 0 ? "Яд " + Count("яд") + " [" + _poisonOverloadProgress.ToString("0.#") + "/5]" : "",
        Count("кровь") > 0 ? "Кровь " + Count("кровь") : "",
        Cold > 0 ? "Холод " + Cold : "", Punctures > 0 ? "Прокол " + Punctures : "", Frozen ? "Заморозка" : ""
    }.Where(x => x.Length > 0));
    public int Count(string type) => _dots.Count(x => x.Type == type);
    void Awake()
    {
        var shooter = FindAnyObjectByType<Shooter>();
        if (shooter != null) _player = shooter.transform;
        if (shooter != null)
        {
            _playerHealth = shooter.GetComponent<PlayerHealth>();
        }
        _sprite = GetComponent<SpriteRenderer>();
        Health = _maxHealth;
    }
    public void Configure(int health)
    {
        _maxHealth = Health = health;
        HasTakenDamage = false;
        Kind = health >= 200 ? "Босс" : health >= 80 ? "Большой" : health >= 50 ? "Средний" : "Хилый";
        float size = health >= 200 ? 2.6f : health >= 80 ? 1.8f : health >= 50 ? 1.15f : 1f;
        transform.localScale *= size;
        _baseColor = health >= 200 ? new Color(1,.48f,.42f) : health >= 80 ? new Color(1,.77f,.57f) : Color.white;
        _sprite.color = _baseColor;
        if (_square == null) _square = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height), new Vector2(.5f,.5f), 1);
        var back = new GameObject("Health background", typeof(SpriteRenderer));
        back.transform.SetParent(transform, false); back.transform.localPosition = new Vector3(0,.6f,0); back.transform.localScale = new Vector3(.45f,.035f,1);
        var bg = back.GetComponent<SpriteRenderer>(); bg.sprite = _square; bg.sortingOrder = 55; bg.color = new Color(.13f,.1f,.1f);
        var fill = new GameObject("Health", typeof(SpriteRenderer)); fill.transform.SetParent(back.transform, false);
        _healthBar = fill.GetComponent<SpriteRenderer>(); _healthBar.sprite = _square; _healthBar.sortingOrder = 56; _healthBar.color = new Color(.95f,.35f,.26f);
        UpdateHealthBar();
    }
    void Update()
    {
        if (!IsAlive) return;
        _sprite.color = Time.time < _flashUntil ? new Color(2,2,2,1) : Frozen ? Color.cyan : _baseColor;
        UpdateHealthBar();
        if (RunDirector.Instance == null || !RunDirector.Instance.CombatActive || _player == null) return;
        TryContactAttack();
        float speed = Frozen ? 0 : _speed * Mathf.Max(.25f,1-Cold*.15f) * (_maxHealth >= 80 ? .65f : 1);
        if (StaticShooterStage.Instance != null)
        {
            _knockback.y = 0;
        }
        if (Vector3.Distance(transform.position,_player.position) > .65f)
            transform.position = Vector3.MoveTowards(transform.position,_player.position,speed*Time.deltaTime);
        transform.position += _knockback * Time.deltaTime; _knockback = Vector3.Lerp(_knockback,Vector3.zero,12*Time.deltaTime);
    }
    private void TryContactAttack()
    {
        if (Frozen || Time.time < _nextContactAttack || (_player.position - transform.position).sqrMagnitude > 0.8f * 0.8f)
        {
            return;
        }

        if (_playerHealth == null)
        {
            _playerHealth = _player.GetComponent<PlayerHealth>();
        }

        _nextContactAttack = Time.time + 1.1f;
        if (_playerHealth != null)
        {
            _playerHealth.TryTakeDamage(10);
        }
    }

    public void ReceiveHit(CombatProfile profile, Vector3 direction)
    {
        if (!IsAlive)
        {
            return;
        }

        GameAudio.Play(SoundCue.EnemyHit, transform.position);
        bool shatter = Frozen;
        Frozen = false;
        float damage = profile.Damage * (HasStatus || shatter ? profile.Afflicted : 1);
        if (shatter)
        {
            damage *= 1.5f;
        }

        TakeDamage(damage, profile.Color, direction);
        _receivedHits++;
        if (shatter)
        {
            CombatEffects.Area(transform.position, 15 * profile.All * profile.Area, Color.cyan);
        }

        if (!IsAlive)
        {
            return;
        }

        AmmoProperties properties = profile.Properties;
        AddDots("огонь", properties.Burn, 2 * profile.All * profile.Dot, 3, profile);
        AddDots("яд", properties.Poison, 1.5f * profile.All * profile.Dot, 4, profile);
        AddDots("кровь", properties.Bleed, 2.5f * profile.All * profile.Dot, 2, profile);
        Cold += properties.Chill;
        Punctures += properties.Puncture;

        if (Punctures > 0)
        {
            TakeDamage(2 * Punctures * Punctures * profile.All, new Color(0.8f, 0.5f, 1), Vector3.zero);
        }

        if (!IsAlive || !properties.Overload)
        {
            TryPlayPainReaction();
            return;
        }

        // Forced overload needs any active fire/poison charge, even before its first tick.
        if (Count("огонь") > 0)
        {
            TriggerDotOverload("огонь", 15 * profile.All * profile.Area,
                1.5f * profile.All * profile.Dot, consumeStatus: true);
        }

        if (!IsAlive)
        {
            return;
        }

        // Cold and puncture retain their existing charge thresholds for this test.
        if (Cold >= 3)
        {
            Cold = 0;
            Frozen = true;
        }

        if (Count("яд") > 0)
        {
            TriggerDotOverload("яд", 10 * profile.All * profile.Area,
                1.5f * profile.All * profile.Dot, consumeStatus: true);
        }

        if (IsAlive && Punctures >= 2)
        {
            Punctures = 0;
            TakeDamage(40 * profile.All, new Color(0.8f, 0.5f, 1), Vector3.zero);
        }
        TryPlayPainReaction();
    }

    private void TryPlayPainReaction()
    {
        // Resolve puncture/overload first: a fatal hit should not also start a pain voice.
        if (IsAlive && _receivedHits % 3 == 1 && Time.time >= _nextPainReaction)
        {
            _nextPainReaction = Time.time + 0.8f;
            GameAudio.Play(SoundCue.EnemyPain, transform.position);
        }
    }

    public void AddDots(string type, int count, float damage, int ticks, CombatProfile source = null)
    {
        float allMultiplier = source == null ? 1 : source.All;
        float areaMultiplier = source == null ? 1 : source.Area;
        float dotMultiplier = source == null ? 1 : source.Dot;
        float explosionBase = type == "огонь" ? 15 : 10;

        for (int index = 0; index < count; index++)
        {
            _dots.Add(new DotCharge
            {
                Type = type,
                Damage = damage,
                Ticks = ticks,
                OverloadDamage = explosionBase * allMultiplier * areaMultiplier,
                SpreadDamage = 1.5f * allMultiplier * dotMultiplier
            });
        }
    }

    private void TriggerDotOverload(string type, float damage, float spreadDamage, bool consumeStatus)
    {
        if (type == "огонь")
        {
            _fireOverloadProgress = 0;
        }
        else
        {
            _poisonOverloadProgress = 0;
        }

        if (consumeStatus)
        {
            _dots.RemoveAll(charge => charge.Type == type);
        }

        Enemy[] neighbors = type == "яд"
            ? CombatEffects.InRadius(transform.position).Where(enemy => enemy != this).ToArray()
            : new Enemy[0];
        Color color = type == "огонь" ? new Color(1, 0.45f, 0.1f) : new Color(0.45f, 1, 0.15f);
        CombatEffects.Area(transform.position, damage, color);

        foreach (Enemy neighbor in neighbors)
        {
            if (neighbor.IsAlive)
            {
                // Spreading poison adds a charge; it never triggers another overload immediately.
                neighbor._dots.Add(new DotCharge
                {
                    Type = "яд",
                    Damage = spreadDamage,
                    Ticks = 4,
                    OverloadDamage = damage,
                    SpreadDamage = spreadDamage
                });
            }
        }
    }

    public static void TickAllStatuses(IEnumerable<Enemy> enemies)
    {
        Enemy[] targets = enemies.Where(enemy => enemy != null && enemy.IsAlive).ToArray();
        DotCharge[][] charges = targets.Select(enemy => enemy._dots.ToArray()).ToArray();

        // Snapshot the whole arena so poison spread cannot tick early based on enemy list order.
        for (int index = 0; index < targets.Length; index++)
        {
            targets[index].TickStatuses(charges[index]);
        }
    }

    public void TickStatuses()
    {
        TickStatuses(_dots.ToArray());
    }

    private void TickStatuses(DotCharge[] snapshot)
    {
        if (!IsAlive)
        {
            return;
        }

        foreach (string type in new[] { "огонь", "яд", "кровь" })
        {
            DotCharge[] charges = snapshot.Where(charge => charge.Type == type).ToArray();
            float damage = charges.Sum(charge => charge.Damage);
            foreach (DotCharge charge in charges)
            {
                charge.Ticks--;
            }

            _dots.RemoveAll(charge => charge.Ticks <= 0);
            float previousHealth = Health;
            Color color = type == "яд" ? Color.green : Color.red;
            if (type == "огонь")
            {
                color = new Color(1, 0.5f, 0.15f);
            }

            TakeDamage(damage, color, Vector3.zero);
            if (!IsAlive)
            {
                return;
            }

            if (type == "огонь" || type == "яд")
            {
                ApplyNaturalOverload(type, previousHealth - Health, charges);
            }

            if (!IsAlive)
            {
                return;
            }
        }
    }

    private void ApplyNaturalOverload(string type, float damageDealt, DotCharge[] charges)
    {
        float progress = type == "огонь" ? _fireOverloadProgress : _poisonOverloadProgress;
        progress += damageDealt;

        // At most one overload per state per tick; excess charge is spent on this overload.
        if (progress >= NaturalOverloadThreshold && charges.Length > 0)
        {
            DotCharge strongest = charges.OrderByDescending(charge => charge.OverloadDamage).First();
            TriggerDotOverload(type, strongest.OverloadDamage, strongest.SpreadDamage, consumeStatus: false);
            progress = 0;
        }

        // Expired states cannot leave stored charge for a later, unrelated application.
        if (Count(type) == 0)
        {
            progress = 0;
        }

        if (type == "огонь")
        {
            _fireOverloadProgress = progress;
        }
        else
        {
            _poisonOverloadProgress = progress;
        }
    }
    private void UpdateHealthBar()
    {
        if (_healthBar == null)
        {
            return;
        }
        _healthBar.transform.parent.gameObject.SetActive(HasTakenDamage && IsAlive);
        float fraction = Mathf.Clamp01(Health / _maxHealth);
        _healthBar.transform.localScale = new Vector3(fraction, 1, 1);
        // Compensate the centered sprite pivot so only its right edge moves as health falls.
        float centerOffset = (fraction - 1) * _square.bounds.extents.x;
        _healthBar.transform.localPosition = new Vector3(centerOffset, 0, StaticShooterStage.Instance != null ? -0.015f : 0);
    }

    public void TakeDamage() => TakeDamage(10,Color.white,Vector3.zero);
    public void TakeDamage(float amount, Color color, Vector3 direction)
    {
        if (!IsAlive || amount <= 0) return;
        Health = Mathf.Max(0,Health-amount);
        HasTakenDamage = true;
        UpdateHealthBar();
        _flashUntil = Time.time + .11f; _knockback += direction.normalized * 1.8f;
        if (RunUI.Instance != null) RunUI.Instance.Damage(AimPosition,amount,color);
        ParticleSystem hitPrefab = StaticShooterStage.Instance != null && _hitBlood3D != null ? _hitBlood3D : _hitVFX;
        if (hitPrefab != null)
        {
            var hit = Instantiate(hitPrefab,AimPosition,Quaternion.identity);
            PerspectiveEffects.Prepare(hit, AimPosition, 1.7f);
            hit.Play(true);
            PhysicalBlood blood = hit.GetComponent<PhysicalBlood>();
            Destroy(hit.gameObject, blood != null ? blood.CleanupDelay : 1);
        }
        if (Health <= 0) Die();
    }
    void Die()
    {
        if (_dead) return; _dead = true;
        GameAudio.Play(SoundCue.EnemyDeath, transform.position);
        G.Enemies.Remove(this);
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }
        ParticleSystem deathPrefab = StaticShooterStage.Instance != null && _deathBlood3D != null ? _deathBlood3D : _onDeadVFX;
        if (deathPrefab != null)
        {
            ParticleSystem effect = Instantiate(deathPrefab, AimPosition, Quaternion.identity);
            PerspectiveEffects.Prepare(effect, AimPosition, 1.5f);
            effect.Play(true);
            PhysicalBlood blood = effect.GetComponent<PhysicalBlood>();
            Destroy(effect.gameObject, blood != null ? blood.CleanupDelay : 2);
        }
        Destroy(gameObject);
    }
    void OnDestroy() { G.Enemies.Remove(this); }
}

public static class CombatEffects
{
    public const float Radius = 2.1f;
    public static Enemy[] InRadius(Vector3 origin) => G.Enemies.Where(e=>e!=null&&e.IsAlive&&(e.transform.position-origin).sqrMagnitude<=Radius*Radius).ToArray();
    public static void Area(Vector3 origin, float damage, Color color)
    {
        GameAudio.Play(SoundCue.Explosion, origin);
        if (G.DataBase.ExplosionPrefab != null) Object.Instantiate(G.DataBase.ExplosionPrefab,origin,Quaternion.identity).Visual(Radius,color);
        foreach (var e in InRadius(origin)) e.TakeDamage(damage,color,e.transform.position-origin);
    }
}
