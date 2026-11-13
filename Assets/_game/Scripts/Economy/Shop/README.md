# Shop API ? task 1

Namespace `GameEconomy`. No shop UI or RunDirector stage transition is installed.
Keep **one ShopService per RunEconomyContext**, shared by all shop pages. The
integration task creates it after BulletContainer.Init. Do not create it in OnEnable.
The service checks run.IsActive before Open/purchase; no subscription needs disposal.
After run end discard the service and all visits. All calls run on Unity's main thread.

## Configuration

- `Resources/Economy/RunEconomySettings.asset`, `RarityMultipliers`: completed waves
  1..4 default 0.4 / 0.77 / 1.13 / 1.5. Wave 4's shop is before final wave 5.
- `Resources/Economy/ShopSettings.asset`: `DropTable`, `Stock`, `AmmoPrices`.
  Each ShopStockRule creates Count offers, inclusive FirstCompletedWave..
  LastCompletedWave. Kind=Cartridge generates independent AmmoItems. Kind=Shard
  uses Shard (kind + grade), ShardQuantity and ShardPrice. Count=0 disables a rule.
  Each price is integer coins, zero is allowed; negative values are errors.
- Initial stock: three cartridges; Chaos/Ice/Lava T3 each; Chaos T2 after wave 2,
  Chaos T1 after wave 3. Prices: Common 8, Rare 16, Epic 30, Legendary 50;
  shards 6/8/8/14/24. These are provisional balance values.
- `AmmoDropTable` retains all global weights. `AmmoGenerator.Generate(table,
  module, starter: false, rarityMultiplier: value)` multiplies only Rare/Epic/
  Legendary weights **at selection time**. Common stays unchanged. Pick rolls
  against their sum. For current 55/30/12/3 weights, 0.4 produces
  55/12/4.8/1.2 (Common probability 55/73, about 75.34%); 1.5 produces
  55/45/18/4.5 (Common about 44.90%). Ordinary generation keeps multiplier=1.
- Free between-wave rewards are unchanged. The future integration invokes the
  shop as an additional stage. No coefficient is applied to free rewards here.

## Starter pool

In `Resources/Drops/AmmoDropTable.asset`:

- `Archetypes[].AllowStarter`: independent admission of immutable intrinsic.
  Current starter pool: plain, needle, lava, ice. Ricochet (+2) and all support
  intrinsics are disabled for starters but remain available to rewards/shop.
- `Properties[].AllowStarter`, `Tiers[].AllowStarter` and
  `StarterMaximumPowerRank` (default 1) jointly restrict rolled starter affixes.
  PowerRank is explicit strength, independent of tier Name. Both cartridge and
  module use this rule. Rarity=Common, one affix, no rolled trigger remain unchanged.
- Tier rarity bounds and positive weights must also match. Properties with no
  surviving tier are excluded before selection. Empty valid pools fail explicitly.
  No silent fallback to a forbidden intrinsic or stronger tier.

## UI usage

```csharp
using System.Linq;
using GameEconomy;
using UnityEngine;

RunEconomyContext run = RunDirector.Instance.Economy;
var settings = Resources.Load<ShopSettings>(ShopSettings.ResourcePath);
IShopService shop = new ShopService(run, settings, drum);

// Store this service in the run coordinator, not inside a transient page.
ShopVisit visit = shop.Open(completedWave: 1);
foreach (ShopOffer offer in visit.Offers)
{
    // Draw price, Purchased and either Cartridge preview or Shard + Quantity.
}

ShopOffer cartridgeOffer = visit.Offers.First(offer => offer.Kind == ShopProductKind.Cartridge);
ShopOffer shardOffer = visit.Offers.First(offer => offer.Kind == ShopProductKind.Shard);

// Cartridge: first show target-slot selection and replacement confirmation.
// Cancelling either prompt simply does not call TryPurchase.
ShopPurchaseResult result = shop.TryPurchase(cartridgeOffer.Id, cartridgeSlot: 2);
// Shard: no slot necessary.
ShopPurchaseResult shardResult = shop.TryPurchase(shardOffer.Id);
```

`ShopVisit.CompletedWave`, `RarityMultiplier`, `Offers : IReadOnlyList<ShopOffer>`.
Repeated Open for the same wave returns the same visit, offer IDs, generated
stats, prices and sold flags; even after visiting another wave. It does not reroll
or re-read the coefficient. Only the currently opened visit can be purchased.
The coordinator owns which wave can currently be opened; the service does not
advance waves or constrain progression of the coordinator itself.

`ShopOffer`: Id (Guid), Kind, Price, Shard, Quantity, Purchased, Cartridge.
Cartridge is a detached preview copy, including its archetype: mutating it does
not change the stock or configuration. Store Id, not preview object identity.
Offers snapshot their contents at creation; changing balance later does not reroll
existing shops. One ID is one purchase, including free offers and shard bundles.

`TryPurchase(Guid offerId, int? cartridgeSlot = null) -> ShopPurchaseResult`:

- `Code`: Success / RunEnded / InvalidOffer / AlreadyPurchased / SlotRequired /
  InvalidSlot / InsufficientCoins / InventoryFull / Busy. Default Unavailable
  never means success. `Succeeded` is true only for Success.
- `ReplacedCartridge`: the old cartridge on success; old cartridge is discarded
  from the drum. Modules stay in their original sockets. UI should show its own
  confirmation before calling. No permanent backpack for cartridges is added.
- Rechecks slot, stock, currency and shard overflow immediately before committing.
  Failures change no wallet, slot, sold state or inventory events. No reservation
  or payment is made while picking a slot. Service never spends shards.
- Internal RunInventory.TryCommitShopPurchase is a small transaction boundary:
  coin debit, shard credit/slot assignment and sold flag commit before publishing
  Changed. Its delivery callback is internal and consists only of non-throwing
  assignments. Listener exceptions are logged after commit and cannot cancel the
  sale. Recursive calls to this shop while committing return Busy. General wallet
  contract still prohibits reentrant spending/mutation in Changed subscribers.
- On cartridge success BulletContainer.Rebuild refreshes profiles after commit,
  resetting the drum and emitting its existing reload/transfer events. Purchases
  belong between combat waves. A UI listener error is logged without reverting
  a completed purchase. Successful result returns after refresh.

The UI must **not** debit coins or install/add the item itself. It redraws balances
on Inventory.Changed and redraws stock/selected-slot state after TryPurchase.

Validation: `ShopProgressionValidation.Run` in Unity Editor/batchmode. Covers
normalized weighted sampling at four coefficients, generation, starter intrinsic
and PowerRank filters, immutable assets/previews, stable visits, missing/invalid
slots, insufficient money, one-time purchases, shard overflow, zero-price stock,
ended scopes, consistent notifications and a reentrant purchase attempt.
