using System;
using System.Linq;
using GameEconomy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EconomyFoundationValidation
{
    private static bool _done;
    private static double _started;
    private static bool _oldOptionsEnabled;
    private static EnterPlayModeOptions _oldOptions;

    public static void Run()
    {
        ValidateInventory();
        RunEconomySettings settings = Resources.Load<RunEconomySettings>(RunEconomySettings.ResourcePath);
        Require(settings != null, "Missing economy resource.");
        Require(settings.GetKindRule(ShardKind.Ice).AllowedProperties.Single() == AmmoPropertyKind.Chill,
            "Ice rule did not deserialize.");
        Require(settings.GetKindRule(ShardKind.Lava).AllowedProperties.Single() == AmmoPropertyKind.Burn,
            "Lava rule did not deserialize.");
        Require(settings.GetGradeRule(ShardGrade.T1).ExcludedWeakestTiers == 3, "Best shard restrictions missing.");
        AmmoDropTable table = Resources.Load<AmmoDropTable>(AmmoDropTable.ResourcePath);
        Require(table.Properties.SelectMany(property => property.Tiers).All(tier => tier.PowerRank > 0),
            "Every existing tier needs an explicit power rank.");
        Require(table.Properties[0].Tiers[0].Name == "T1" && table.Properties[0].Tiers[2].PowerRank == 3,
            "Tier names or strength migration changed.");
        EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
        _oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _oldOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        _started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void ValidateInventory()
    {
        RunInventory wallet = new RunInventory();
        ShardId shard = new ShardId(ShardKind.Chaos, ShardGrade.T1);
        int changes = 0;
        wallet.Changed += change => changes++;
        wallet.AddCoins(10);
        Require(!wallet.TrySpendCoins(11) && wallet.Coins == 10, "Insufficient spend changed coins.");
        Require(wallet.TrySpendCoins(10) && wallet.Coins == 0, "Exact spend failed.");
        wallet.AddShards(shard, 2);
        var snapshot = wallet.GetShardSnapshot();
        Require(!wallet.TrySpendShards(shard, 3), "Insufficient shard spend succeeded.");
        Require(wallet.TrySpendShards(shard, 2) && wallet.GetShardCount(shard) == 0, "Shard spend failed.");
        Require(snapshot[shard] == 2 && changes == 4, "Snapshot or change notifications are incorrect.");
        Expect<ArgumentOutOfRangeException>(() => wallet.AddCoins(-1));
        Expect<ArgumentOutOfRangeException>(() => wallet.TrySpendCoins(-1));
        Expect<ArgumentOutOfRangeException>(() => wallet.AddShards(shard, -1));
        Expect<ArgumentOutOfRangeException>(() => wallet.TrySpendShards(shard, -1));
        Expect<ArgumentException>(() => wallet.AddShards(default));
        wallet.AddCoins(int.MaxValue);
        Expect<OverflowException>(() => wallet.AddCoins(1));
        Require(wallet.Coins == int.MaxValue, "Overflow mutated inventory.");
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - _started < 120, "Economy foundation timed out.");
            if (EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
            }
            RunDirector run = RunDirector.Instance;
            if (_done || !Application.isPlaying || run == null || !run.CombatActive || G.Enemies.Count < 3)
            {
                return;
            }
            _done = true;
            RunEconomyContext scope = run.Economy;
            Require(scope.Inventory.Coins == 0 && scope.Inventory.GetShardSnapshot().Count == 0,
                "New run did not start empty.");
            Enemy first = G.Enemies.First();
            int count = 0;
            bool beforeDestruction = false;
            EnemyDeathInfo last = default;
            scope.EnemyDied += info =>
            {
                count++;
                last = info;
                beforeDestruction = first != null;
                first.TakeDamage(999, Color.white, Vector3.zero);
            };
            first.TakeDamage(999, Color.white, Vector3.zero, new DamageSource(DamageKind.Projectile, 123));
            first.TakeDamage(999, Color.white, Vector3.zero);
            Require(count == 1 && beforeDestruction && last.RunId == scope.RunId && last.Source.ActorId == 123,
                "Death must be once, before destruction, with source and run identity.");
            Require(last.MaximumHealth == 30 && last.Kind == EnemyKind.Weak && last.Wave == 1 && last.Group == 1,
                "Death snapshot metadata incorrect.");
            Enemy second = G.Enemies.First();
            CombatProfile profile = new CombatProfile { SourceActorId = 456 };
            second.AddDots("огонь", 1, 100, 2, profile);
            second.TickStatuses();
            Require(count == 2 && last.Source.Kind == DamageKind.Status && last.Source.ActorId == 456,
                "Status death did not retain source attribution.");
            Enemy remaining = G.Enemies.First();
            scope.Inventory.AddCoins(17);
            scope.Inventory.AddShards(new ShardId(ShardKind.Ice, ShardGrade.T2), 3);
            int ended = 0;
            scope.Ended += () => ended++;
            run.Initialize(UnityEngine.Object.FindAnyObjectByType<EnemySpawner>());
            Require(ended == 1 && !scope.IsActive && run.Economy.RunId != scope.RunId,
                "New run did not close the old scope.");
            Require(run.Economy.Inventory.Coins == 0 && run.Economy.Inventory.GetShardSnapshot().Count == 0,
                "New run leaked old inventory.");
            int newDeaths = 0;
            run.Economy.EnemyDied += info => newDeaths++;
            remaining.TakeDamage(999, Color.white, Vector3.zero);
            Require(newDeaths == 0 && count == 2, "Stale enemy death crossed run boundaries.");
            scope.Dispose();
            Require(ended == 1, "Scope ended more than once.");
            Debug.Log("ECONOMY_FOUNDATION_VALIDATION_PASSED: inventory/negative spending/overflow/snapshots, settings/ranks, once-only deaths, status source, reset and stale-run isolation");
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged += Exit;
            EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }

    private static void Exit(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }
        EditorSettings.enterPlayModeOptionsEnabled = _oldOptionsEnabled;
        EditorSettings.enterPlayModeOptions = _oldOptions;
        EditorApplication.Exit(0);
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
