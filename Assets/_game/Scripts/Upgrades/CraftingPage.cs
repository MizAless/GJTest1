using System;
using System.Collections;
using System.Collections.Generic;
using GameEconomy;
using UnityEngine;
using UnityEngine.UI;

public sealed class CraftingPage : MonoBehaviour
{
    private RunDirector _run;
    private UpgradeSelection _selection;
    private ICraftingService _crafting;
    private readonly Dictionary<ShardId, Button> _shards = new Dictionary<ShardId, Button>();
    private Button _cartridgeMode;
    private Button _moduleMode;
    private Button _apply;
    private Text _target;
    private Text _details;
    private Text _result;
    private Coroutine _animation;
    public ShardId? SelectedShard { get; private set; }
    public bool IsModule { get; private set; }
    public event Action Changed;
    public event Action<int> Crafted;
    public bool CapturesSlots => SelectedShard.HasValue;

    public void Initialize(RunDirector run, UpgradeSelection selection)
    {
        _run = run;
        _selection = selection;
        _crafting = new ShardCraftingService(run.Economy,
            Resources.Load<AmmoDropTable>(AmmoDropTable.ResourcePath), run.Drum);
        for (int kind = 0; kind < 3; kind++)
        {
            for (int grade = 1; grade <= 3; grade++)
            {
                var shard = new ShardId((ShardKind)kind, (ShardGrade)grade);
                float left = kind / 3f;
                float top = 1 - (grade - 1) * 0.15f;
                Button button = IntermissionWidgets.Button(transform, "Craft shard " + shard.Kind + " " + shard.Grade,
                    new Vector2(left, top - 0.135f), new Vector2(left + 0.315f, top), "", () => SelectShard(shard));
                button.gameObject.AddComponent<TooltipTrigger>().Bind(() => ShardTooltip(shard));
                _shards.Add(shard, button);
            }
        }
        _cartridgeMode = IntermissionWidgets.Button(transform, "Craft cartridge mode", new Vector2(0, 0.46f),
            new Vector2(0.49f, 0.54f), "Патрон", () => SetModule(false));
        _moduleMode = IntermissionWidgets.Button(transform, "Craft module mode", new Vector2(0.51f, 0.46f),
            new Vector2(1, 0.54f), "Модуль", () => SetModule(true));
        _target = IntermissionWidgets.Label(transform, "Craft target", new Vector2(0, 0.34f), new Vector2(1, 0.45f), 18);
        _target.gameObject.AddComponent<TooltipTrigger>().Bind(() => AmmoTooltipContent.Create(Target));
        // TooltipTrigger needs a raycastable Graphic on its rect.
        _target.raycastTarget = true;
        _details = IntermissionWidgets.Label(transform, "Craft applicability", new Vector2(0, 0.13f), new Vector2(1, 0.34f), 16);
        _apply = IntermissionWidgets.Button(transform, "Apply shard", new Vector2(0, 0.025f), new Vector2(1, 0.12f),
            "Добавить свойство", Apply);
        _result = IntermissionWidgets.Label(transform, "Craft result", new Vector2(0, -0.07f), new Vector2(1, 0.02f), 17);
        Refresh();
    }

    public AmmoItem Target
    {
        get
        {
            int slot = _selection.Slot;
            if (slot < 0)
            {
                return null;
            }
            return IsModule ? _run.Drum.Sockets[slot] : _run.Drum.Cartridges[slot];
        }
    }

    public void SelectShard(ShardId shard)
    {
        SelectedShard = SelectedShard.HasValue && SelectedShard.Value.Equals(shard) ? (ShardId?)null : shard;
        _result.text = "";
        _selection.Reset();
        Changed?.Invoke();
    }

    private void SetModule(bool module)
    {
        IsModule = module;
        _result.text = "";
        Changed?.Invoke();
    }

    public void ClearSelection()
    {
        SelectedShard = null;
        _result.text = "";
    }

    public void Refresh()
    {
        foreach (KeyValuePair<ShardId, Button> entry in _shards)
        {
            int count = _run.Economy.Inventory.GetShardCount(entry.Key);
            entry.Value.GetComponentInChildren<Text>().text = ShardName(entry.Key.Kind) + " · " + entry.Key.Grade + "\n×" + count;
            IntermissionWidgets.Select(entry.Value, SelectedShard.HasValue && SelectedShard.Value.Equals(entry.Key));
        }
        IntermissionWidgets.Select(_cartridgeMode, !IsModule);
        IntermissionWidgets.Select(_moduleMode, IsModule);
        AmmoItem target = Target;
        _target.text = "Выберите гнездо справа";
        if (_selection.Slot >= 0)
        {
            string itemName = target == null ? "Нет модуля" : target.Name;
            _target.text = "Гнездо " + (_selection.Slot + 1) + " · " + itemName;
        }
        _apply.interactable = false;
        if (!SelectedShard.HasValue)
        {
            _details.text = "Выберите осколок.\nТ1 — лучшее качество, Т3 — базовое.";
            return;
        }
        ShardId shard = SelectedShard.Value;
        int cost = _run.Economy.Settings.GetGradeRule(shard.Grade).Cost;
        string price = "Стоимость: " + cost + " × " + ShardName(shard.Kind) + " " + shard.Grade;
        if (target == null)
        {
            _details.text = price + "\n" + (_selection.Slot >= 0 ? "В этом гнезде нет модуля." : "Теперь выберите предмет в патронташе.");
            return;
        }
        CraftCheck check = _crafting.CanApply(new CraftRequest(target, IsModule, shard));
        AffixValues.IsValid(target.Properties, out int countProperties);
        _details.text = price + "\nДоп. свойства: " + countProperties + " / "
            + _run.Economy.Settings.MaximumAdditionalProperties + "\n" + check.Reason;
        _apply.interactable = _run.AwaitingReward && check.CanApply;
    }

    public void Apply()
    {
        if (!_run.AwaitingReward || !SelectedShard.HasValue || Target == null)
        {
            return;
        }
        CraftResult result = _crafting.TryApply(new CraftRequest(Target, IsModule, SelectedShard.Value));
        if (!result.Succeeded)
        {
            _result.text = result.Reason;
            _result.color = new Color(1, 0.48f, 0.35f);
            Changed?.Invoke();
            return;
        }
        var added = new AmmoProperties();
        AmmoGenerator.Apply(added, result.Affix.Property, result.Affix.Value);
        _result.text = "Добавлено: " + added.Description();
        if (_animation != null)
        {
            StopCoroutine(_animation);
        }
        _animation = StartCoroutine(AnimateResult());
        Crafted?.Invoke(_selection.Slot);
        GameAudio.Play(SoundCue.Select);
        Changed?.Invoke();
    }

    private IEnumerator AnimateResult()
    {
        float started = Time.unscaledTime;
        while (Time.unscaledTime - started < 0.8f)
        {
            float progress = (Time.unscaledTime - started) / 0.8f;
            _result.transform.localScale = Vector3.one * (1 + Mathf.Sin(progress * Mathf.PI) * 0.08f);
            _result.color = Color.Lerp(Color.white, IntermissionWidgets.Gold, progress);
            yield return null;
        }
        _result.transform.localScale = Vector3.one;
        _animation = null;
    }

    public static string ShardName(ShardKind kind)
    {
        switch (kind)
        {
            case ShardKind.Chaos: return "Хаос";
            case ShardKind.Ice: return "Лёд";
            case ShardKind.Lava: return "Лава";
            default: return kind.ToString();
        }
    }

    public static TooltipContent ShardTooltip(ShardId shard)
    {
        string effect = "Добавляет случайное новое свойство.";
        if (shard.Kind == ShardKind.Ice)
        {
            effect = "Добавляет новое свойство холода.";
        }
        else if (shard.Kind == ShardKind.Lava)
        {
            effect = "Добавляет новое свойство огня.";
        }
        return new TooltipContent
        {
            Title = "Осколок: " + ShardName(shard.Kind),
            Subtitle = shard.Grade.ToString(),
            Accent = IntermissionWidgets.Gold,
            Properties = new[] { effect, "Т1 — лучшее качество, Т3 — базовое." }
        };
    }
}
