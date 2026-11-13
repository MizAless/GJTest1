using System;
using UnityEngine;

namespace GameEconomy
{
    [CreateAssetMenu(menuName = "Game/Economy/Coin drops")]
    public sealed class CoinDropSettings : ScriptableObject
    {
        public const string ResourcePath = "Economy/CoinDropSettings";

        [Serializable]
        public sealed class DropRule
        {
            public EnemyKind Kind;
            [Min(1)] public int Count = 1;
            [Min(1)] public int Value = 1;
        }

        public DropRule[] Rules =
        {
            new DropRule { Kind = EnemyKind.Weak, Count = 1, Value = 1 },
            new DropRule { Kind = EnemyKind.Medium, Count = 2, Value = 1 },
            new DropRule { Kind = EnemyKind.Strong, Count = 3, Value = 2 },
            new DropRule { Kind = EnemyKind.Boss, Count = 8, Value = 5 }
        };
        [Min(0.1f)] public float HoverHeight = 0.45f;
        [Min(0.1f)] public float CoinDiameter = 0.48f;
        [Min(0)] public float ScatterRadius = 0.65f;
        [Min(0.1f)] public float ChainBudget = 2.4f;
        [Min(0.05f)] public float MaximumHopTime = 0.12f;
        [Min(0.05f)] public float FlyTime = 0.65f;
        [Min(0.01f)] public float TossTime = 0.14f;

        public DropRule GetRule(EnemyKind kind)
        {
            foreach (DropRule rule in Rules)
            {
                if (rule != null && rule.Kind == kind)
                {
                    return rule;
                }
            }
            throw new InvalidOperationException("Missing coin drop rule for " + kind);
        }
    }
}
