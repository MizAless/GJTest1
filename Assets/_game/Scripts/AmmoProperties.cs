using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum AmmoTrigger { None, ReloadNext, ReloadPrevious, ReloadTwo, ReloadAll, ReloadRepeat, ShotNext, ShotPrevious, ShotTwo, ShotNextEffect, ShotPastTargets }

[Serializable]
public class AmmoProperties
{
    public int Ricochet, Pierce, Fan, Burst, Split, Burn, Chill, Bleed, Poison, Puncture;
    public bool Overload;
    public float More, ProjectileMore, DotMore, AreaMore, AfflictedMore;
    public AmmoProperties Copy() => (AmmoProperties)MemberwiseClone();
    public string Description()
    {
        var p = new List<string>();
        Add(p, Ricochet, "рикошет"); Add(p, Pierce, "пробитие"); Add(p, Fan, "веер"); Add(p, Burst, "очередь"); Add(p, Split, "разветвление");
        Add(p, Burn, "поджог"); Add(p, Chill, "холод"); Add(p, Bleed, "кровотечение"); Add(p, Poison, "яд"); Add(p, Puncture, "прокол");
        if (Overload) p.Add("Досрочная перегрузка: огонь/яд взрываются и снимаются; холод от 3, прокол от 2");
        Percent(p, More, "весь урон"); Percent(p, ProjectileMore, "урон снарядов"); Percent(p, DotMore, "постепенный урон"); Percent(p, AreaMore, "урон области"); Percent(p, AfflictedMore, "по цели с состоянием");
        return p.Count == 0 ? "Обычное попадание" : string.Join("\n", p);
    }
    static void Add(List<string> list, int n, string name) { if (n > 0) list.Add("+" + n + " " + name); }
    static void Percent(List<string> list, float n, string name) { if (n > 0) list.Add("×" + (1 + n / 100f).ToString("0.##") + " " + name); }
    public string Tags()
    {
        var tags = new List<string> { "снаряды" };
        if (Burn > 0) tags.Add("огонь"); if (Chill > 0) tags.Add("холод"); if (Poison > 0) tags.Add("яд"); if (Bleed > 0) tags.Add("кровотечение"); if (Puncture > 0) tags.Add("прокол");
        if (Burn + Poison + Bleed > 0) tags.Add("постепенный урон"); if (Overload) tags.Add("область");
        return string.Join(" · ", tags);
    }
}

[Serializable]
public class AmmoItem
{
    public string Name;
    public BulletRarity Rarity;
    public AmmoProperties Properties = new AmmoProperties();
    public AmmoTrigger Trigger;
    public string Description => Properties.Description() + (Trigger == AmmoTrigger.None ? "" : "\n\n" + TriggerName(Trigger));
    public static string TriggerName(AmmoTrigger trigger)
    {
        switch (trigger)
        {
            case AmmoTrigger.ReloadNext: return "Перезарядка → усилить следующий";
            case AmmoTrigger.ReloadPrevious: return "Перезарядка → усилить предыдущий";
            case AmmoTrigger.ReloadTwo: return "Перезарядка → усилить два следующих";
            case AmmoTrigger.ReloadAll: return "Перезарядка → усилить остальные гнёзда";
            case AmmoTrigger.ReloadRepeat: return "Перезарядка → повтор предыдущего триггера";
            case AmmoTrigger.ShotNext: return "Выстрел → усилить следующий";
            case AmmoTrigger.ShotPrevious: return "Выстрел → усилить предыдущий";
            case AmmoTrigger.ShotTwo: return "Выстрел → усилить два следующих";
            case AmmoTrigger.ShotNextEffect: return "Выстрел → запустить следующий патрон";
            case AmmoTrigger.ShotPastTargets: return "Выстрел → попасть по прошлым целям";
            default: return "Без триггера";
        }
    }
    public static Color RarityColor(BulletRarity rarity) => new[] { new Color(.8f,.84f,.88f), new Color(.45f,.75f,1f), new Color(.8f,.52f,1f), new Color(1f,.72f,.27f) }[(int)rarity];
    public static string RarityName(BulletRarity rarity) => new[] { "Обычный", "Редкий", "Эпический", "Легендарный" }[(int)rarity];
    public static AmmoItem Ordinary() => new AmmoItem { Name = "Обычный патрон" };
}

// Profiles are immutable snapshots for each projectile; module instances are never shared.
public class CombatProfile
{
    public AmmoProperties Properties = new AmmoProperties();
    public float All = 1, Projectile = 1, Dot = 1, Area = 1, Afflicted = 1;
    public float Damage => 10 * All * Projectile;
    public CombatProfile Copy() { var p = (CombatProfile)MemberwiseClone(); p.Properties = Properties.Copy(); return p; }
    public void Apply(AmmoProperties p)
    {
        var a = Properties;
        a.Ricochet += p.Ricochet; a.Pierce += p.Pierce; a.Fan += p.Fan; a.Burst += p.Burst; a.Split += p.Split;
        a.Burn += p.Burn; a.Chill += p.Chill; a.Bleed += p.Bleed; a.Poison += p.Poison; a.Puncture += p.Puncture; a.Overload |= p.Overload;
        All *= 1 + p.More / 100f; Projectile *= 1 + p.ProjectileMore / 100f; Dot *= 1 + p.DotMore / 100f; Area *= 1 + p.AreaMore / 100f; Afflicted *= 1 + p.AfflictedMore / 100f;
    }
    public Color Color => Properties.Burn > 0 ? new Color(1,.48f,.18f) : Properties.Poison > 0 ? new Color(.55f,1,.3f) : Properties.Chill > 0 ? Color.cyan : Properties.Bleed > 0 ? new Color(1,.3f,.4f) : Properties.Puncture > 0 ? new Color(.8f,.5f,1) : new Color(1,.85f,.45f);
}

public static class AmmoGenerator
{
    public static AmmoItem Generate(bool module, bool starter = false)
    {
        float roll = UnityEngine.Random.value;
        BulletRarity rarity = BulletRarity.Common;
        if (!starter)
        {
            if (roll >= 0.97f)
            {
                rarity = BulletRarity.Legendary;
            }
            else if (roll >= 0.85f)
            {
                rarity = BulletRarity.Epic;
            }
            else if (roll >= 0.55f)
            {
                rarity = BulletRarity.Rare;
            }
        }
        var item = new AmmoItem { Rarity = rarity };
        var choices = Enumerable.Range(0, 16).OrderBy(_ => UnityEngine.Random.value).Take((int)rarity + 1).ToList();
        // One standalone property: projectile behavior or a status, without rarity bonuses.
        if (starter)
        {
            choices = new List<int> { UnityEngine.Random.Range(0, 10) };
        }
        int n = rarity >= BulletRarity.Epic ? 2 : 1;
        foreach (int k in choices)
        {
            var p = item.Properties;
            switch (k)
            {
                case 0: p.Ricochet = n; break; case 1: p.Pierce = n; break; case 2: p.Fan = n; break; case 3: p.Burst = n; break; case 4: p.Split = 1; break;
                case 5: p.Burn = n; break; case 6: p.Chill = n; break; case 7: p.Bleed = n; break; case 8: p.Poison = n; break; case 9: p.Puncture = n; break;
                case 10: p.Overload = true; break; case 11: p.More = 25*n; break; case 12: p.ProjectileMore = 50*n; break; case 13: p.DotMore = 50*n; break; case 14: p.AreaMore = 50*n; break; case 15: p.AfflictedMore = 50*n; break;
            }
        }
        if (!starter && rarity >= BulletRarity.Rare && UnityEngine.Random.value < .55f)
            item.Trigger = (AmmoTrigger)UnityEngine.Random.Range(1, Enum.GetValues(typeof(AmmoTrigger)).Length);
        item.Name = (module ? "Модуль: " : "Патрон: ") + item.Properties.Description().Split('\n')[0];
        return item;
    }
    public static AmmoItem[] Offer(bool module)
    {
        var items = new List<AmmoItem>();
        for (int i = 0; i < 3; i++)
        {
            AmmoItem item; int tries = 0;
            do { item = Generate(module); } while (items.Any(x => x.Description == item.Description) && ++tries < 30);
            items.Add(item);
        }
        return items.ToArray();
    }
}

