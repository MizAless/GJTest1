using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletContainer : MonoBehaviour
{
    [SerializeField] float _reloadTime = 1;
    [SerializeField] int _maxBullets = 6;
    public int MaxBullets => _maxBullets;
    public int Used { get; private set; }
    public int BulletsRemaining => MaxBullets - Used;
    public bool IsReloading { get; private set; }
    public AmmoItem[] Cartridges { get; private set; }
    public AmmoItem[] Sockets { get; private set; }
    public CombatProfile[] Profiles { get; private set; }
    public event Action Shooted;
    public event Action Reloaded;
    public event Action<int,int> Transferred;
    public bool Initialized => Cartridges != null;
    public void Init()
    {
        if (Initialized) return;
        Cartridges = new AmmoItem[MaxBullets]; Sockets = new AmmoItem[MaxBullets];
        for (int i = 0; i < MaxBullets; i++) Cartridges[i] = AmmoItem.Ordinary();
        Cartridges[0] = AmmoGenerator.Generate(false, true);
        Rebuild();
    }
    public void Install(int slot, AmmoItem item, bool module)
    {
        if (module) Sockets[slot] = item; else Cartridges[slot] = item;
        Rebuild();
    }
    public void SwapCartridges(int a, int b)
    {
        var old = Cartridges[a]; Cartridges[a] = Cartridges[b]; Cartridges[b] = old; Rebuild();
    }
    public IEnumerable<AmmoItem> Layers(int i)
    {
        yield return Cartridges[i]; if (Sockets[i] != null) yield return Sockets[i];
    }
    public int Next(int n) => (n + MaxBullets) % MaxBullets;
    public IEnumerable<int> Destinations(int i, AmmoTrigger trigger)
    {
        if (trigger == AmmoTrigger.ReloadNext || trigger == AmmoTrigger.ShotNext) yield return Next(i+1);
        if (trigger == AmmoTrigger.ReloadPrevious || trigger == AmmoTrigger.ShotPrevious) yield return Next(i-1);
        if (trigger == AmmoTrigger.ReloadTwo || trigger == AmmoTrigger.ShotTwo) { yield return Next(i+1); yield return Next(i+2); }
        if (trigger == AmmoTrigger.ReloadAll) for (int n = 1; n < MaxBullets; n++) yield return Next(i+n);
    }
    public void Grant(int source, int target, AmmoItem layer)
    {
        Profiles[target].Apply(layer.Properties); Transferred?.Invoke(source,target);
    }
    public void Rebuild()
    {
        Used = 0; Profiles = new CombatProfile[MaxBullets];
        for (int i = 0; i < MaxBullets; i++) { Profiles[i] = new CombatProfile(); foreach (var l in Layers(i)) Profiles[i].Apply(l.Properties); }
        for (int i = 0; i < MaxBullets; i++) foreach (var l in Layers(i))
        {
            if (l.Trigger == AmmoTrigger.ReloadRepeat)
            {
                int previous = Next(i-1); Transferred?.Invoke(i,previous);
                foreach (var original in Layers(previous))
                    if (original.Trigger >= AmmoTrigger.ReloadNext && original.Trigger <= AmmoTrigger.ReloadAll)
                        foreach (int target in Destinations(previous,original.Trigger)) Grant(previous,target,original);
            }
            else if (l.Trigger >= AmmoTrigger.ReloadNext && l.Trigger <= AmmoTrigger.ReloadAll)
                foreach (int target in Destinations(i,l.Trigger)) Grant(i,target,l);
        }
        Reloaded?.Invoke();
    }
    public bool BeginShot(out int slot)
    {
        slot = Used;
        if (!Initialized || IsReloading || Used >= MaxBullets) return false;
        Used++; Shooted?.Invoke(); return true;
    }
    public IEnumerator Reload()
    {
        if (IsReloading) yield break;
        IsReloading = true;
        yield return new WaitForSeconds(_reloadTime);
        Rebuild(); IsReloading = false;
    }
}
