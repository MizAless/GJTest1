using System;
using System.Collections.Generic;

namespace GameEconomy
{
    public enum ShopPurchaseCode
    {
        Unavailable, Success, RunEnded, InvalidOffer, AlreadyPurchased,
        SlotRequired, InvalidSlot, InsufficientCoins, InventoryFull, Busy
    }

    public readonly struct ShopPurchaseResult
    {
        public ShopPurchaseCode Code { get; }
        public bool Succeeded => Code == ShopPurchaseCode.Success;
        // Replaced cartridges are discarded; UI can show this snapshot in its confirmation.
        public AmmoItem ReplacedCartridge { get; }

        public ShopPurchaseResult(ShopPurchaseCode code, AmmoItem replacedCartridge = null)
        {
            Code = code;
            ReplacedCartridge = replacedCartridge;
        }
    }

    public sealed class ShopOffer
    {
        private readonly AmmoItem _cartridge;
        public Guid Id { get; } = Guid.NewGuid();
        public ShopProductKind Kind { get; }
        public int Price { get; }
        public ShardId Shard { get; }
        public int Quantity { get; }
        public bool Purchased { get; internal set; }
        // UI and crafting cannot change the stored offer through a preview reference.
        public AmmoItem Cartridge => CopyCartridge();

        internal ShopOffer(AmmoItem cartridge, int price)
        {
            _cartridge = UnityEngine.JsonUtility.FromJson<AmmoItem>(UnityEngine.JsonUtility.ToJson(cartridge));
            Kind = ShopProductKind.Cartridge;
            Price = price;
            Quantity = 1;
        }

        internal ShopOffer(ShardId shard, int quantity, int price)
        {
            Kind = ShopProductKind.Shard;
            Shard = shard;
            Quantity = quantity;
            Price = price;
        }

        internal AmmoItem CopyCartridge()
        {
            if (_cartridge == null)
            {
                return null;
            }
            // Archetypes include the intrinsic payload; copy that too to protect configuration assets.
            return UnityEngine.JsonUtility.FromJson<AmmoItem>(UnityEngine.JsonUtility.ToJson(_cartridge));
        }
    }

    public sealed class ShopVisit
    {
        public int CompletedWave { get; }
        public float RarityMultiplier { get; }
        public IReadOnlyList<ShopOffer> Offers { get; }

        internal ShopVisit(int completedWave, float rarityMultiplier, List<ShopOffer> offers)
        {
            CompletedWave = completedWave;
            RarityMultiplier = rarityMultiplier;
            Offers = offers.AsReadOnly();
        }
    }

    public interface IShopService
    {
        ShopVisit Open(int completedWave);
        ShopPurchaseResult TryPurchase(Guid offerId, int? cartridgeSlot = null);
    }
}
