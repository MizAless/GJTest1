# Shard crafting — task 4

`GameEconomy.ShardCraftingService` implements `ICraftingService` from task 0.
No UI, service registration, RunDirector transitions, drops or shop are added.

```csharp
var crafting = new ShardCraftingService(runDirector.Economy,
    Resources.Load<AmmoDropTable>(AmmoDropTable.ResourcePath), shooter.Container);
var request = new CraftRequest(selectedItem, isModule, selectedShard);
CraftCheck check = crafting.CanApply(request); // pure: no RNG, spending or mutation
CraftResult result = crafting.TryApply(request); // rechecks the current state
// result.Code/Reason are for UI; on Success, Affix identifies kind/tier/rank/value.
```

Pass the active BulletContainer when crafting installed equipment. Uninstalled
items can use the two-argument constructor. Do not debit shards in the UI and do
not call Rebuild/Reload after this service: successful installed crafting already
refreshes profiles. Keep one service bound to one run; create another for a new
run. An ended scope returns RunEnded and cannot craft with its former wallet.
A request's IsModule is supplied by the caller; an installed item is checked
against the supplied container's cartridge/socket arrays.

## Balance and selection

The original AmmoDropTable is read directly, without modification or a second
property table. Property weights, cartridge/module eligibility, item rarity
bounds, tier weights and RollValue ranges remain authoritative. Starter-only
restrictions do not apply to crafting. Current rarity drop counts stay unchanged.

RunEconomySettings is the existing configuration for crafting limits and filters:

- Chaos: AllowAllProperties=true, all eligible properties.
- Ice: explicit AllowedProperties=[Chill].
- Lava: explicit AllowedProperties=[Burn].
- AllowAllProperties=false with an empty list allows NOTHING.
- T1 shard: exclude 3 weakest distinct PowerRank values, exponent 2.
- T2 shard: exclude 1 weakest distinct PowerRank value, exponent 1.
- T3 shard: exclude 0, exponent 0.
- Cost defaults to 1; each successful application adds exactly one property.

After ordinary eligibility filters (including rarity and positive finite weight),
rank values are sorted numerically. Exclusion is clamped to distinctRanks-1, so
one available rank is always retained. With eight ranks T1 leaves 4..8; with
1/2/3 ranks it leaves the strongest available rank. Disabled tiers are excluded
BEFORE calculating rank cutoffs. Tier names and their text/order are irrelevant.
An ordinary Common item remains restricted to Common-eligible tiers even with
T1 shards. ShardGrade.T1 means best SHARD, independently of the names of ammo tiers.

Remaining tier weight = originalWeight * PowerRank^PowerWeightExponent.
Selection normalizes these weights in log space to avoid overflow. Property
weights are chosen separately; a property with more tiers does not receive extra
property probability. Duplicate rows cannot cause duplicate affixes on an item.
Invalid/nonpositive value ranges and count ranges with no positive integer are
not candidates, so a successful craft always adds a nonzero property.

## Limits, intrinsics and transactions

Only fields on AmmoItem.Properties count as additional properties. An existing
positive field cannot be rolled again. Archetype.SelfProperties, intrinsic trigger
and intrinsic payload are untouched; intrinsic overlap with a new additional
property is allowed. Target.Properties is replaced with a modified copy, so
another item's accidentally shared Properties reference is not changed.

AmmoItem.Trigger is a separate additional trigger. This recipe never rolls,
replaces or removes it, and never copies the archetype's intrinsic trigger into
it. MaximumAdditionalTriggers limits this separate field, not the implicit one.
The current storage supports zero or one additional trigger.

Default craft limits are 2 additional properties + 1 additional trigger, matching
the previously discussed target. Existing rarity generation still produces its
original 1/2/3/4 properties. Items already at/above the craft cap fail with
AffixLimit; they keep every existing property. Raising the config limit permits
further crafting. No migration or silent trimming is performed.

CanApply checks target, run, module policy, caps, configuration, resources and
eligible pool. TryApply rechecks, rolls, copies one affix, refreshes installed
profiles and then commits the shard debit. Inventory observers see the new item
and profiles. Unavailable/failed checks spend nothing. As in task 0's inventory
contract, Changed listeners must not throw or perform reentrant spending; the
service also rejects reentrant crafting while its commit is in progress.

## Profile refresh without a reload

BulletContainer.RefreshProfiles preserves Used, IsReloading and any running
reload's timer. It reconstructs equipment and derived reload bonuses without
Reloaded, Shooted, Transferred, sound, wait, refill or status ticks. Runtime Grant
calls retain copies of their original shot payloads; refresh reapplies these
snapshots once, without re-emitting transfer events. Repeated refreshes do not
stack bonuses. Actual Rebuild/Reload still clears these temporary grants, refills
the drum and emits the original reload events. In-flight projectile snapshots
are unaffected by a later craft.

Validation: Unity batchmode `-executeMethod ShardCraftingValidation.Run`.
The checks use real resource assets plus disposable test clones: exact resource
costs, pure preflight, filters, caps, immutable intrinsics/shared data, 1/2/3/8
ranks, disabled/duplicate ranks, statistical weights, installed cartridges and
modules, no refill/reload/transfer/tick, transient grants, and ended scope.
