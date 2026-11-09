using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameEconomy;

public class RunDirector : MonoBehaviour
{
    public static RunDirector Instance { get; private set; }
    public RunEconomyContext Economy { get; private set; }
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
    public void Initialize(EnemySpawner spawner)
    {
        Economy?.Dispose();
        RunEconomySettings settings = Resources.Load<RunEconomySettings>(RunEconomySettings.ResourcePath);
        Economy = new RunEconomyContext(settings);
        Instance = this;
        _spawner = spawner;
    }
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

        CombatActive = true;
        for (int group = 0; group < groups.Length; group++)
        {
            Pack = group + 1;
            _ui.Note($"Волна {Round} · группа {Pack} / {PackCount}");
            float nextGroupTime = Time.time + GameScript.GroupCooldown(wave, group);
            List<Enemy> members = new List<Enemy>();
            yield return _spawner.SpawnPack(groups[group], members);
            if (group < groups.Length - 1)
            {
                yield return WaitForNextGroup(members, nextGroupTime);
            }
        }

        // Stragglers from any group must be defeated before rewards become available.
        yield return new WaitUntil(() => G.Enemies.Count == 0);
        CombatActive = false;
        yield return new WaitUntil(() => !Shooter.IsFiring && !Drum.IsReloading);
        yield return new WaitForSeconds(0.6f);
    }

    private static IEnumerator WaitForNextGroup(List<Enemy> members, float deadline)
    {
        const float clearDelay = 0.2f;
        while (Time.time < deadline)
        {
            bool hasSurvivors = members.Exists(enemy => enemy != null && enemy.IsAlive);
            if (!hasSurvivors)
            {
                // A quick clear accelerates this group even if older groups still have survivors.
                float clearDeadline = Mathf.Min(deadline, Time.time + clearDelay);
                while (Time.time < clearDeadline)
                {
                    yield return null;
                }
                yield break;
            }
            yield return null;
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
        if (!AwaitingReward || offer < 0 || offer >= Offers.Length || slot < 0 || slot >= Drum.MaxBullets)
        {
            return;
        }
        GameAudio.Play(SoundCue.Select);
        Drum.Install(slot, Offers[offer], ChoosingModule);
        if (!ChoosingModule)
        {
            ChoosingModule = true;
            Offers = AmmoGenerator.Offer(true);
            _ui.ShowReward();
            return;
        }
        AwaitingReward = false;
        _ui.HideReward();
        Time.timeScale = 1;
    }

    public bool SwapRewardCartridges(int first, int second)
    {
        if (!AwaitingReward || first < 0 || second < 0 || first >= Drum.MaxBullets || second >= Drum.MaxBullets)
        {
            return false;
        }
        Drum.SwapCartridges(first, second);
        return true;
    }
    public void Lose()
    {
        if (Victory || Defeated)
        {
            return;
        }

        Defeated = true;
        Economy?.Dispose();
        CombatActive = false;
        AwaitingReward = false;
        StopAllCoroutines();
        Time.timeScale = 0;
        GameAudio.SilenceCombat();
        GameAudio.Play(SoundCue.Defeat, delay: 0.45f);
        _ui.ShowDefeat();
    }

    private void OnDestroy()
    {
        Economy?.Dispose();
        if (Instance == this)
        {
            Instance = null;
        }
        Time.timeScale = 1;
    }
}
