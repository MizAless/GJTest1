using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameEconomy.Shards
{
    public sealed class ShardDropService : MonoBehaviour, IShardCollectionService
    {
        [SerializeField] private ShardDropSettings _settings;
        private readonly HashSet<Guid> _deaths = new HashSet<Guid>();
        private readonly List<ShardPickup> _pending = new List<ShardPickup>();
        private RunEconomyContext _run;
        private Transform _target;
        private Transform _groundSurface;
        private Guid _deduplicatedRun;
        public int PendingCount => _pending.Count;

        public void Initialize(RunEconomyContext run, Transform collectionTarget,
            ShardDropSettings settings = null, Transform groundSurface = null)
        {
            if (run == null || !run.IsActive || collectionTarget == null)
            {
                throw new ArgumentException("An active run and collection target are required.");
            }
            if (_run == run)
            {
                _target = collectionTarget;
                _groundSurface = groundSurface;
                return;
            }
            Unbind();
            _settings = settings != null ? settings : _settings;
            if (_settings == null || _settings.Motion == null || _settings.Kinds == null)
            {
                throw new InvalidOperationException("Shard drop and motion settings must be assigned.");
            }
            if (_deduplicatedRun != run.RunId)
            {
                _deaths.Clear();
                _deduplicatedRun = run.RunId;
            }
            _run = run;
            _target = collectionTarget;
            _groundSurface = groundSurface;
            _run.EnemyDied += OnEnemyDied;
            _run.Ended += OnRunEnded;
        }

        private void OnEnemyDied(EnemyDeathInfo death)
        {
            if (_run == null || !_run.IsActive || _target == null || death.RunId != _run.RunId || !_deaths.Add(death.DeathId))
            {
                return;
            }
            if (!_settings.TryRoll(death.Kind, out int count))
            {
                return;
            }
            for (int index = 0; index < count; index++)
            {
                ShardAppearance kind = _settings.RollKind();
                if (kind == null || !_settings.TryRollGrade(out ShardGrade grade))
                {
                    continue;
                }
                Vector2 scatter = UnityEngine.Random.insideUnitCircle * _settings.ScatterRadius;
                Vector3 ground = death.Position + new Vector3(scatter.x, 0, scatter.y);
                if (_groundSurface != null)
                {
                    ground.y = _groundSurface.position.y;
                }
                ShardPickup pickup = Instantiate(kind.Prefab, transform);
                pickup.Initialize(new ShardId(kind.Kind, grade), ground, _settings.Motion);
                _pending.Add(pickup);
            }
        }

        private void Update()
        {
            if (_run == null || !_run.IsActive || _target == null)
            {
                Cancel();
                return;
            }
            // Scaled time freezes both levitation and flight during a real pause.
            for (int index = _pending.Count - 1; index >= 0; index--)
            {
                if (index >= _pending.Count)
                {
                    continue;
                }
                ShardPickup pickup = _pending[index];
                if (pickup == null)
                {
                    _pending.RemoveAt(index);
                    continue;
                }
                pickup.Advance(Time.deltaTime, _target.position);
                if (!pickup.Arrived)
                {
                    continue;
                }
                _pending.RemoveAt(index);
                ShardId shard = pickup.Shard;
                Destroy(pickup.gameObject);
                // Remove first: an inventory notification may end the run reentrantly.
                RunEconomyContext scope = _run;
                scope.Inventory.AddShards(shard);
                if (_run != scope || !scope.IsActive)
                {
                    return;
                }
            }
        }

        public IEnumerator CollectAll()
        {
            RunEconomyContext scope = _run;
            while (_run == scope && scope != null && scope.IsActive && PendingCount > 0)
            {
                yield return null;
            }
        }

        public void Cancel()
        {
            foreach (ShardPickup pickup in _pending)
            {
                if (pickup != null)
                {
                    Destroy(pickup.gameObject);
                }
            }
            _pending.Clear();
        }

        private void OnRunEnded()
        {
            Unbind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (_run != null)
            {
                _run.EnemyDied -= OnEnemyDied;
                _run.Ended -= OnRunEnded;
            }
            _run = null;
            _target = null;
            _groundSurface = null;
            Cancel();
        }
    }
}
