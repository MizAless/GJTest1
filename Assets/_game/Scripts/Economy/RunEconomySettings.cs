using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameEconomy
{
    [Serializable]
    public sealed class ShardKindRule
    {
        public ShardKind Kind;
        public bool AllowAllProperties;
        public List<AmmoPropertyKind> AllowedProperties = new List<AmmoPropertyKind>();
    }

    [Serializable]
    public sealed class ShardGradeRule
    {
        public ShardGrade Grade = ShardGrade.T3;
        [Min(0)] public int ExcludedWeakestTiers;
        [Min(0)] public float PowerWeightExponent;
        [Min(1)] public int Cost = 1;
    }

    [CreateAssetMenu(menuName = "Prototype/Run economy settings")]
    public sealed class RunEconomySettings : ScriptableObject, IRarityMultiplierProvider
    {
        public const string ResourcePath = "Economy/RunEconomySettings";
        [Tooltip("Index 0: after wave 1. Only non-Common rarity weights receive this multiplier.")]
        public float[] RarityMultipliers = { 0.4f, 0.77f, 1.13f, 1.5f, 1.5f };
        public List<ShardKindRule> ShardKinds = new List<ShardKindRule>();
        public List<ShardGradeRule> ShardGrades = new List<ShardGradeRule>();
        [Min(1)] public int MaximumAdditionalProperties = 2;
        [Min(0)] public int MaximumAdditionalTriggers = 1;
        public bool AllowModuleCrafting = true;

        public float GetRarityMultiplier(int completedWave)
        {
            if (RarityMultipliers == null || RarityMultipliers.Length == 0)
            {
                throw new InvalidOperationException("Rarity multiplier configuration is empty.");
            }
            int index = Mathf.Clamp(completedWave - 1, 0, RarityMultipliers.Length - 1);
            float value = RarityMultipliers[index];
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            {
                throw new InvalidOperationException("Invalid rarity multiplier.");
            }
            return value;
        }

        public ShardKindRule GetKindRule(ShardKind kind)
        {
            return ShardKinds.Find(rule => rule != null && rule.Kind == kind)
                ?? throw new InvalidOperationException("Missing shard kind rule: " + kind);
        }

        public ShardGradeRule GetGradeRule(ShardGrade grade)
        {
            return ShardGrades.Find(rule => rule != null && rule.Grade == grade)
                ?? throw new InvalidOperationException("Missing shard grade rule: " + grade);
        }
    }
}
