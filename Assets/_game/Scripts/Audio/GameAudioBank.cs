using System;
using UnityEngine;

public enum SoundCue
{
    Shot, Explosion, BandolierOpen, Swap, Select, EnemyHit, EnemyPain, EnemyDeath,
    Ricochet, HeroImpact, HeroPain, HeroDeath, WaveWin, GameWin, Defeat, Reload
}

public enum SoundBus { Combat, Foley, Interface, Voice, Results }

[Serializable]
public class SoundDefinition
{
    public SoundCue Cue;
    public SoundBus Bus;
    public AudioClip[] Clips = Array.Empty<AudioClip>();
    [Range(0, 1)] public float Volume = 0.4f;
    public Vector2 Pitch = new Vector2(0.96f, 1.04f);
    [Range(0, 0.2f)] public float VolumeVariation = 0.06f;
    [Min(0)] public float MinimumInterval = 0.04f;
    [Min(1)] public int MaximumVoices = 3;
    [Range(0, 256)] public int Priority = 128;
}

[CreateAssetMenu(menuName = "Game/Audio bank")]
public class GameAudioBank : ScriptableObject
{
    [Range(0, 1)] public float MasterVolume = 0.72f;
    [Range(8, 64)] public int VoicePoolSize = 28;
    public SoundDefinition[] Sounds = Array.Empty<SoundDefinition>();
}
