# Economy contracts — shared base for tasks 1–4

Namespace: `GameEconomy`. Runtime code: `Scripts/Economy/`.
Settings: `Resources/Economy/RunEconomySettings.asset`.
This foundation does not spawn loot, roll craft affixes, spend shop money or
change rewards. Interfaces have no default fake implementation.

## Lifetime and access

`RunDirector.Economy : RunEconomyContext` exists after `Initialize(spawner)`.
Each initialization creates a new scope/RunId and empty RunInventory. R creates
another director. The old scope is disposed on replacement, defeat and scene
unload. `RunId : Guid`, `IsActive : bool`, `Ended : event Action` identify lifetime.
Victory currently retains the scope until unload, allowing future final collection.
Services must bind one scope, unsubscribe/cancel on Ended, and ignore stale callbacks.
All operations are on Unity's main thread. Configuration assets are read-only at runtime.

## Inventory

`IRunInventory` is implemented by `RunInventory`:

- `int Coins { get; }`
- `void AddCoins(int amount)` / `bool TrySpendCoins(int amount)`
- `int GetShardCount(ShardId shard)`
- `void AddShards(ShardId shard, int amount = 1)`
- `bool TrySpendShards(ShardId shard, int amount = 1)`
- `IReadOnlyDictionary<ShardId,int> GetShardSnapshot()` — detached read-only copy.
- `event Action<InventoryChange> Changed` — after successful mutations only.

`ShardId(ShardKind kind, ShardGrade grade)` is a value key.
Kinds: Chaos / Ice / Lava. Grades: T1 / T2 / T3; T1 is BEST.
Default ShardId is invalid. Invalid IDs and amounts <= 0 throw; insufficient
balance returns false without changes/events; overflow throws without mutation.
InventoryChange provides Currency (Coins/Shard), Shard, Delta and new Balance.
Read Shard only for Currency=Shard. Changed listeners must not throw or perform
reentrant spending. Mutation is committed before Changed is raised.
No global static wallet and no wallet data in a ScriptableObject.

```csharp
RunEconomyContext run = RunDirector.Instance.Economy;
IRunInventory wallet = run.Inventory;
wallet.AddCoins(5);
bool paid = wallet.TrySpendCoins(3);
var ice = new ShardId(ShardKind.Ice, ShardGrade.T2);
wallet.AddShards(ice);
```

## Enemy death — one owner, independent drop listeners

`run.EnemyDied : event Action<EnemyDeathInfo>` is published ONLY by Enemy.Die.
Enemy marks itself dead before notification; repeated/reentrant damage does not
publish again. Scope also deduplicates EnemyId. Object destruction or unloading
alone never publishes. Enemy binds the scope and spawn wave/group in Awake/Configure;
stragglers retain the group they spawned in. Current enemies are not pooled.
Event occurs before removal/VFX/destruction; data is a value snapshot:

`DeathId`, `RunId` (Guid); `EnemyId` (int); `Kind` (Weak/Medium/Strong/Boss);
`MaximumHealth`; `Position` (ground); `AimPosition` (body); `Wave`, `Group` (spawn);
`Source` (DamageSource: Kind, ActorId, Status).

DamageKind is Unknown / Projectile / Status / Area. ActorId=0 means unattributed.
`CombatProfile.SourceActorId` follows shot copies, branches and delayed statuses.
Aggregated DoT attribution uses the strongest charge in that status tick; this is
not a per-player damage ledger. Status overload/spread retains actor identity.
Direct TakeDamage calls without source remain valid and report Unknown.

Task 2 owns shard drop rolls/instances and awards shards ONLY on arrival.
Task 3 owns coin drop values/instances and awards coins ONLY on arrival.
Neither death publisher nor inventory awards a default drop. Each listener must
independently deduplicate DeathId; one listener must not consume another's event.
Listeners do not remove/kill enemies. Faults in one death listener are logged
without interrupting other listeners or enemy cleanup.

```csharp
run.EnemyDied += OnDeath; // service also unsubscribes/cancels on run.Ended
void OnDeath(EnemyDeathInfo death) { /* create pending loot, do not credit twice */ }
```

## Coin collection / task 3

`ICoinCollectionService`: `bool IsCollecting`,
`IEnumerator CollectAll(CoinCollectionRequest request)`, `void Cancel()`.
Request carries Run, Muzzle, CollectionTarget. CollectAll must finish only when
all pending coins have arrived/been awarded, or collection has been cancelled.
Empty collection completes immediately. Duplicate concurrent calls must not
start duplicate pickups. Cancel stops visuals; unarrived coins are not credited.
No combat shot, drum consumption, profile triggers or status ticks for this animation.
Integration owns when to call it and waits with `yield return`.

```csharp
var stage = StaticShooterStage.Instance;
yield return coins.CollectAll(new CoinCollectionRequest(run,
    stage.Weapon.Muzzle, stage.CollectionTarget));
```

CollectionTarget is a dedicated chest-area transform under the visual hero,
not the logical player's ground position. Task 2 may use this target too.

## Crafting / task 4

`ICraftingService.CanApply(CraftRequest) -> CraftCheck` (pure preflight).
`ICraftingService.TryApply(CraftRequest) -> CraftResult` (rechecks and commits).
Request: Target (AmmoItem), IsModule, Shard. Construct service with the shared
RunEconomyContext and AmmoDropTable. No service singleton or implementation yet.
CraftCheck has Code, Reason, CanApply. CraftResult has Code, Reason, Succeeded,
Affix (CraftedAffix: Property, TierName, PowerRank, Value).
Codes: Unavailable / Ready / Success / InvalidTarget / InsufficientShards /
AffixLimit / NoEligibleProperties / RunEnded. Default results are not success.
Only successful TryApply spends shards and changes the target. Intrinsics stay
on Archetype and are immutable; failed/preflight requests must not mutate anything.
Service handles transaction order; UI must not spend the shard separately.

`AmmoPropertyTier.PowerRank` is explicit strength WITHIN a property: larger=better.
Existing names, values and weights are retained (current T1/T2/T3 ranks 1/2/3).
Never infer rank from Name. ShardGrade ordering is separate and T1 is best.
Settings contain kind filters, grade rules, affix/trigger limits and module permission.
For the future implementation: exclude N distinct weakest ranks, clamped to keep
at least one available rank; then tier weight *= Pow(PowerRank, PowerWeightExponent).
Do not include a zero-weight tier. Kind AllowAllProperties=false means ONLY the
explicit AllowedProperties list (an empty list allows nothing).

Initial proposals: Chaos=all, Ice=Chill, Lava=Burn; T1 excludes 3 weakest ranks,
exponent 2; T2 excludes 1, exponent 1; T3 excludes 0, exponent 0. Cost=1 each.
MaximumAdditionalProperties=2, MaximumAdditionalTriggers=1; these are FUTURE craft
limits, not changes to current 1/2/3/4 generation. Above-limit existing items must
fail preflight, not lose affixes. Task 4 must implement profile refresh without
calling BulletContainer.Rebuild (that currently resets the drum/emits Reloaded).

## Rarity / task 1

`IRarityMultiplierProvider.GetRarityMultiplier(int completedWave)` is implemented
by RunEconomySettings. Initial array: 0.4 / 0.77 / 1.13 / 1.5 / 1.5 for completed
waves 1..5, index 0 also before wave 1; larger wave numbers clamp to last value.
Thus the last actual shop before the boss wave can reach 1.5. The curve is editable.
Task 1 applies this to NON-COMMON weights only, normalizes and rolls, without
mutating the shared drop table. Foundation does not connect it to AmmoGenerator.

## Parallel ownership

After this foundation commit, tasks 1–4 start from the SAME main commit.
Task 1: shop/rarity services, own shop settings; task 2: shards/visuals/drop settings;
task 3: coins/visuals/drop settings; task 4: craft service and non-reload profile refresh.
Use separate folders/settings/assets; do not rewrite the shared event/contracts.
Task 5 owns UI. Task 6 owns final registration, RunDirector stage transitions,
waiting for collection, victory/defeat and the complete run test.

Validation: `EconomyFoundationValidation.Run`; integration: `EnemyPressureValidation.Run`.

## Shop implementation — task 1

See [Shop/README.md](Shop/README.md) for configuration, exact results and UI examples.
`IShopService` / `ShopService(run, settings, drum, rarity = null)`:
`Open(int completedWave) -> ShopVisit`,
`TryPurchase(Guid offerId, int? cartridgeSlot = null) -> ShopPurchaseResult`.
One instance per run; fixed visits, purchases and immutable offer previews survive
page closes. The service checks context lifetime. `ShopSettings.ResourcePath` is
`Economy/ShopSettings`; its DropTable references the current AmmoDropTable.
`AmmoGenerator.Generate(table, module, starter = false, rarityMultiplier = 1)`
applies non-Common weights during the roll, never modifying the shared table.
RunInventory has a narrow internal TryCommitShopPurchase transaction for wallet,
delivery and sold flag before Changed. General public inventory API is unchanged.
Cartridge purchase refreshes the drum between waves; UI never separately pays or
installs items. Free rewards and RunDirector stage order remain unchanged.
