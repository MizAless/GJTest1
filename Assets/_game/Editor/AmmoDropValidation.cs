using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AmmoDropValidation
{
    public static void Run()
    {
        UnityEngine.Random.State originalState = UnityEngine.Random.state;
        AmmoDropTable custom = ScriptableObject.CreateInstance<AmmoDropTable>();
        try
        {
            UnityEngine.Random.InitState(51247);
            AmmoDropTable actual = Resources.Load<AmmoDropTable>(AmmoDropTable.ResourcePath);
            Require(actual != null && actual.Properties.Count == 16 && actual.Triggers.Count == 10,
                "Default table must contain every existing property and trigger.");
            for (int index = 0; index < 500; index++)
            {
                foreach (bool module in new[] { false, true })
                {
                    AmmoItem starter = AmmoGenerator.Generate(actual, module, true);
                    Require(starter.Rarity == BulletRarity.Common && starter.Trigger == AmmoTrigger.None,
                        "Starter rarity or trigger changed.");
                    Require(starter.Properties.Description().Split('\n').Length == 1,
                        "Starter must have exactly one standalone property.");
                    AmmoItem reward = AmmoGenerator.Generate(actual, module);
                    Require(reward.Properties.Description().Split('\n').Length == (int)reward.Rarity + 1,
                        "Default reward property count does not match rarity.");
                }
            }
            custom.Rarities.Add(new AmmoRarityDrop
            {
                Rarity = BulletRarity.Common, Weight = 1, PropertyCount = 1, TriggerChance = 1
            });
            custom.Triggers.Add(new AmmoTriggerDrop
            {
                Trigger = AmmoTrigger.ShotNext, Weight = 1, MinimumRarity = BulletRarity.Common
            });
            AmmoPropertyDrop ricochet = Property(AmmoPropertyKind.Ricochet, 1);
            ricochet.Tiers.Add(Tier(100, 1));
            ricochet.Tiers.Add(Tier(300, 2));
            ricochet.Tiers.Add(Tier(0, 99));
            AmmoPropertyDrop pierce = Property(AmmoPropertyKind.Pierce, 3);
            pierce.Tiers.Add(Tier(1, 5));
            AmmoPropertyDrop disabled = Property(AmmoPropertyKind.Burn, 0);
            disabled.Tiers.Add(Tier(1, 1));
            custom.Properties.AddRange(new[] { ricochet, pierce, disabled });
            int ricochetCount = 0;
            int secondTierCount = 0;
            const int samples = 20000;
            for (int index = 0; index < samples; index++)
            {
                AmmoItem item = AmmoGenerator.Generate(custom, false);
                Require(item.Trigger == AmmoTrigger.ShotNext && item.Properties.Burn == 0,
                    "Trigger weight or disabled property ignored.");
                if (item.Properties.Ricochet > 0)
                {
                    ricochetCount++;
                    Require(item.Properties.Ricochet <= 2, "Zero-weight tier selected.");
                    if (item.Properties.Ricochet == 2)
                    {
                        secondTierCount++;
                    }
                }
                else
                {
                    Require(item.Properties.Pierce == 5, "Unexpected property value.");
                }
            }
            Require(Mathf.Abs((float)ricochetCount / samples - 0.25f) < 0.02f,
                "Property distribution does not follow 1:3 weights.");
            Require(Mathf.Abs((float)secondTierCount / ricochetCount - 0.75f) < 0.03f,
                "Tier distribution does not follow 1:3 weights.");
            pierce.ForModules = false;
            ricochet.Tiers[0].Weight = 0;
            ricochet.Tiers[1].MinimumValue = 3;
            ricochet.Tiers[1].MaximumValue = 5;
            HashSet<int> values = new HashSet<int>();
            custom.Rarities[0].PropertyCount = 4;
            for (int index = 0; index < 100; index++)
            {
                AmmoItem item = AmmoGenerator.Generate(custom, true);
                Require(item.Properties.Pierce == 0 && item.Properties.Ricochet >= 3
                    && item.Properties.Ricochet <= 5, "Module filter or integer range ignored.");
                values.Add(item.Properties.Ricochet);
            }
            Require(values.Count == 3, "Integer range must include both endpoints.");
            ricochet.Weight = 0;
            bool rejected = false;
            try
            {
                AmmoGenerator.Generate(custom, true);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Require(rejected, "Empty pool must report a configuration error.");
            Debug.Log("AMMO_DROP_VALIDATION_PASSED: default rewards, starters, 20000 weighted rolls, tier weights, zero weights, module filters, ranges and exhausted pools");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
        finally
        {
            UnityEngine.Random.state = originalState;
            UnityEngine.Object.DestroyImmediate(custom);
        }
    }

    private static AmmoPropertyDrop Property(AmmoPropertyKind kind, float weight)
    {
        return new AmmoPropertyDrop { Property = kind, Weight = weight };
    }

    private static AmmoPropertyTier Tier(float weight, float value)
    {
        return new AmmoPropertyTier { Weight = weight, MinimumValue = value, MaximumValue = value };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
