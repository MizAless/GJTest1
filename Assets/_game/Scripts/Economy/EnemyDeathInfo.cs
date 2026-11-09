using System;
using UnityEngine;

namespace GameEconomy
{
    public enum EnemyKind { Weak, Medium, Strong, Boss }
    public enum DamageKind { Unknown, Projectile, Status, Area }

    public readonly struct DamageSource
    {
        public DamageKind Kind { get; }
        public int ActorId { get; }
        public string Status { get; }
        public DamageSource(DamageKind kind, int actorId = 0, string status = null)
        {
            Kind = kind;
            ActorId = actorId;
            Status = status;
        }
    }

    // Value snapshot: subscribers do not need to keep a dying Unity object alive.
    public readonly struct EnemyDeathInfo
    {
        public Guid DeathId { get; }
        public Guid RunId { get; }
        public int EnemyId { get; }
        public EnemyKind Kind { get; }
        public float MaximumHealth { get; }
        public Vector3 Position { get; }
        public Vector3 AimPosition { get; }
        public int Wave { get; }
        public int Group { get; }
        public DamageSource Source { get; }

        public EnemyDeathInfo(Guid runId, int enemyId, EnemyKind kind, float maximumHealth,
            Vector3 position, Vector3 aimPosition, int wave, int group, DamageSource source)
        {
            DeathId = Guid.NewGuid();
            RunId = runId;
            EnemyId = enemyId;
            Kind = kind;
            MaximumHealth = maximumHealth;
            Position = position;
            AimPosition = aimPosition;
            Wave = wave;
            Group = group;
            Source = source;
        }
    }
}
