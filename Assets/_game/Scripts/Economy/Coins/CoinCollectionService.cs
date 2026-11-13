using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEconomy
{
    [DefaultExecutionOrder(100)]
    public sealed class CoinCollectionService : MonoBehaviour, ICoinCollectionService
    {
        private sealed class Flight
        {
            public CoinVisual Coin;
            public Vector3 Origin;
            public float Age;
        }

        private readonly List<CoinVisual> _pending = new List<CoinVisual>();
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly HashSet<Guid> _deaths = new HashSet<Guid>();
        private RunEconomyContext _run;
        private CoinDropSettings _settings;
        private CoinCollectionRequest _request;
        private CoinVisual _next;
        private Transform _projectile;
        private Material _projectileMaterial;
        private Vector3 _segmentOrigin;
        private float _segmentAge;
        private float _hopTime;
        private int _generation;
        private Shooter _shooter;
        private bool _restoreShooter;
        private RevolverVisual _weapon;
        private bool _fireOnLateUpdate;
        public bool IsCollecting { get; private set; }
        public int PendingCount => _pending.Count + _flights.Count;
        public event Action<CoinVisual> CoinStruck;

        public void Initialize(RunEconomyContext run, CoinDropSettings settings = null)
        {
            if (run == null || !run.IsActive)
            {
                throw new ArgumentException("An active run is required.", nameof(run));
            }
            Unbind();
            _settings = settings != null ? settings : Resources.Load<CoinDropSettings>(CoinDropSettings.ResourcePath);
            if (_settings == null)
            {
                throw new InvalidOperationException("Missing CoinDropSettings resource.");
            }
            _run = run;
            _run.EnemyDied += OnEnemyDied;
            _run.Ended += Unbind;
        }

        private void OnEnemyDied(EnemyDeathInfo death)
        {
            if (_run == null || !_run.IsActive || death.RunId != _run.RunId || !_deaths.Add(death.DeathId))
            {
                return;
            }
            CoinDropSettings.DropRule rule = _settings.GetRule(death.Kind);
            int count = Mathf.Max(1, rule.Count);
            int value = Mathf.Max(1, rule.Value);
            float floor = StaticShooterStage.Instance != null ? StaticShooterStage.Instance.GroundSurface.position.y : death.Position.y;
            for (int index = 0; index < count; index++)
            {
                float angle = index * 2.399963f + death.EnemyId;
                float radius = _settings.ScatterRadius * Mathf.Sqrt((index + 0.5f) / count);
                Vector3 ground = death.Position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                ground.y = floor;
                CoinVisual coin = new GameObject("Pending coin", typeof(CoinVisual)).GetComponent<CoinVisual>();
                coin.transform.SetParent(transform, false);
                coin.Initialize(value, ground, _settings, angle);
                _pending.Add(coin);
            }
        }

        public IEnumerator CollectAll(CoinCollectionRequest request)
        {
            if (request.Run != _run || _run == null || !_run.IsActive)
            {
                yield break;
            }
            if (IsCollecting)
            {
                int joinedGeneration = _generation;
                while (IsCollecting && joinedGeneration == _generation)
                {
                    yield return null;
                }
                yield break;
            }
            if (_pending.Count == 0)
            {
                yield break;
            }
            if (request.Muzzle == null || request.CollectionTarget == null)
            {
                throw new ArgumentException("Collection requires a muzzle and a separate hero target.");
            }
            _request = request;
            IsCollecting = true;
            _generation++;
            int generation = _generation;
            _hopTime = Mathf.Min(Mathf.Max(0.01f, _settings.MaximumHopTime), Mathf.Max(0.05f, _settings.ChainBudget) / _pending.Count);
            _shooter = FindObjectOfType<Shooter>();
            _restoreShooter = _shooter != null && _shooter.enabled;
            if (_shooter != null)
            {
                _shooter.enabled = false;
            }
            _weapon = request.Muzzle.GetComponentInParent<RevolverVisual>();
            _segmentOrigin = request.Muzzle.position;
            _next = FindFarthest(_segmentOrigin);
            _fireOnLateUpdate = true;
            CreateProjectile();
            while (IsCollecting && generation == _generation)
            {
                yield return null;
            }
        }

        public static CoinVisual Farthest(IReadOnlyList<CoinVisual> coins, Vector3 from)
        {
            CoinVisual result = null;
            float greatest = -1;
            foreach (CoinVisual coin in coins)
            {
                if (coin == null)
                {
                    continue;
                }
                float distance = (coin.transform.position - from).sqrMagnitude;
                if (distance > greatest)
                {
                    greatest = distance;
                    result = coin;
                }
            }
            return result;
        }

        private CoinVisual FindFarthest(Vector3 from)
        {
            return Farthest(_pending, from);
        }

        private void LateUpdate()
        {
            if (!IsCollecting)
            {
                return;
            }
            if (_run == null || !_run.IsActive || _request.Muzzle == null || _request.CollectionTarget == null)
            {
                Cancel();
                return;
            }
            if (_fireOnLateUpdate)
            {
                // Override the stage's normal aim, then sample the actual muzzle AFTER its rotation.
                if (_weapon != null)
                {
                    Vector3 up = Camera.main != null ? Camera.main.transform.up : Vector3.up;
                    _weapon.AimAt(_next.transform.position, up);
                    _weapon.Fire();
                }
                GameAudio.Play(SoundCue.Shot, _request.Muzzle.position);
                _segmentOrigin = _request.Muzzle.position;
                _projectile.position = _segmentOrigin;
                _fireOnLateUpdate = false;
                return;
            }
            AdvanceChain(Time.deltaTime);
            if (!IsCollecting)
            {
                return;
            }
            AdvanceFlights(Time.deltaTime);
            if (_pending.Count == 0 && _flights.Count == 0)
            {
                Finish();
            }
        }

        private void AdvanceChain(float deltaTime)
        {
            _segmentAge += deltaTime;
            while (_next != null && _segmentAge >= _hopTime)
            {
                _segmentAge -= _hopTime;
                Vector3 impact = _next.transform.position;
                CoinVisual struck = _next;
                _pending.Remove(struck);
                struck.InFlight = true;
                _flights.Add(new Flight { Coin = struck, Origin = impact });
                _segmentOrigin = impact;
                _next = FindFarthest(impact);
                _projectile.GetComponent<TrailRenderer>().AddPosition(impact);
                CoinStruck?.Invoke(struck);
                if (!IsCollecting)
                {
                    return;
                }
            }
            if (_next != null)
            {
                _projectile.position = Vector3.Lerp(_segmentOrigin, _next.transform.position, _segmentAge / _hopTime);
            }
            else if (_projectile != null)
            {
                _projectile.gameObject.SetActive(false);
            }
        }

        private void AdvanceFlights(float deltaTime)
        {
            float tossDuration = Mathf.Max(0.01f, _settings.TossTime);
            float flyDuration = Mathf.Max(0.05f, _settings.FlyTime);
            for (int index = _flights.Count - 1; index >= 0; index--)
            {
                Flight flight = _flights[index];
                flight.Age += deltaTime;
                Vector3 top = flight.Origin + Vector3.up * 0.7f;
                if (flight.Age < tossDuration)
                {
                    float toss = flight.Age / tossDuration;
                    flight.Coin.transform.position = Vector3.Lerp(flight.Origin, top, Mathf.Sin(toss * Mathf.PI * 0.5f));
                    continue;
                }
                float progress = Mathf.Clamp01((flight.Age - tossDuration) / flyDuration);
                Vector3 point = Vector3.Lerp(top, _request.CollectionTarget.position, progress * progress);
                point.y += Mathf.Sin(progress * Mathf.PI) * 1.7f;
                flight.Coin.transform.position = point;
                if (progress >= 1)
                {
                    // Remove before notifying inventory observers: arrival can never be credited twice.
                    _flights.RemoveAt(index);
                    int value = flight.Coin.Value;
                    Destroy(flight.Coin.gameObject);
                    _run.Inventory.AddCoins(value);
                    if (!IsCollecting)
                    {
                        return;
                    }
                }
            }
        }

        private void CreateProjectile()
        {
            GameObject root = new GameObject("Coin ricochet (presentation only)", typeof(LineRenderer));
            root.transform.SetParent(transform, false);
            _projectile = root.transform;
            _projectileMaterial = new Material(Shader.Find("Sprites/Default"));
            LineRenderer core = root.GetComponent<LineRenderer>();
            core.sharedMaterial = _projectileMaterial;
            core.useWorldSpace = false;
            core.positionCount = 2;
            core.SetPosition(0, Vector3.zero);
            core.SetPosition(1, Vector3.up * 0.04f);
            core.startWidth = 0.14f;
            core.endWidth = 0.14f;
            core.startColor = Color.white;
            core.endColor = new Color(1, 0.85f, 0.2f);
            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = _projectileMaterial;
            trail.time = 0.16f;
            trail.startWidth = 0.09f;
            trail.endWidth = 0;
            trail.minVertexDistance = 0.02f;
            trail.startColor = new Color(1, 0.8f, 0.15f);
            trail.endColor = new Color(1, 0.5f, 0.05f, 0);
            _projectile.position = _segmentOrigin;
        }

        private void Finish()
        {
            IsCollecting = false;
            _next = null;
            _segmentAge = 0;
            _fireOnLateUpdate = false;
            if (_projectile != null)
            {
                Destroy(_projectile.gameObject);
            }
            if (_projectileMaterial != null)
            {
                Destroy(_projectileMaterial);
            }
            if (_restoreShooter && _shooter != null && _run != null && _run.IsActive)
            {
                _shooter.enabled = true;
            }
            _restoreShooter = false;
        }

        public void Cancel()
        {
            _generation++;
            Finish();
            foreach (CoinVisual coin in _pending)
            {
                if (coin != null)
                {
                    Destroy(coin.gameObject);
                }
            }
            foreach (Flight flight in _flights)
            {
                if (flight.Coin != null)
                {
                    Destroy(flight.Coin.gameObject);
                }
            }
            _pending.Clear();
            _flights.Clear();
        }

        private void Unbind()
        {
            Cancel();
            if (_run != null)
            {
                _run.EnemyDied -= OnEnemyDied;
                _run.Ended -= Unbind;
            }
            _run = null;
            _deaths.Clear();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
