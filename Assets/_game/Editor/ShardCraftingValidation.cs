using System;
using System.Linq;
using GameEconomy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public static class ShardCraftingValidation
{
    private static RunEconomySettings _settings;
    private static RunEconomyContext _run;
    private static AmmoDropTable _table;
    private static ShardCraftingService _service;
    private static readonly ShardId Ice = new ShardId(ShardKind.Ice, ShardGrade.T1);
    private static readonly ShardId Chaos = new ShardId(ShardKind.Chaos, ShardGrade.T3);
    private static readonly ShardId Best = new ShardId(ShardKind.Chaos, ShardGrade.T1);

    public static void Run()
    {
        Random.State oldRandom = Random.state;
        try
        {
            AmmoDropTable sourceTable = Resources.Load<AmmoDropTable>(AmmoDropTable.ResourcePath);
            RunEconomySettings sourceSettings = Resources.Load<RunEconomySettings>(RunEconomySettings.ResourcePath);
            Require(sourceTable != null && sourceSettings != null, "Missing real economy resources.");
            string tableBefore = EditorJsonUtility.ToJson(sourceTable);
            string settingsBefore = EditorJsonUtility.ToJson(sourceSettings);
            _settings = Object.Instantiate(sourceSettings);
            _run = new RunEconomyContext(_settings);
            _table = Object.Instantiate(sourceTable);
            _service = new ShardCraftingService(_run, _table);
            _run.Inventory.AddShards(Ice, 10);
            _run.Inventory.AddShards(Chaos, 100000);
            _run.Inventory.AddShards(Best, 100000);
            Random.InitState(174921);
            ValidateRealTable();
            ValidateFailures();
            ValidateTierRules();
            ValidateWeights();
            ValidateProfiles(sourceTable);
            Require(EditorJsonUtility.ToJson(sourceTable) == tableBefore, "Source drop table was modified.");
            Require(EditorJsonUtility.ToJson(sourceSettings) == settingsBefore, "Source settings were modified.");
            _run.Dispose();
            Require(_service.CanApply(new CraftRequest(AmmoItem.Ordinary(), false, Chaos)).Code == CraftCode.RunEnded,
                "Ended run still allows crafting.");
            Debug.Log("SHARD_CRAFTING_VALIDATION_PASSED: filters; limits; pure preflight; exact costs; intrinsic isolation; explicit ranks 1/2/3/8; weighted rolls; no reload/tick; transient grants retained; real assets unchanged.");
            Random.state = oldRandom;
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Random.state = oldRandom;
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ValidateRealTable()
    {
        AmmoArchetype archetype = _table.Archetypes.First(entry => entry.IntrinsicTrigger != AmmoTrigger.None);
        AmmoItem item = new AmmoItem
        {
            Archetype = archetype,
            Rarity = BulletRarity.Legendary,
            Trigger = AmmoTrigger.ShotNext,
            Properties = new AmmoProperties { More = 15 }
        };
        AmmoProperties shared = item.Properties;
        AmmoItem other = new AmmoItem { Properties = shared };
        string intrinsic = JsonUtility.ToJson(archetype);
        var request = new CraftRequest(item, false, Ice);
        Random.State before = Random.state;
        Require(_service.CanApply(request).CanApply && _service.CanApply(request).CanApply, "Real Ice craft unavailable.");
        Require(Random.state.Equals(before) && ReferenceEquals(item.Properties, shared)
            && _run.Inventory.GetShardCount(Ice) == 10, "Preflight mutated item/wallet/RNG.");
        CraftResult result = _service.TryApply(request);
        Require(result.Succeeded && result.Affix.Property == AmmoPropertyKind.Chill
            && item.Properties.Chill > 0 && item.Properties.More == 15, "Ice did not add exactly cold.");
        Require(_run.Inventory.GetShardCount(Ice) == 9 && item.Trigger == AmmoTrigger.ShotNext,
            "Cost or separately stored trigger changed.");
        Require(JsonUtility.ToJson(archetype) == intrinsic && other.Properties.Chill == 0
            && ReferenceEquals(item.Archetype, archetype), "Intrinsic/shared properties were mutated.");
        Require(_service.TryApply(request).Code == CraftCode.AffixLimit && _run.Inventory.GetShardCount(Ice) == 9,
            "Craft above configured affix cap spent a shard.");
        _settings.MaximumAdditionalProperties = 3;
        Require(_service.CanApply(request).Code == CraftCode.NoEligibleProperties, "Duplicate Chill allowed.");
        _settings.MaximumAdditionalProperties = 2;
        var lava = new ShardId(ShardKind.Lava, ShardGrade.T3);
        _run.Inventory.AddShards(lava);
        AmmoItem module = AmmoItem.Ordinary();
        Require(_service.TryApply(new CraftRequest(module, true, lava)).Succeeded && module.Properties.Burn > 0,
            "Lava module craft did not use explicit fire list.");
        foreach (AmmoRarityDrop rarity in _table.Rarities)
        {
            int previousCount = rarity.PropertyCount;
            AmmoGenerator.Generate(_table, false);
            Require(rarity.PropertyCount == previousCount, "Craft limits changed rarity generation.");
        }
    }

    private static void ValidateFailures()
    {
        AmmoItem item = AmmoItem.Ordinary();
        Require(_service.CanApply(new CraftRequest(null, false, Chaos)).Code == CraftCode.InvalidTarget,
            "Null target allowed.");
        Require(_service.CanApply(new CraftRequest(item, false, default)).Code == CraftCode.InvalidTarget,
            "Invalid shard allowed.");
        _settings.AllowModuleCrafting = false;
        Require(_service.TryApply(new CraftRequest(item, true, Chaos)).Code == CraftCode.InvalidTarget,
            "Module policy ignored.");
        _settings.AllowModuleCrafting = true;
        _settings.MaximumAdditionalTriggers = 0;
        item.Trigger = AmmoTrigger.ShotNext;
        Require(_service.TryApply(new CraftRequest(item, false, Chaos)).Code == CraftCode.AffixLimit,
            "Additional trigger cap ignored.");
        item.Trigger = AmmoTrigger.None;
        _settings.MaximumAdditionalTriggers = 1;
        int countBefore = _run.Inventory.GetShardCount(Chaos);
        _settings.GetKindRule(ShardKind.Chaos).AllowAllProperties = false;
        Require(_service.TryApply(new CraftRequest(item, false, Chaos)).Code == CraftCode.NoEligibleProperties,
            "Empty explicit whitelist allowed properties.");
        Require(_run.Inventory.GetShardCount(Chaos) == countBefore && item.Properties.Chill == 0,
            "Unavailable craft changed wallet/item.");
        _settings.GetKindRule(ShardKind.Chaos).AllowAllProperties = true;
        var missing = new ShardId(ShardKind.Ice, ShardGrade.T2);
        Require(_service.TryApply(new CraftRequest(item, false, missing)).Code == CraftCode.InsufficientShards,
            "Missing resource accepted.");
        _table.Properties.ForEach(property => property.Weight = 0);
        Require(_service.TryApply(new CraftRequest(item, false, Chaos)).Code == CraftCode.NoEligibleProperties
            && _run.Inventory.GetShardCount(Chaos) == countBefore, "Zero weight pool spent a shard.");
    }

    private static AmmoPropertyDrop Property(AmmoPropertyKind kind, int tiers)
    {
        var property = new AmmoPropertyDrop { Property = kind, Weight = 1 };
        for (int rank = 1; rank <= tiers; rank++)
        {
            property.Tiers.Add(new AmmoPropertyTier
            {
                Name = "misleading-" + (10 - rank),
                PowerRank = rank,
                Weight = 1,
                MinimumValue = rank,
                MaximumValue = rank
            });
        }
        return property;
    }

    private static void ValidateTierRules()
    {
        _table.Properties.Clear();
        for (int count = 1; count <= 8; count++)
        {
            if (count > 3 && count < 8)
            {
                continue;
            }
            _table.Properties.Clear();
            _table.Properties.Add(Property(AmmoPropertyKind.Chill, count));
            for (int roll = 0; roll < 100; roll++)
            {
                AmmoItem item = AmmoItem.Ordinary();
                CraftResult result = _service.TryApply(new CraftRequest(item, false, Best));
                int minimum = Math.Min(4, count);
                Require(result.Succeeded && result.Affix.PowerRank >= minimum && result.Affix.PowerRank <= count,
                    "Explicit rank cutoff or small tier clamping failed.");
            }
        }
        AmmoPropertyDrop drop = Property(AmmoPropertyKind.Chill, 3);
        drop.Tiers[1].PowerRank = 1;
        drop.Tiers.Add(new AmmoPropertyTier { PowerRank = 100, Weight = 0, MinimumValue = 100, MaximumValue = 100 });
        _table.Properties.Clear();
        _table.Properties.Add(drop);
        var middle = new ShardId(ShardKind.Chaos, ShardGrade.T2);
        _run.Inventory.AddShards(middle, 100);
        for (int roll = 0; roll < 100; roll++)
        {
            CraftResult result = _service.TryApply(new CraftRequest(AmmoItem.Ordinary(), false, middle));
            Require(result.Succeeded && result.Affix.PowerRank == 3,
                "Distinct weak ranks or disabled strongest tier affected the pool.");
        }
        _table.Properties[0].Tiers.ForEach(tier => tier.MinimumRarity = BulletRarity.Epic);
        Require(_service.CanApply(new CraftRequest(AmmoItem.Ordinary(), false, Best)).Code == CraftCode.NoEligibleProperties,
            "Rarity eligibility ignored.");
    }

    private static void ValidateWeights()
    {
        _table.Properties.Clear();
        AmmoPropertyDrop cold = Property(AmmoPropertyKind.Chill, 1);
        AmmoPropertyDrop fire = Property(AmmoPropertyKind.Burn, 1);
        fire.Weight = 3;
        _table.Properties.Add(cold);
        _table.Properties.Add(fire);
        int fireCount = 0;
        const int samples = 12000;
        for (int roll = 0; roll < samples; roll++)
        {
            CraftResult result = _service.TryApply(new CraftRequest(AmmoItem.Ordinary(), false, Chaos));
            Require(result.Succeeded, "Weighted craft failed.");
            if (result.Affix.Property == AmmoPropertyKind.Burn)
            {
                fireCount++;
            }
        }
        Require(Math.Abs(fireCount / (double)samples - 0.75) < 0.025, "Property weights not respected.");
        _table.Properties.Clear();
        AmmoPropertyDrop drop = Property(AmmoPropertyKind.Chill, 2);
        drop.Tiers[0].PowerRank = 2;
        drop.Tiers[1].PowerRank = 4;
        _table.Properties.Add(drop);
        ShardGradeRule grade = _settings.GetGradeRule(ShardGrade.T1);
        grade.ExcludedWeakestTiers = 0;
        int strongCount = 0;
        for (int roll = 0; roll < samples; roll++)
        {
            CraftResult result = _service.TryApply(new CraftRequest(AmmoItem.Ordinary(), false, Best));
            if (result.Affix.PowerRank == 4)
            {
                strongCount++;
            }
        }
        Require(Math.Abs(strongCount / (double)samples - 0.8) < 0.025, "Power rank weighting not respected.");
        grade.ExcludedWeakestTiers = 3;
        Debug.Log("CRAFT_WEIGHT_SAMPLES: fire=" + fireCount + "/12000; strong=" + strongCount + "/12000");
    }

    private static void ValidateProfiles(AmmoDropTable realTable)
    {
        GameObject root = new GameObject("Craft profile validation");
        BulletContainer drum = root.AddComponent<BulletContainer>();
        drum.Init();
        for (int slot = 0; slot < drum.MaxBullets; slot++)
        {
            drum.Cartridges[slot] = AmmoItem.Ordinary();
            drum.Sockets[slot] = null;
        }
        AmmoItem item = drum.Cartridges[0];
        item.Archetype = realTable.Archetypes.First(entry => entry.IntrinsicTrigger == AmmoTrigger.ReloadNext);
        item.Properties.More = 20;
        item.Trigger = AmmoTrigger.ReloadNext;
        AmmoItem module = new AmmoItem { Properties = new AmmoProperties { Pierce = 1 } };
        drum.Sockets[0] = module;
        drum.Rebuild();
        GameObject enemyRoot = new GameObject("Craft status sentinel", typeof(SpriteRenderer), typeof(Enemy));
        Enemy sentinel = enemyRoot.GetComponent<Enemy>();
        // Explicit setup is safe in edit mode, where Awake is not guaranteed to execute.
        typeof(Enemy).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(sentinel, null);
        sentinel.AddDots("\u043a\u0440\u043e\u0432\u044c", 1, 2, 4);
        G.Enemies.Add(sentinel);
        float healthBefore = sentinel.Health;
        drum.BeginShot(out _);
        drum.BeginShot(out _);
        drum.BeginShot(out _);
        var pastShot = new AmmoItem { Properties = new AmmoProperties { More = 50, Burn = 2 } };
        drum.Grant(4, 0, pastShot);
        pastShot.Properties.More = 900;
        int reloadEvents = 0;
        int transferEvents = 0;
        int shotEvents = 0;
        drum.Reloaded += () => reloadEvents++;
        drum.Transferred += (source, target) => transferEvents++;
        drum.Shooted += () => shotEvents++;
        var service = new ShardCraftingService(_run, realTable, drum);
        float beforeAll = drum.Profiles[0].All;
        float nextAll = drum.Profiles[1].All;
        CraftResult result = service.TryApply(new CraftRequest(item, false, Ice));
        Require(result.Succeeded && drum.Profiles[0].Properties.Chill == item.Properties.Chill
            && drum.Profiles[1].Properties.Chill == item.Properties.Chill, "Installed craft did not refresh self/reload recipient.");
        Require(drum.Used == 3 && drum.BulletsRemaining == 3 && !drum.IsReloading,
            "Refresh refilled drum or changed reload state.");
        Require(Mathf.Approximately(drum.Profiles[0].All, beforeAll) && drum.Profiles[0].Properties.Burn == 2
            && Mathf.Approximately(drum.Profiles[1].All, nextAll), "Refresh lost or duplicated prior grants.");
        int cold = drum.Profiles[1].Properties.Chill;
        drum.RefreshProfiles();
        drum.RefreshProfiles();
        Require(drum.Profiles[1].Properties.Chill == cold && Mathf.Approximately(drum.Profiles[0].All, beforeAll),
            "Repeated refresh accumulated bonuses.");
        Require(service.TryApply(new CraftRequest(module, true, Ice)).Succeeded
            && drum.Profiles[0].Properties.Chill > item.Properties.Chill, "Installed module profile was not updated.");
        Require(reloadEvents == 0 && transferEvents == 0 && shotEvents == 0 && drum.Used == 3,
            "Craft emitted reload/shot/transfer presentation.");
        drum.Rebuild();
        Require(drum.Used == 0 && reloadEvents == 1 && drum.Profiles[0].Properties.Burn == 0,
            "Real reload no longer clears transient grants/refills drum.");
        Require(sentinel.Health == healthBefore && sentinel.Count("\u043a\u0440\u043e\u0432\u044c") == 1,
            "Craft or profile refresh advanced an enemy status.");
        Object.DestroyImmediate(enemyRoot);
        Object.DestroyImmediate(root);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
