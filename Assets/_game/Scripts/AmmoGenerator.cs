using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class AmmoGenerator
{
    public static AmmoItem Generate(bool module, bool starter = false)
    {
        AmmoDropTable table = Resources.Load<AmmoDropTable>(AmmoDropTable.ResourcePath);
        if (table == null)
        {
            throw new InvalidOperationException("Missing Resources/" + AmmoDropTable.ResourcePath);
        }
        return Generate(table, module, starter);
    }

    public static AmmoItem Generate(AmmoDropTable table, bool module, bool starter = false,
        float rarityMultiplier = 1f)
    {
        if (table == null)
        {
            throw new ArgumentNullException(nameof(table));
        }
        if (float.IsNaN(rarityMultiplier) || float.IsInfinity(rarityMultiplier) || rarityMultiplier < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rarityMultiplier));
        }
        AmmoRarityDrop rarity = AmmoDropTable.Pick(table.Rarities,
            entry => GetRarityWeight(entry, starter, rarityMultiplier));
        if (rarity == null)
        {
            throw new InvalidOperationException("Ammo drop table has no eligible rarity with positive weight.");
        }
        AmmoItem item = new AmmoItem { Rarity = rarity.Rarity };
        if (!module && table.Archetypes.Count > 0)
        {
            item.Archetype = AmmoDropTable.Pick(table.Archetypes,
                entry => !starter || entry.AllowStarter ? entry.Weight : 0);
            if (item.Archetype == null)
            {
                throw new InvalidOperationException("Ammo drop table has no eligible cartridge type.");
            }
        }
        HashSet<AmmoPropertyKind> used = new HashSet<AmmoPropertyKind>();
        int count = starter ? 1 : Mathf.Max(1, rarity.PropertyCount);
        for (int index = 0; index < count; index++)
        {
            AmmoPropertyDrop property = AmmoDropTable.Pick(table.Properties, entry =>
                !used.Contains(entry.Property) && entry.IsAvailable(module, rarity.Rarity, starter)
                    && entry.Tiers.Exists(tier => IsTierAvailable(table, tier, rarity.Rarity, starter))
                    ? entry.Weight : 0);
            if (property == null)
            {
                // Small custom pools produce fewer affixes instead of duplicates.
                break;
            }
            AmmoPropertyTier tier = AmmoDropTable.Pick(property.Tiers,
                entry => IsTierAvailable(table, entry, rarity.Rarity, starter) ? entry.Weight : 0);
            Apply(item.Properties, property.Property, tier.RollValue(property.Property));
            used.Add(property.Property);
        }
        if (used.Count == 0)
        {
            throw new InvalidOperationException("Ammo drop table has no eligible property with positive weight.");
        }
        if (!starter && UnityEngine.Random.value < rarity.TriggerChance)
        {
            AmmoTriggerDrop trigger = AmmoDropTable.Pick(table.Triggers,
                entry => entry.IsAvailable(module, rarity.Rarity) ? entry.Weight : 0);
            if (trigger != null)
            {
                item.Trigger = trigger.Trigger;
            }
        }
        item.Name = item.Archetype != null
            ? item.Archetype.Name
            : (module ? "Модуль: " : "Патрон: ") + item.Properties.Description().Split('\n')[0];
        return item;
    }

    // Relative weights are normalized by AmmoDropTable.Pick; the shared entries stay untouched.
    public static float GetRarityWeight(AmmoRarityDrop entry, bool starter, float multiplier)
    {
        if (entry == null || (starter && entry.Rarity != BulletRarity.Common))
        {
            return 0;
        }
        if (entry.Rarity == BulletRarity.Common)
        {
            return entry.Weight;
        }
        return entry.Weight * multiplier;
    }

    private static bool IsTierAvailable(AmmoDropTable table, AmmoPropertyTier tier,
        BulletRarity rarity, bool starter)
    {
        return tier != null && tier.IsAvailable(rarity, starter)
            && (!starter || tier.PowerRank <= table.StarterMaximumPowerRank);
    }

    public static void Apply(AmmoProperties properties, AmmoPropertyKind kind, float value)
    {
        int count = Mathf.RoundToInt(value);
        switch (kind)
        {
            case AmmoPropertyKind.Ricochet: properties.Ricochet = count; break;
            case AmmoPropertyKind.Pierce: properties.Pierce = count; break;
            case AmmoPropertyKind.Fan: properties.Fan = count; break;
            case AmmoPropertyKind.Burst: properties.Burst = count; break;
            case AmmoPropertyKind.Split: properties.Split = count; break;
            case AmmoPropertyKind.Burn: properties.Burn = count; break;
            case AmmoPropertyKind.Chill: properties.Chill = count; break;
            case AmmoPropertyKind.Bleed: properties.Bleed = count; break;
            case AmmoPropertyKind.Poison: properties.Poison = count; break;
            case AmmoPropertyKind.Puncture: properties.Puncture = count; break;
            case AmmoPropertyKind.Overload: properties.Overload = value > 0; break;
            case AmmoPropertyKind.More: properties.More = value; break;
            case AmmoPropertyKind.ProjectileMore: properties.ProjectileMore = value; break;
            case AmmoPropertyKind.DotMore: properties.DotMore = value; break;
            case AmmoPropertyKind.AreaMore: properties.AreaMore = value; break;
            case AmmoPropertyKind.AfflictedMore: properties.AfflictedMore = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    public static AmmoItem[] Offer(bool module)
    {
        List<AmmoItem> items = new List<AmmoItem>();
        for (int index = 0; index < 3; index++)
        {
            AmmoItem item;
            int attempts = 0;
            do
            {
                item = Generate(module);
                attempts++;
            }
            while (items.Any(existing => existing.Description == item.Description) && attempts < 30);
            items.Add(item);
        }
        return items.ToArray();
    }
}
