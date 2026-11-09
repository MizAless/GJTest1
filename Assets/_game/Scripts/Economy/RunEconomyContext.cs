using System;
using System.Collections.Generic;

namespace GameEconomy
{
    public sealed class RunEconomyContext : IDisposable
    {
        private readonly HashSet<int> _reportedEnemies = new HashSet<int>();
        public Guid RunId { get; } = Guid.NewGuid();
        public RunInventory Inventory { get; } = new RunInventory();
        public RunEconomySettings Settings { get; }
        public bool IsActive { get; private set; } = true;
        public event Action<EnemyDeathInfo> EnemyDied;
        public event Action Ended;

        public RunEconomyContext(RunEconomySettings settings)
        {
            Settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        }

        // Enemy.Die is the sole runtime publisher. Subscribers create drops, not additional deaths.
        internal void PublishDeath(EnemyDeathInfo info)
        {
            if (!IsActive || info.RunId != RunId || !_reportedEnemies.Add(info.EnemyId))
            {
                return;
            }
            Delegate[] subscribers = EnemyDied?.GetInvocationList();
            if (subscribers == null)
            {
                return;
            }
            foreach (Delegate subscriber in subscribers)
            {
                try
                {
                    ((Action<EnemyDeathInfo>)subscriber)(info);
                }
                catch (Exception exception)
                {
                    // A broken drop listener must not interrupt removal/VFX/destruction of the enemy.
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        public void Dispose()
        {
            if (!IsActive)
            {
                return;
            }
            IsActive = false;
            Delegate[] subscribers = Ended?.GetInvocationList();
            EnemyDied = null;
            Ended = null;
            _reportedEnemies.Clear();
            if (subscribers == null)
            {
                return;
            }
            foreach (Delegate subscriber in subscribers)
            {
                try
                {
                    ((Action)subscriber)();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }
    }
}
