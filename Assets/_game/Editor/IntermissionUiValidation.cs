using System;
using System.IO;
using System.Linq;
using GameEconomy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class IntermissionUiValidation
{
    private static int _phase;
    private static double _started;
    private static float _phaseTime;
    private static bool _oldOptionsEnabled;
    private static EnterPlayModeOptions _oldOptions;
    private static string _artifacts;
    private static BandolierUpgradeScreen _screen;
    private static Guid _shopOffer;
    private static double _nextLog;
    private static int _exitCode;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/_game/Scenes/Game.unity");
        _oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _oldOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        _phase = 0;
        _artifacts = ".utmp/Artifacts/intermission-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        _started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - _started < 180, "Intermission timeout, phase " + _phase);
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
            }
            if (EditorApplication.timeSinceStartup > _nextLog)
            {
                _nextLog = EditorApplication.timeSinceStartup + 10;
                Debug.Log("INTERMISSION_PROGRESS phase=" + _phase + " playing=" + Application.isPlaying
                    + " director=" + (RunDirector.Instance != null) + " ui=" + (RunUI.Instance != null));
            }
            if (!Application.isPlaying || RunDirector.Instance == null || RunUI.Instance == null)
            {
                return;
            }
            RunDirector run = RunDirector.Instance;
            if (run.Shooter == null)
            {
                return;
            }
            _screen = RunUI.Instance.UpgradeScreen;
            if (_phase == 0)
            {
                if (!run.AwaitingReward)
                {
                    Time.timeScale = 8;
                    foreach (Enemy enemy in G.Enemies.ToArray())
                    {
                        enemy.TakeDamage(99999, Color.white, Vector3.zero);
                    }
                    return;
                }
                Require(!run.ContinueAfterRewards(), "Skipped mandatory rewards.");
                Require(!FindButton("Next wave").interactable, "Next button enabled before rewards.");
                run.Economy.Inventory.AddCoins(200);
                run.Economy.Inventory.AddShards(new ShardId(ShardKind.Lava, ShardGrade.T3), 3);
                run.Economy.Inventory.AddShards(new ShardId(ShardKind.Chaos, ShardGrade.T3), 3);
                _phaseTime = Time.unscaledTime;
                _phase = 1;
            }
            else if (_phase == 1 && Time.unscaledTime - _phaseTime > 1.7f)
            {
                Require(_screen.Presentation.Bandolier.Progress > 0.99f, "Paused bandolier did not unroll.");
                Capture("rewards");
                ValidateCrafting(run);
                _phaseTime = Time.unscaledTime;
                _phase = 2;
            }
            else if (_phase == 2 && Time.unscaledTime - _phaseTime > 0.2f)
            {
                Capture("craft-success");
                ValidateShop(run);
                Capture("shop", 1024, 768);
                ValidateRewardsAndSwaps(run);
                _phaseTime = Time.unscaledTime;
                _phase = 3;
            }
            else if (_phase == 3 && Time.unscaledTime - _phaseTime > 0.2f)
            {
                Capture("ready-for-next-wave");
                Click("Next wave");
                Require(!run.AwaitingReward && !_screen.IsOpen && Time.timeScale == 1, "Explicit continuation failed.");
                Require(!run.SwapRewardCartridges(0, 1), "Swap allowed after screen closed.");
                _phase = 4;
            }
            else if (_phase == 4 && run.Round == 2 && run.CombatActive)
            {
                Debug.Log("INTERMISSION_UI_VALIDATION_PASSED: real craft/shop APIs, currencies, errors, cancel/confirm, fixed stock, tooltips, swaps, mandatory rewards and explicit continuation");
                Finish(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static void ValidateCrafting(RunDirector run)
    {
        Click("Page Craft");
        AmmoDropTable table = Resources.Load<AmmoDropTable>(AmmoDropTable.ResourcePath);
        AmmoArchetype needle = table.Archetypes.Find(entry => entry.VisualIndex == 1);
        var target = new AmmoItem { Name = needle.Name, Archetype = needle, Rarity = BulletRarity.Common };
        run.Drum.Cartridges[1] = target;
        run.Drum.Sockets[1] = null;
        run.Drum.RefreshProfiles();
        Require(run.Drum.BeginShot(out _), "Could not prepare a spent chamber.");
        int used = run.Drum.Used;
        int reloads = 0;
        Action onReload = () => reloads++;
        run.Drum.Reloaded += onReload;
        string intrinsic = JsonUtility.ToJson(needle);
        Click("Craft shard Lava T3");
        Click("Upgrade slot 2");
        Require(FindButton("Apply shard").interactable, "Applicable craft disabled.");
        int shards = run.Economy.Inventory.GetShardCount(new ShardId(ShardKind.Lava, ShardGrade.T3));
        Click("Apply shard");
        Require(target.Properties.Burn > 0 && run.Drum.Profiles[1].Properties.Burn > 0,
            "UI did not apply craft or refresh profile.");
        Require(run.Economy.Inventory.GetShardCount(new ShardId(ShardKind.Lava, ShardGrade.T3)) == shards - 1,
            "Craft UI charged the wrong amount.");
        Require(run.Drum.Used == used && reloads == 0 && intrinsic == JsonUtility.ToJson(needle),
            "Craft refilled drum, reloaded or changed intrinsic.");
        Require(!FindButton("Apply shard").interactable, "Duplicate-only elemental pool should be disabled.");
        Click("Apply shard");
        Require(run.Economy.Inventory.GetShardCount(new ShardId(ShardKind.Lava, ShardGrade.T3)) == shards - 1,
            "Rejected craft consumed resource.");
        TooltipContent tooltip = AmmoTooltipContent.Create(target);
        Require(tooltip.Intrinsic == needle.IntrinsicDescription && tooltip.Properties.Any(value => value.Contains("1")),
            "Crafted tooltip did not retain separate intrinsic.");
        Click("Craft module mode");
        Require(!FindButton("Apply shard").interactable, "Empty module slot craft enabled.");
        var module = new AmmoItem { Name = "Validation module", Rarity = BulletRarity.Common };
        run.Drum.Sockets[1] = module;
        run.Drum.RefreshProfiles();
        Click("Craft shard Chaos T3");
        Click("Upgrade slot 2");
        Click("Apply shard");
        Require(module.Properties.Description() != new AmmoProperties().Description(), "Module craft not connected.");
        Require(run.Drum.Used == used && reloads == 0, "Module craft reloaded drum.");
        GameObject.Find("Upgrade slot 2").GetComponent<UpgradeSlotHover>()
            .OnPointerEnter(new PointerEventData(EventSystem.current));
        Require(_screen.Tooltips.HasModule, "Missing stacked cartridge and module tooltips.");
        _screen.Tooltips.HideAll();
        run.Drum.Reloaded -= onReload;
    }

    private static void ValidateShop(RunDirector run)
    {
        Click("Page Shop");
        ShopVisit visit = _screen.ShopPage.Visit;
        ShopOffer ammo = visit.Offers.First(offer => offer.Kind == ShopProductKind.Cartridge);
        _shopOffer = ammo.Id;
        int coins = run.Economy.Inventory.Coins;
        AmmoItem old = run.Drum.Cartridges[2];
        AmmoItem module = run.Drum.Sockets[2];
        Click("Shop product 0");
        Click("Upgrade slot 3");
        Require(_screen.AwaitingConfirmation && run.Economy.Inventory.Coins == coins
            && ReferenceEquals(old, run.Drum.Cartridges[2]), "Purchase happened before confirmation.");
        Capture("shop-confirmation");
        Click("Cancel replacement");
        Require(!ammo.Purchased && run.Economy.Inventory.Coins == coins, "Cancellation charged money.");
        Click("Shop product 0");
        Click("Upgrade slot 3");
        Click("Confirm replacement");
        Require(ammo.Purchased && run.Economy.Inventory.Coins == coins - ammo.Price
            && !ReferenceEquals(old, run.Drum.Cartridges[2]) && ReferenceEquals(module, run.Drum.Sockets[2]),
            "Shop confirmation did not atomically install cartridge.");
        _screen.ShopPage.Confirm();
        Require(run.Economy.Inventory.Coins == coins - ammo.Price, "Repeated confirmation charged twice.");
        Click("Page Craft");
        Click("Page Shop");
        Require(ReferenceEquals(visit, _screen.ShopPage.Visit) && visit.Offers[0].Id == _shopOffer && ammo.Purchased,
            "Page navigation rerolled shop or lost sold state.");
        int shardIndex = Enumerable.Range(0, visit.Offers.Count).First(index => visit.Offers[index].Kind == ShopProductKind.Shard);
        ShopOffer shard = visit.Offers[shardIndex];
        int shards = run.Economy.Inventory.GetShardCount(shard.Shard);
        run.Economy.Inventory.TrySpendCoins(run.Economy.Inventory.Coins);
        Click("Shop product " + shardIndex);
        Require(!shard.Purchased && run.Economy.Inventory.GetShardCount(shard.Shard) == shards,
            "Insufficient payment granted shard.");
        run.Economy.Inventory.AddCoins(200);
        Click("Shop product " + shardIndex);
        Require(shard.Purchased && run.Economy.Inventory.Coins == 200 - shard.Price
            && run.Economy.Inventory.GetShardCount(shard.Shard) == shards + shard.Quantity, "Shard UI purchase failed.");
        Require(run.AwaitingReward && !run.RewardsComplete && Time.timeScale == 0,
            "Optional pages bypassed mandatory rewards.");
    }

    private static void ValidateRewardsAndSwaps(RunDirector run)
    {
        Click("Page Rewards");
        AmmoItem expected = run.Offers[0];
        Click("Upgrade offer 1");
        Click("Upgrade slot 1");
        Click("Confirm replacement");
        Require(ReferenceEquals(expected, run.Drum.Cartridges[0]) && run.ChoosingModule && !run.RewardsComplete,
            "Free cartridge stage broken.");
        expected = run.Offers[0];
        Click("Upgrade offer 1");
        Click("Upgrade slot 6");
        Click("Confirm replacement");
        Require(ReferenceEquals(expected, run.Drum.Sockets[5]) && run.RewardsComplete
            && run.AwaitingReward && _screen.IsOpen && Time.timeScale == 0, "Free module stage auto-closed screen.");
        run.Choose(0, 0);
        Require(FindButton("Next wave").interactable, "Next-wave button remains disabled after rewards.");
        foreach (string page in new[] { "Rewards", "Craft", "Shop" })
        {
            Click("Page " + page);
            AmmoItem first = run.Drum.Cartridges[0];
            AmmoItem socket = run.Drum.Sockets[0];
            Click("Upgrade slot 1");
            Click("Upgrade slot 2");
            Require(ReferenceEquals(first, run.Drum.Cartridges[1]) && ReferenceEquals(socket, run.Drum.Sockets[0]),
                "Swapping failed on page " + page);
        }
        Click("Page Rewards");
    }

    private static Button FindButton(string name)
    {
        GameObject target = GameObject.Find(name);
        Require(target != null, "Missing active button: " + name);
        return target.GetComponent<Button>();
    }

    private static void Click(string name)
    {
        FindButton(name).onClick.Invoke();
    }

    private static void Capture(string name, int width = 1280, int height = 720)
    {
        Canvas canvas = GameObject.Find("Run interface").GetComponent<Canvas>();
        Camera camera = Camera.main;
        int originalOrder = canvas.sortingOrder;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.sortingOrder = 32760;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        var texture = new RenderTexture(width, height, 24);
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        _screen.Presentation.RenderNow(width, height);
        _screen.RefreshLayout();
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = texture;
        var capture = new Texture2D(width, height, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        capture.Apply();
        Directory.CreateDirectory(_artifacts);
        File.WriteAllBytes(_artifacts + "/" + name + ".png", capture.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = originalOrder;
        canvas.worldCamera = null;
        texture.Release();
        UnityEngine.Object.Destroy(texture);
        UnityEngine.Object.Destroy(capture);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Finish(int code)
    {
        _exitCode = code;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged += ExitAfterPlay;
        EditorApplication.isPlaying = false;
    }

    private static void ExitAfterPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }
        EditorApplication.playModeStateChanged -= ExitAfterPlay;
        EditorSettings.enterPlayModeOptionsEnabled = _oldOptionsEnabled;
        EditorSettings.enterPlayModeOptions = _oldOptions;
        EditorApplication.delayCall += () => EditorApplication.Exit(_exitCode);
    }
}
