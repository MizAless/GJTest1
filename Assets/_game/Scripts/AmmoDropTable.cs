using System;
using System.Collections.Generic;
using UnityEngine;

public enum AmmoPropertyKind
{
    Ricochet, Pierce, Fan, Burst, Split, Burn, Chill, Bleed,
    Poison, Puncture, Overload, More, ProjectileMore, DotMore, AreaMore, AfflictedMore
}

[Serializable]
public sealed class AmmoPropertyTier
{
    public string Name = "T1";
    [Tooltip("Explicit strength order within this property. Larger means stronger; independent of Name.")]
    [Min(1)] public int PowerRank = 1;
    [Min(0)] public float Weight = 100;
    public BulletRarity MinimumRarity = BulletRarity.Common;
    public BulletRarity MaximumRarity = BulletRarity.Legendary;
    public bool AllowStarter = true;
    [Tooltip("Inclusive range. Counts use integers; damage bonuses use percentages. Overload: 1 = enabled.")]
    [Min(0)] public float MinimumValue = 1;
    [Min(0)] public float MaximumValue = 1;

    public bool IsAvailable(BulletRarity rarity, bool starter)
    {
        return Weight > 0 && !float.IsInfinity(Weight)
            && rarity >= MinimumRarity && rarity <= MaximumRarity
            && (!starter || AllowStarter);
    }

    public float RollValue(AmmoPropertyKind kind)
    {
        float minimum = Mathf.Max(0, MinimumValue);
        float maximum = Mathf.Max(minimum, MaximumValue);
        if (kind >= AmmoPropertyKind.More)
        {
            return UnityEngine.Random.Range(minimum, maximum);
        }
        int lower = Mathf.CeilToInt(minimum);
        int upper = Mathf.Max(lower, Mathf.FloorToInt(maximum));
        return UnityEngine.Random.Range(lower, upper + 1);
    }
}

[Serializable]
public sealed class AmmoPropertyDrop
{
    public string Name;
    public AmmoPropertyKind Property;
    [Min(0)] public float Weight = 100;
    public bool ForCartridges = true;
    public bool ForModules = true;
    public bool AllowStarter = true;
    public List<AmmoPropertyTier> Tiers = new List<AmmoPropertyTier>();

    public bool IsAvailable(bool module, BulletRarity rarity, bool starter)
    {
        if (module ? !ForModules : !ForCartridges)
        {
            return false;
        }
        return (!starter || AllowStarter)
            && Tiers.Exists(tier => tier != null && tier.IsAvailable(rarity, starter));
    }
}

[Serializable]
public sealed class AmmoRarityDrop
{
    public BulletRarity Rarity;
    [Min(0)] public float Weight;
    [Min(1)] public int PropertyCount = 1;
    [Range(0, 1)] public float TriggerChance;
}

[Serializable]
public sealed class AmmoTriggerDrop
{
    public AmmoTrigger Trigger;
    [Min(0)] public float Weight = 100;
    public bool ForCartridges = true;
    public bool ForModules = true;
    public BulletRarity MinimumRarity = BulletRarity.Rare;
    public BulletRarity MaximumRarity = BulletRarity.Legendary;

    public bool IsAvailable(bool module, BulletRarity rarity)
    {
        return Trigger != AmmoTrigger.None
            && (module ? ForModules : ForCartridges)
            && rarity >= MinimumRarity && rarity <= MaximumRarity;
    }
}

[CreateAssetMenu(menuName = "Prototype/Ammo drop table", fileName = "AmmoDropTable")]
public sealed class AmmoDropTable : ScriptableObject
{
    public const string ResourcePath = "Drops/AmmoDropTable";
    [Tooltip("Starter tiers must also have AllowStarter enabled. Larger PowerRank means stronger.")]
    [Min(1)] public int StarterMaximumPowerRank = 1;
    [Tooltip("Cartridge types, their fixed intrinsic and art. Modules never roll a cartridge type.")]
    public List<AmmoArchetype> Archetypes = new List<AmmoArchetype>();
    [Tooltip("Relative rarity weights, property counts and optional trigger chances.")]
    public List<AmmoRarityDrop> Rarities = new List<AmmoRarityDrop>();
    [Tooltip("Choose a property by Weight, then an eligible tier by its own Weight. Zero disables an entry.")]
    public List<AmmoPropertyDrop> Properties = new List<AmmoPropertyDrop>();
    public List<AmmoTriggerDrop> Triggers = new List<AmmoTriggerDrop>();

    // Selection uses relative weights; disabled entries never occupy probability mass.
    public static T Pick<T>(IEnumerable<T> entries, Func<T, float> weight) where T : class
    {
        double total = 0;
        T last = null;
        foreach (T entry in entries)
        {
            if (entry == null)
            {
                continue;
            }
            float value = weight(entry);
            if (value > 0 && !float.IsInfinity(value))
            {
                total += value;
                last = entry;
            }
        }
        if (last == null)
        {
            return null;
        }
        double roll = UnityEngine.Random.value * total;
        foreach (T entry in entries)
        {
            if (entry == null)
            {
                continue;
            }
            float value = weight(entry);
            if (value <= 0 || float.IsNaN(value) || float.IsInfinity(value))
            {
                continue;
            }
            roll -= value;
            if (roll < 0)
            {
                return entry;
            }
        }
        return last;
    }
}
