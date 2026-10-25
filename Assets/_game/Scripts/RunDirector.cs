using System;
using System.Collections;
using UnityEngine;

public class RunDirector : MonoBehaviour
{
    public static RunDirector Instance { get; private set; }
    public bool CombatActive { get; private set; }
    public int Round { get; private set; }
    public int Pack { get; private set; }
    public int PackCount { get; private set; }
    public bool AwaitingReward { get; private set; }
    public bool ChoosingModule { get; private set; }
    public bool Victory { get; private set; }
    public AmmoItem[] Offers { get; private set; }
    public Shooter Shooter { get; private set; }
    public BulletContainer Drum => Shooter.Container;
    EnemySpawner _spawner;
    RunUI _ui;
    public void Initialize(EnemySpawner spawner) { Instance=this; _spawner=spawner; }
    IEnumerator Start()
    {
        Shooter=FindAnyObjectByType<Shooter>(); Drum.Init();
        var old=FindAnyObjectByType<BulletContainerView>(); if(old!=null) old.gameObject.SetActive(false);
        _ui=gameObject.AddComponent<RunUI>(); _ui.Initialize(this);
        ShowReward(true);
        yield return new WaitUntil(()=>!AwaitingReward);
        for(int round=1;round<=3;round++)
        {
            Round=round; var packs=GameScript.Round(round); PackCount=packs.Length;
            CombatActive=true; Time.timeScale=1; Drum.Rebuild();
            for(int pack=0;pack<packs.Length;pack++)
            {
                CombatActive=true;
                Pack=pack+1; _ui.Note("Раунд "+Round+" · пачка "+Pack+" / "+PackCount);
                yield return _spawner.SpawnPack(packs[pack]);
                yield return new WaitUntil(()=>G.Enemies.Count==0);
                CombatActive=false;
                yield return new WaitUntil(()=>!Shooter.IsFiring&&!Drum.IsReloading);
                yield return new WaitForSeconds(.6f);
            }
            CombatActive=false;
            if(round<3)
            {
                ShowReward(false); yield return new WaitUntil(()=>!AwaitingReward);
                ShowReward(true); yield return new WaitUntil(()=>!AwaitingReward);
            }
        }
        Victory=true; Time.timeScale=0; _ui.ShowVictory();
    }
    void ShowReward(bool module)
    {
        CombatActive=false; AwaitingReward=true; ChoosingModule=module;
        Offers=AmmoGenerator.Offer(module); Time.timeScale=0;
        _ui.ShowReward();
    }
    public void Choose(int offer, int slot)
    {
        if(!AwaitingReward||offer<0||offer>=Offers.Length||slot<0||slot>=Drum.MaxBullets) return;
        Drum.Install(slot,Offers[offer],ChoosingModule);
        AwaitingReward=false; _ui.HideReward(); Time.timeScale=1;
    }
    void OnDestroy() { if(Instance==this) Instance=null; Time.timeScale=1; }
}
