using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameEconomy
{
    // These are temporary views of the original tiers, never a second balance table.
    internal sealed class CraftPropertyPool
    {
        public AmmoPropertyDrop Property;
        public List<AmmoPropertyTier> Tiers;
        public double[] TierWeights;
    }

    internal static class CraftAffixPool
    {
        public static List<CraftPropertyPool> Build(AmmoDropTable table, CraftRequest request,
            ShardKindRule kind, ShardGradeRule grade)
        {
            var result = new List<CraftPropertyPool>();
            if (table.Properties == null)
            {
                return result;
            }
            foreach (AmmoPropertyDrop property in table.Properties)
            {
                if (property == null || !PositiveFinite(property.Weight)
                    || !Enum.IsDefined(typeof(AmmoPropertyKind), property.Property)
                    || AffixValues.Read(request.Target.Properties, property.Property) > 0
                    || (request.IsModule ? !property.ForModules : !property.ForCartridges)
                    || (!kind.AllowAllProperties && !kind.AllowedProperties.Contains(property.Property)))
                {
                    continue;
                }
                var tiers = property.Tiers?.Where(tier => EligibleTier(tier, property.Property, request.Target.Rarity)).ToList();
                if (tiers == null || tiers.Count == 0)
                {
                    continue;
                }
                int[] ranks = tiers.Select(tier => tier.PowerRank).Distinct().OrderBy(rank => rank).ToArray();
                int excluded = Math.Min(grade.ExcludedWeakestTiers, ranks.Length - 1);
                int minimumRank = ranks[excluded];
                tiers.RemoveAll(tier => tier.PowerRank < minimumRank);
                double[] logs = tiers.Select(tier => Math.Log(tier.Weight)
                    + grade.PowerWeightExponent * Math.Log(tier.PowerRank)).ToArray();
                double maximumLog = logs.Max();
                result.Add(new CraftPropertyPool
                {
                    Property = property,
                    Tiers = tiers,
                    // Normalize in log space to avoid overflow at high ranks/exponents.
                    TierWeights = logs.Select(value => Math.Exp(value - maximumLog)).ToArray()
                });
            }
            return result;
        }

        private static bool EligibleTier(AmmoPropertyTier tier, AmmoPropertyKind property, BulletRarity rarity)
        {
            if (tier == null || tier.PowerRank <= 0 || !PositiveFinite(tier.Weight)
                || !tier.IsAvailable(rarity, false) || !PositiveFinite(tier.MinimumValue)
                || !PositiveFinite(tier.MaximumValue) || tier.MaximumValue < tier.MinimumValue)
            {
                return false;
            }
            if (property >= AmmoPropertyKind.More)
            {
                return true;
            }
            // Count affixes must be able to produce a positive integer. Reject unsafe bounds.
            return tier.MinimumValue >= 1 && tier.MaximumValue < int.MaxValue
                && Math.Ceiling(tier.MinimumValue) <= Math.Floor(tier.MaximumValue);
        }

        public static bool PositiveFinite(float value)
        {
            return value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static int PickIndex(double[] weights)
        {
            double total = weights.Sum();
            double roll = UnityEngine.Random.value * total;
            for (int index = 0; index < weights.Length; index++)
            {
                roll -= weights[index];
                if (roll < 0)
                {
                    return index;
                }
            }
            return weights.Length - 1;
        }
    }
}
