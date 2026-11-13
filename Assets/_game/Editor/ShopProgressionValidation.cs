using System;
using System.Linq;
using GameEconomy;
using UnityEditor;
using UnityEngine;

public static class ShopProgressionValidation
{
    private sealed class CountingRarity : IRarityMultiplierProvider
    {
        public int Calls;
        public float GetRarityMultiplier(int completedWave)
        {
            Calls++;
            return 0.4f;
        }
    }

    public static void Run()
    {
        UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
        GameObject owner = null;
        try
        {
            UnityEngine.Random.InitState(789012);
            AmmoDropTable table = Resources.Load<AmmoDropTable>(AmmoDropTable.ResourcePath);
            ShopSettings shopSettings = Resources.Load<ShopSettings>(ShopSettings.ResourcePath);
            RunEconomySettings economy = Resources.Load<RunEconomySettings>(RunEconomySettings.ResourcePath);
            Require(table != null && shopSettings != null && economy != null, "Missing shop resources.");
            Require(shopSettings.DropTable == table, "Shop does not reference the current drop table.");
            string tableBefore = JsonUtility.ToJson(table);
            string settingsBefore = JsonUtility.ToJson(shopSettings);
            string economyBefore = JsonUtility.ToJson(economy);
            ValidateWeights(table, economy);
            ValidateStarters(table);
            owner = new GameObject("Shop validation drum");
            BulletContainer drum = owner.AddComponent<BulletContainer>();
            drum.Init();
            ValidatePurchases(table, shopSettings, economy, drum);
            Require(tableBefore == JsonUtility.ToJson(table), "Generation mutated the shared drop table.");
            Require(settingsBefore == JsonUtility.ToJson(shopSettings), "Purchases mutated shop settings.");
            Require(economyBefore == JsonUtility.ToJson(economy), "Shop mutated economy settings.");
            Debug.Log("SHOP_PROGRESSION_VALIDATION_PASSED");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
            throw;
        }
        finally
        {
            UnityEngine.Random.state = previousRandom;
            if (owner != null)
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }

    private static void ValidateWeights(AmmoDropTable table, RunEconomySettings economy)
    {
        Require(Mathf.Approximately(economy.GetRarityMultiplier(1), 0.4f), "First shop multiplier.");
        Require(Mathf.Approximately(economy.GetRarityMultiplier(4), 1.5f), "Final shop multiplier.");
        foreach (float multiplier in new[] { 0f, 0.4f, 1f, 1.5f })
        {
            double total = table.Rarities.Sum(entry => (double)AmmoGenerator.GetRarityWeight(entry, false, multiplier));
            Require(total > 0, "Empty rarity distribution.");
            var counts = new int[4];
            const int sampleCount = 16000;
            for (int index = 0; index < sampleCount; index++)
            {
                AmmoRarityDrop rarity = AmmoDropTable.Pick(table.Rarities,
                    entry => AmmoGenerator.GetRarityWeight(entry, false, multiplier));
                counts[(int)rarity.Rarity]++;
            }
            foreach (AmmoRarityDrop entry in table.Rarities)
            {
                float expectedWeight = entry.Weight;
                if (entry.Rarity != BulletRarity.Common)
                {
                    expectedWeight *= multiplier;
                }
                Require(Mathf.Approximately(expectedWeight,
                    AmmoGenerator.GetRarityWeight(entry, false, multiplier)), "Wrong adjusted weight.");
                double probability = expectedWeight / total;
                double observed = counts[(int)entry.Rarity] / (double)sampleCount;
                Require(Math.Abs(probability - observed) < 0.018, "Rarity selection is not normalized.");
            }
        }
        for (int index = 0; index < 600; index++)
        {
            Require(AmmoGenerator.Generate(table, false, rarityMultiplier: 0).Rarity == BulletRarity.Common,
                "Generator ignored adjusted rarity weights.");
        }
    }

    private static void ValidateStarters(AmmoDropTable table)
    {
        Require(table.StarterMaximumPowerRank == 1, "Starter power limit did not deserialize.");
        Require(table.Archetypes.Where(entry => entry.VisualIndex >= 4).All(entry => !entry.AllowStarter),
            "Strong intrinsic in starter pool.");
        for (int index = 0; index < 400; index++)
        {
            AmmoItem starter = AmmoGenerator.Generate(table, false, true, 100);
            AmmoItem module = AmmoGenerator.Generate(table, true, true, 100);
            Require(starter.Rarity == BulletRarity.Common && module.Rarity == BulletRarity.Common,
                "Rarity multiplier affected starters.");
            Require(starter.Archetype.AllowStarter && starter.Trigger == AmmoTrigger.None,
                "Forbidden starter intrinsic or trigger.");
        }
        AmmoDropTable custom = ScriptableObject.CreateInstance<AmmoDropTable>();
        custom.Rarities.Add(new AmmoRarityDrop { Rarity = BulletRarity.Common, Weight = 1 });
        custom.Properties.Add(new AmmoPropertyDrop
        {
            Property = AmmoPropertyKind.Ricochet,
            Tiers = new System.Collections.Generic.List<AmmoPropertyTier>
            {
                new AmmoPropertyTier { PowerRank = 1, MinimumValue = 1, MaximumValue = 1 },
                new AmmoPropertyTier { PowerRank = 8, Weight = 100000, MinimumValue = 8, MaximumValue = 8 }
            }
        });
        try
        {
            for (int index = 0; index < 60; index++)
            {
                Require(AmmoGenerator.Generate(custom, false, true).Properties.Ricochet == 1,
                    "Starter cap ignored PowerRank.");
            }
            custom.StarterMaximumPowerRank = 8;
            Require(AmmoGenerator.Generate(custom, false, true).Properties.Ricochet == 8,
                "Starter cap is not configurable.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(custom);
        }
    }

    private static void ValidatePurchases(AmmoDropTable table, ShopSettings settings,
        RunEconomySettings economy, BulletContainer drum)
    {
        using (var run = new RunEconomyContext(economy))
        {
            var rarity = new CountingRarity();
            var shop = new ShopService(run, settings, drum, rarity);
            ShopVisit first = shop.Open(1);
            Require(ReferenceEquals(first, shop.Open(1)) && rarity.Calls == 1,
                "Reopening rerolled offers or recalculated multiplier.");
            ShopOffer ammo = first.Offers.First(offer => offer.Kind == ShopProductKind.Cartridge);
            ShopOffer shard = first.Offers.First(offer => offer.Kind == ShopProductKind.Shard);
            AmmoItem preview = ammo.Cartridge;
            preview.Properties.More = 99999;
            preview.Archetype.SelfProperties.More = 99999;
            Require(ammo.Cartridge.Properties.More != 99999 && ammo.Cartridge.Archetype.SelfProperties.More != 99999,
                "Preview can mutate stored stock.");
            AmmoItem oldCartridge = drum.Cartridges[2];
            int failedEvents = 0;
            Action<InventoryChange> failedListener = change => failedEvents++;
            run.Inventory.Changed += failedListener;
            Require(shop.TryPurchase(shard.Id).Code == ShopPurchaseCode.InsufficientCoins,
                "Insufficient shard payment accepted.");
            Require(shop.TryPurchase(ammo.Id).Code == ShopPurchaseCode.SlotRequired, "Slot selection missing.");
            Require(shop.TryPurchase(ammo.Id, -1).Code == ShopPurchaseCode.InvalidSlot, "Invalid slot accepted.");
            Require(shop.TryPurchase(ammo.Id, 2).Code == ShopPurchaseCode.InsufficientCoins, "Insufficient coins accepted.");
            Require(!ammo.Purchased && ReferenceEquals(drum.Cartridges[2], oldCartridge) && run.Inventory.Coins == 0,
                "Failed purchase mutated state.");
            run.Inventory.Changed -= failedListener;
            Require(failedEvents == 0 && !shard.Purchased && run.Inventory.GetShardCount(shard.Shard) == 0,
                "Failed purchases emitted events or granted a shard.");
            run.Inventory.AddCoins(1000);
            int before = run.Inventory.Coins;
            int notifications = 0;
            bool ammoEventConsistent = true;
            Action<InventoryChange> ammoListener = change =>
            {
                notifications++;
                ammoEventConsistent &= ammo.Purchased && !ReferenceEquals(oldCartridge, drum.Cartridges[2]);
                ammoEventConsistent &= shop.TryPurchase(ammo.Id, 2).Code == ShopPurchaseCode.Busy;
            };
            run.Inventory.Changed += ammoListener;
            ShopPurchaseResult bought = shop.TryPurchase(ammo.Id, 2);
            run.Inventory.Changed -= ammoListener;
            Require(bought.Succeeded && bought.ReplacedCartridge == oldCartridge
                && run.Inventory.Coins == before - ammo.Price && notifications == 1 && ammoEventConsistent, "Ammo payment not atomic.");
            Require(shop.TryPurchase(ammo.Id, 2).Code == ShopPurchaseCode.AlreadyPurchased, "Bought twice.");
            before = run.Inventory.Coins;
            int shardBefore = run.Inventory.GetShardCount(shard.Shard);
            bool shardEventConsistent = true;
            Action<InventoryChange> shardListener = change =>
            {
                shardEventConsistent &= shard.Purchased
                    && run.Inventory.GetShardCount(shard.Shard) == shardBefore + shard.Quantity
                    && run.Inventory.Coins == before - shard.Price;
            };
            run.Inventory.Changed += shardListener;
            Require(shop.TryPurchase(shard.Id).Succeeded, "Shard purchase failed.");
            run.Inventory.Changed -= shardListener;
            Require(shardEventConsistent, "Notification observed partial shard purchase.");
            ShopVisit second = shop.Open(2);
            Require(shop.TryPurchase(ammo.Id, 0).Code == ShopPurchaseCode.InvalidOffer, "Foreign shop offer accepted.");
            Require(ReferenceEquals(first, shop.Open(1)) && ammo.Purchased && shard.Purchased && rarity.Calls == 2,
                "Reopening lost purchase state.");
            run.Dispose();
            Require(shop.TryPurchase(first.Offers.Last().Id).Code == ShopPurchaseCode.RunEnded, "Ended run accepted.");
        }
        ShopSettings custom = ScriptableObject.CreateInstance<ShopSettings>();
        custom.DropTable = table;
        custom.Stock.Add(new ShopStockRule { Kind = ShopProductKind.Shard, ShardPrice = 0, ShardQuantity = 2 });
        try
        {
            using (var run = new RunEconomyContext(economy))
            {
                var shop = new ShopService(run, custom, drum);
                ShopOffer offer = shop.Open(1).Offers[0];
                run.Inventory.AddShards(offer.Shard, int.MaxValue);
                Require(shop.TryPurchase(offer.Id).Code == ShopPurchaseCode.InventoryFull && !offer.Purchased,
                    "Overflow changed purchase state.");
                run.Inventory.TrySpendShards(offer.Shard, int.MaxValue);
                Require(shop.TryPurchase(offer.Id).Succeeded && run.Inventory.Coins == 0
                    && run.Inventory.GetShardCount(offer.Shard) == 2, "Zero-price purchase failed.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(custom);
        }
    }

    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
