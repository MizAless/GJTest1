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
    public bool Defeated { get; private set; }
    public PlayerHealth PlayerHealth { get; private set; }
    public AmmoItem[] Offers { get; private set; }
    public Shooter Shooter { get; private set; }
    public BulletContainer Drum => Shooter.Container;
    EnemySpawner _spawner;
    RunUI _ui;
    public void Initialize(EnemySpawner spawner) { Instance=this; _spawner=spawner; }
    private IEnumerator Start()
    {
        Shooter = FindAnyObjectByType<Shooter>();
        PlayerHealth = Shooter.GetComponent<PlayerHealth>();
        if (PlayerHealth == null)
        {
            PlayerHealth = Shooter.gameObject.AddComponent<PlayerHealth>();
        }
        Drum.Init();
        BulletContainerView oldView = FindAnyObjectByType<BulletContainerView>();
        if (oldView != null)
        {
            oldView.gameObject.SetActive(false);
        }
        _ui = gameObject.AddComponent<RunUI>();
        _ui.Initialize(this);

        for (int wave = 1; wave <= GameScript.WaveCount; wave++)
        {
            yield return PlayWave(wave);
            if (wave < GameScript.WaveCount)
            {
                GameAudio.Play(SoundCue.WaveWin);
                ShowReward(false);
                yield return new WaitUntil(() => !AwaitingReward);
                ShowReward(true);
                yield return new WaitUntil(() => !AwaitingReward);
            }
        }
        Victory = true;
        GameAudio.SilenceCombat();
        GameAudio.Play(SoundCue.GameWin);
        Time.timeScale = 0;
        _ui.ShowVictory();
    }

    private IEnumerator PlayWave(int wave)
    {
        Round = wave;
        int[][] groups = GameScript.Round(wave);
        PackCount = groups.Length;
        Time.timeScale = 1;
        Drum.Rebuild();

        for (int group = 0; group < groups.Length; group++)
        {
            CombatActive = true;
            Pack = group + 1;
            _ui.Note("Волна " + Round + " · группа " + Pack + " / " + PackCount);
            yield return _spawner.SpawnPack(groups[group]);
            // A group must be completely cleared before the next one can spawn.
            yield return new WaitUntil(() => G.Enemies.Count == 0);
            CombatActive = false;
            yield return new WaitUntil(() => !Shooter.IsFiring && !Drum.IsReloading);
            yield return new WaitForSeconds(0.6f);
        }
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
        GameAudio.Play(SoundCue.Select);
        Drum.Install(slot,Offers[offer],ChoosingModule);
        AwaitingReward=false; _ui.HideReward(); Time.timeScale=1;
    }
    public void Lose()
    {
        if (Victory || Defeated)
        {
            return;
        }

        Defeated = true;
        CombatActive = false;
        AwaitingReward = false;
        StopAllCoroutines();
        Time.timeScale = 0;
        GameAudio.SilenceCombat();
        GameAudio.Play(SoundCue.Defeat, delay: 0.45f);
        _ui.ShowDefeat();
    }

    void OnDestroy() { if(Instance==this) Instance=null; Time.timeScale=1; }
}
