# Coins: task 3 integration

`CoinCollectionService` implements the shared `ICoinCollectionService` contract.
It is NOT registered in Game/RunDirector yet. Task 6 owns registration and the
wave transition, including the final wave before victory.

```csharp
// Once per run, before the first enemy can die:
var coins = serviceRoot.AddComponent<GameEconomy.CoinCollectionService>();
coins.Initialize(runDirector.Economy); // loads Economy/CoinDropSettings

// After ALL groups AND all surviving enemies in a wave have finished:
// Integration must first set its combat phase inactive and let outstanding
// Shooter.IsFiring work finish (including status ticks/secondary deaths).
var stage = StaticShooterStage.Instance;
yield return coins.CollectAll(new GameEconomy.CoinCollectionRequest(
    runDirector.Economy, stage.Weapon.Muzzle, stage.CollectionTarget));
// Only now open rewards, or end the run with victory for the final wave.
```

The service temporarily disables the Shooter input component during collection,
then restores its previous enabled state if the run is still active. Public
Shooter.TryFire calls and already running combat coroutines remain owned by the
integration phase gate above. No RunDirector/UI changes are included here.

Settings: `Resources/Economy/CoinDropSettings.asset`, editable in Inspector.
Weak = 1 x 1, Medium = 2 x 1, Strong = 3 x 2, Boss = 8 x 5.
Count and face value are independent per type. Hover height, diameter, scatter,
chain time budget, maximum hop duration, toss time and flight time are editable.
Do not mutate the shared asset at runtime.

Each death creates pending coin instances, with zero wallet mutation. DeathId
is deduplicated independently of the shard listener. Coins do not expire or
react to group changes. They use a native faceted gold disc with a contrasting
rim, rotate about Y, bob above the XZ floor and cast a soft procedural shadow.
They have no Collider, Rigidbody, Bullet or combat layer interaction. No external
art or new asset license is needed. Mesh/material are shared by all coins.

CollectAll is an IEnumerator; callers must yield/advance it to start the operation.
A second concurrent invocation waits for the same collection; it never fires or
awards again. Empty or stale-run requests finish immediately. Nonempty valid
requests require live Muzzle and CollectionTarget transforms. Collection keeps
updating independently of a particular waiter's lifetime while the service is
enabled. Cancel is explicit and idempotent; Ended/disable/destruction unbinds the
run and removes unarrived coins without awarding them. Initialize replaces the
previous scope. A new run needs a new Initialize call.

The first target is the farthest from the muzzle, then every hop chooses the
farthest remaining coin from the impact point (full 3D distance). Ties preserve
spawn order. Every impact adds a visible corner to the trail. The hero aims,
plays revolver recoil/flash and one shot sound; this never calls Shooter.TryFire,
constructs CombatProfile, consumes a chamber or executes a status tick.

Each struck coin tosses upward then follows an accelerating arc toward the LIVE
hero chest transform. Flights overlap. Value is credited exactly once on arrival,
not on ricochet contact. PendingCount includes airborne coins and IsCollecting
stays true until every arrival. Hop duration is min(maximum hop, budget/count),
without a minimum per coin: large piles stay near 2.4 + 0.14 + 0.65 seconds of
scaled game time. Multiple hops in one frame retain all coins and trail corners.
Pausing timeScale freezes animation and the waiter; no reward is skipped.

Validation: `CoinCollectionValidation.Run` in Unity batchmode. Tests four kinds,
retention, totals, route, collision absence, concurrent/repeated calls, 500 coins,
no combat drum consumption, cancellation, run end and replacement. Captures are
saved in `.utmp/Artifacts/coins-idle.png` and `coins-collection.png`.

Denomination affects visual diameter and shadow: value 1 = base, 2 = +15%,
5 = about +35%. Growth is logarithmic and capped at +50% for large values.
This does not change scatter, collisions, flight timing or credited value.
