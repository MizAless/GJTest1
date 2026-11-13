using System;
using UnityEngine;

namespace GameEconomy.Shards
{
    [Serializable]
    public sealed class EnemyShardDropRule
    {
        public EnemyKind Enemy;
        [Range(0, 1)] public float Chance = 0.35f;
        [Min(1)] public int Minimum = 1;
        [Min(1)] public int Maximum = 1;
    }

    [Serializable]
    public sealed class ShardAppearance
    {
        public ShardKind Kind;
        [Min(0)] public float Weight = 1;
        public ShardPickup Prefab;
    }

    [CreateAssetMenu(menuName = "Prototype/Shards/Drop settings")]
    public sealed class ShardDropSettings : ScriptableObject
    {
        public const string ResourcePath = "Economy/Shards/ShardDrops";
        public ShardMotionSettings Motion;
        public EnemyShardDropRule[] Enemies =
        {
            new EnemyShardDropRule { Enemy = EnemyKind.Weak },
            new EnemyShardDropRule { Enemy = EnemyKind.Medium, Chance = 0.55f },
            new EnemyShardDropRule { Enemy = EnemyKind.Strong, Chance = 0.8f, Maximum = 2 },
            new EnemyShardDropRule { Enemy = EnemyKind.Boss, Chance = 1, Minimum = 3, Maximum = 5 }
        };
        public ShardAppearance[] Kinds;
        [Tooltip("Weights for T1 (best), T2, T3 respectively. Zero disables a grade.")]
        public Vector3 GradeWeights = new Vector3(5, 25, 70);
        [Range(0, 1)] public float ScatterRadius = 0.35f;

        public bool TryRoll(EnemyKind enemy, out int count)
        {
            count = 0;
            foreach (EnemyShardDropRule rule in Enemies)
            {
                if (rule.Enemy != enemy)
                {
                    continue;
                }
                float chance = Mathf.Clamp01(rule.Chance);
                if (chance <= 0 || (chance < 1 && UnityEngine.Random.value >= chance))
                {
                    return false;
                }
                int minimum = Mathf.Clamp(rule.Minimum, 1, 64);
                int maximum = Mathf.Clamp(rule.Maximum, minimum, 64);
                count = UnityEngine.Random.Range(minimum, maximum + 1);
                return true;
            }
            return false;
        }

        public ShardAppearance RollKind()
        {
            float total = 0;
            foreach (ShardAppearance kind in Kinds)
            {
                if (kind.Prefab != null)
                {
                    total += Mathf.Max(0, kind.Weight);
                }
            }
            if (total <= 0)
            {
                return null;
            }
            float roll = UnityEngine.Random.value * total;
            ShardAppearance last = null;
            foreach (ShardAppearance kind in Kinds)
            {
                if (kind.Prefab == null || kind.Weight <= 0)
                {
                    continue;
                }
                last = kind;
                roll -= kind.Weight;
                if (roll < 0)
                {
                    return kind;
                }
            }
            return last;
        }

        public bool TryRollGrade(out ShardGrade grade)
        {
            float best = Mathf.Max(0, GradeWeights.x);
            float middle = Mathf.Max(0, GradeWeights.y);
            float common = Mathf.Max(0, GradeWeights.z);
            float total = best + middle + common;
            grade = ShardGrade.T3;
            if (total <= 0)
            {
                return false;
            }
            float roll = UnityEngine.Random.value * total;
            if (best > 0 && roll <= best)
            {
                grade = ShardGrade.T1;
            }
            else if (middle > 0 && roll <= best + middle)
            {
                grade = ShardGrade.T2;
            }
            return true;
        }
    }
}
