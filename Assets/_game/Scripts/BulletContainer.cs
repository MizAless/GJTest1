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
    private sealed class RuntimeGrant
    {
        public int Target;
        public AmmoProperties Properties;
    }

    private readonly List<RuntimeGrant> _runtimeGrants = new List<RuntimeGrant>();
    private bool _buildingProfiles;
    private bool _silentRefresh;
    public bool Initialized => Cartridges != null;
    public void Init()
    {
        if (Initialized)
        {
            return;
        }
        Cartridges = new AmmoItem[MaxBullets];
        Sockets = new AmmoItem[MaxBullets];
        for (int slot = 0; slot < MaxBullets; slot++)
        {
            Cartridges[slot] = AmmoItem.Ordinary();
        }
        Cartridges[0] = AmmoGenerator.Generate(module: false, starter: true);
        int startingModuleSlot = UnityEngine.Random.Range(0, MaxBullets);
        Sockets[startingModuleSlot] = AmmoGenerator.Generate(module: true, starter: true);
        Rebuild();
    }
    public void Install(int slot, AmmoItem item, bool module)
    {
        if (module) Sockets[slot] = item; else Cartridges[slot] = item;
        Rebuild();
    }
    public void SwapCartridges(int a, int b)
    {
        if (a != b)
        {
            GameAudio.Play(SoundCue.Swap);
        }
        var old = Cartridges[a]; Cartridges[a] = Cartridges[b]; Cartridges[b] = old; Rebuild();
    }
    public IEnumerable<AmmoItem> Layers(int i)
    {
        foreach (AmmoItem layer in Cartridges[i].CombatLayers())
        {
            yield return layer;
        }
        if (Sockets[i] != null)
        {
            yield return Sockets[i];
        }
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
        Profiles[target].Apply(layer.Properties);
        if (!_buildingProfiles)
        {
            _runtimeGrants.Add(new RuntimeGrant { Target = target, Properties = layer.Properties.Copy() });
        }
        if (!_silentRefresh)
        {
            Transferred?.Invoke(source, target);
        }
    }
    public void Rebuild()
    {
        Used = 0;
        _runtimeGrants.Clear();
        BuildProfiles();
        Reloaded?.Invoke();
    }

    // Recalculate equipment without refilling chambers or replaying presentation events.
    // Shot-time transfers remain snapshots of the payload that actually fired.
    public void RefreshProfiles()
    {
        if (!Initialized)
        {
            return;
        }
        _silentRefresh = true;
        try
        {
            BuildProfiles();
            foreach (RuntimeGrant grant in _runtimeGrants)
            {
                Profiles[grant.Target].Apply(grant.Properties);
            }
        }
        finally
        {
            _silentRefresh = false;
        }
    }

    private void BuildProfiles()
    {
        _buildingProfiles = true;
        try
        {
            CalculateEquipmentProfiles();
        }
        finally
        {
            _buildingProfiles = false;
        }
    }

    private void CalculateEquipmentProfiles()
    {
        Profiles = new CombatProfile[MaxBullets];
        for (int slot = 0; slot < MaxBullets; slot++)
        {
            Profiles[slot] = new CombatProfile();
            foreach (AmmoItem layer in Layers(slot))
            {
                if (!layer.IsTriggerPayload)
                {
                    Profiles[slot].Apply(layer.Properties);
                }
            }
        }
        for (int slot = 0; slot < MaxBullets; slot++)
        {
            foreach (AmmoItem layer in Layers(slot))
            {
                if (layer.Trigger == AmmoTrigger.ReloadRepeat)
                {
                    int previous = Next(slot - 1);
                    if (!_silentRefresh)
                    {
                        Transferred?.Invoke(slot, previous);
                    }
                    foreach (AmmoItem original in Layers(previous))
                    {
                        // The linking type repeats an intrinsic, while a rolled repeat
                        // keeps its existing ability to repeat additional triggers too.
                        if (!layer.IsTriggerPayload || original.IsTriggerPayload)
                        {
                            ApplyReloadTrigger(previous, original);
                        }
                    }
                }
                else
                {
                    ApplyReloadTrigger(slot, layer);
                }
            }
        }
    }

    private void ApplyReloadTrigger(int source, AmmoItem layer)
    {
        if (layer.Trigger < AmmoTrigger.ReloadNext || layer.Trigger > AmmoTrigger.ReloadAll)
        {
            return;
        }
        foreach (int target in Destinations(source, layer.Trigger))
        {
            Grant(source, target, layer);
        }
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
        GameAudio.Play(SoundCue.Reload, transform.position);
        yield return new WaitForSeconds(_reloadTime);
        Rebuild(); IsReloading = false;
    }
}
