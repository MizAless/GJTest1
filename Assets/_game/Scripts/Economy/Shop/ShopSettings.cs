using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameEconomy
{
    public enum ShopProductKind { Cartridge, Shard }

    [Serializable]
    public sealed class ShopStockRule
    {
        public ShopProductKind Kind;
        [Min(0)] public int Count = 1;
        [Min(1)] public int FirstCompletedWave = 1;
        [Min(1)] public int LastCompletedWave = 4;
        public ShardId Shard = new ShardId(ShardKind.Chaos, ShardGrade.T3);
        [Min(1)] public int ShardQuantity = 1;
        [Min(0)] public int ShardPrice = 6;
    }

    [Serializable]
    public sealed class ShopAmmoPrice
    {
        public BulletRarity Rarity;
        [Min(0)] public int Coins = 10;
    }

    [CreateAssetMenu(menuName = "Prototype/Shop settings")]
    public sealed class ShopSettings : ScriptableObject
    {
        public const string ResourcePath = "Economy/ShopSettings";
        public AmmoDropTable DropTable;
        public List<ShopStockRule> Stock = new List<ShopStockRule>();
        public List<ShopAmmoPrice> AmmoPrices = new List<ShopAmmoPrice>();

        public int GetAmmoPrice(BulletRarity rarity)
        {
            ShopAmmoPrice price = AmmoPrices.Find(entry => entry != null && entry.Rarity == rarity);
            if (price == null || price.Coins < 0)
            {
                throw new InvalidOperationException("Missing or negative shop price: " + rarity);
            }
            return price.Coins;
        }
    }
}
