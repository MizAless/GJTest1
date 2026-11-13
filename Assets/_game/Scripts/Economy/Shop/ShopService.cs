using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameEconomy
{
    // One instance per run, kept alive across shop page opens. Unity main thread only.
    public sealed class ShopService : IShopService
    {
        private readonly RunEconomyContext _run;
        private readonly ShopSettings _settings;
        private readonly BulletContainer _drum;
        private readonly IRarityMultiplierProvider _rarity;
        private readonly Dictionary<int, ShopVisit> _visits = new Dictionary<int, ShopVisit>();
        private ShopVisit _current;
        private bool _purchasing;
        public ShopVisit Current => _current;

        public ShopService(RunEconomyContext run, ShopSettings settings, BulletContainer drum,
            IRarityMultiplierProvider rarity = null)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            _drum = drum != null ? drum : throw new ArgumentNullException(nameof(drum));
            _rarity = rarity ?? run.Settings;
        }

        public ShopVisit Open(int completedWave)
        {
            if (!_run.IsActive)
            {
                throw new InvalidOperationException("The shop's run has ended.");
            }
            if (_purchasing)
            {
                throw new InvalidOperationException("Cannot change shops during a purchase.");
            }
            if (completedWave < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(completedWave));
            }
            if (!_visits.TryGetValue(completedWave, out ShopVisit visit))
            {
                visit = GenerateVisit(completedWave);
                _visits.Add(completedWave, visit);
            }
            _current = visit;
            return visit;
        }

        private ShopVisit GenerateVisit(int completedWave)
        {
            if (_settings.DropTable == null)
            {
                throw new InvalidOperationException("Shop settings require an AmmoDropTable.");
            }
            float multiplier = _rarity.GetRarityMultiplier(completedWave);
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0)
            {
                throw new InvalidOperationException("Invalid shop rarity multiplier.");
            }
            var offers = new List<ShopOffer>();
            foreach (ShopStockRule rule in _settings.Stock)
            {
                if (rule == null || completedWave < rule.FirstCompletedWave || completedWave > rule.LastCompletedWave)
                {
                    continue;
                }
                ValidateRule(rule);
                for (int index = 0; index < rule.Count; index++)
                {
                    if (rule.Kind == ShopProductKind.Cartridge)
                    {
                        AmmoItem item = AmmoGenerator.Generate(_settings.DropTable, false,
                            rarityMultiplier: multiplier);
                        offers.Add(new ShopOffer(item, _settings.GetAmmoPrice(item.Rarity)));
                    }
                    else
                    {
                        offers.Add(new ShopOffer(rule.Shard, rule.ShardQuantity, rule.ShardPrice));
                    }
                }
            }
            return new ShopVisit(completedWave, multiplier, offers);
        }

        private static void ValidateRule(ShopStockRule rule)
        {
            if (rule.Count < 0 || !Enum.IsDefined(typeof(ShopProductKind), rule.Kind))
            {
                throw new InvalidOperationException("Invalid shop stock rule.");
            }
            if (rule.Kind == ShopProductKind.Shard
                && (!rule.Shard.IsValid || rule.ShardQuantity <= 0 || rule.ShardPrice < 0))
            {
                throw new InvalidOperationException("Invalid shard stock rule.");
            }
        }

        public ShopPurchaseResult TryPurchase(Guid offerId, int? cartridgeSlot = null)
        {
            if (!_run.IsActive)
            {
                return new ShopPurchaseResult(ShopPurchaseCode.RunEnded);
            }
            if (_purchasing)
            {
                return new ShopPurchaseResult(ShopPurchaseCode.Busy);
            }
            ShopOffer offer = FindCurrentOffer(offerId);
            if (offer == null)
            {
                return new ShopPurchaseResult(ShopPurchaseCode.InvalidOffer);
            }
            if (offer.Purchased)
            {
                return new ShopPurchaseResult(ShopPurchaseCode.AlreadyPurchased);
            }
            if (offer.Kind == ShopProductKind.Cartridge)
            {
                if (!cartridgeSlot.HasValue)
                {
                    return new ShopPurchaseResult(ShopPurchaseCode.SlotRequired);
                }
                if (_drum == null || !_drum.Initialized || cartridgeSlot.Value < 0
                    || cartridgeSlot.Value >= _drum.Cartridges.Length)
                {
                    return new ShopPurchaseResult(ShopPurchaseCode.InvalidSlot);
                }
            }
            if (_run.Inventory.Coins < offer.Price)
            {
                return new ShopPurchaseResult(ShopPurchaseCode.InsufficientCoins);
            }
            if (offer.Kind == ShopProductKind.Shard
                && _run.Inventory.GetShardCount(offer.Shard) > int.MaxValue - offer.Quantity)
            {
                return new ShopPurchaseResult(ShopPurchaseCode.InventoryFull);
            }
            return CommitPurchase(offer, cartridgeSlot);
        }

        private ShopOffer FindCurrentOffer(Guid id)
        {
            if (_current == null)
            {
                return null;
            }
            foreach (ShopOffer offer in _current.Offers)
            {
                if (offer.Id == id)
                {
                    return offer;
                }
            }
            return null;
        }

        private ShopPurchaseResult CommitPurchase(ShopOffer offer, int? slot)
        {
            AmmoItem delivery = offer.CopyCartridge();
            AmmoItem replaced = delivery != null ? _drum.Cartridges[slot.Value] : null;
            _purchasing = true;
            try
            {
                // Commit wallet, delivery and sold flag before publishing any inventory event.
                bool paid = _run.Inventory.TryCommitShopPurchase(offer.Price, offer.Shard,
                    offer.Kind == ShopProductKind.Shard ? offer.Quantity : 0, () =>
                    {
                        if (delivery != null)
                        {
                            _drum.Cartridges[slot.Value] = delivery;
                        }
                        offer.Purchased = true;
                    });
                if (!paid)
                {
                    return new ShopPurchaseResult(ShopPurchaseCode.InsufficientCoins);
                }
                if (delivery != null)
                {
                    // Rebuild derived profiles after the transaction. UI callbacks cannot undo payment.
                    try
                    {
                        _drum.Rebuild();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
                return new ShopPurchaseResult(ShopPurchaseCode.Success, replaced);
            }
            finally
            {
                _purchasing = false;
            }
        }
    }
}
