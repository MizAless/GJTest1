using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GameAudioAuthoring
{
    public const string BankPath = "Assets/_game/Resources/Audio/GameAudioBank.asset";
    private const string ClipsPath = "Assets/_game/Resources/Audio/Clips/";

    [MenuItem("Tools/Prototype/Audio/Create or update audio bank")]
    public static void CreateBank()
    {
        foreach (string path in Directory.GetFiles(ClipsPath, "*.wav"))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            var serializedImporter = new SerializedObject(importer);
            SerializedProperty normalize = serializedImporter.FindProperty("m_Normalize");
            if (normalize != null)
            {
                normalize.boolValue = false;
                serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            }
            importer.SaveAndReimport();
        }

        GameAudioBank bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>(BankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<GameAudioBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }

        bank.Sounds = new[]
        {
            Sound(SoundCue.Shot, "Shot", SoundBus.Combat, 0.40f, 0.95f, 1.04f, 0.045f, 3, 100),
            Sound(SoundCue.Explosion, "Explosion", SoundBus.Combat, 0.38f, 0.9f, 1.06f, 0.09f, 2, 110),
            Sound(SoundCue.BandolierOpen, "Bandolier", SoundBus.Foley, 0.75f, 0.98f, 1.02f, 0.15f, 1, 50),
            Sound(SoundCue.Swap, "Swap", SoundBus.Interface, 0.40f, 0.94f, 1.06f, 0.09f, 2, 80),
            Sound(SoundCue.Select, "Select", SoundBus.Interface, 0.26f, 0.98f, 1.04f, 0.07f, 1, 70),
            Sound(SoundCue.EnemyHit, "Hit", SoundBus.Combat, 0.22f, 0.92f, 1.08f, 0.025f, 4, 150),
            Sound(SoundCue.EnemyPain, "EnemyPain", SoundBus.Voice, 0.18f, 0.94f, 1.06f, 0.22f, 2, 160),
            Sound(SoundCue.EnemyDeath, "EnemyDeath", SoundBus.Voice, 0.27f, 0.94f, 1.06f, 0.07f, 3, 120),
            Sound(SoundCue.Ricochet, "Ricochet", SoundBus.Combat, 0.18f, 0.94f, 1.10f, 0.05f, 3, 140),
            Sound(SoundCue.HeroImpact, "HeroImpact", SoundBus.Combat, 0.60f, 0.96f, 1.04f, 0.15f, 1, 35),
            Sound(SoundCue.HeroPain, "HeroPain", SoundBus.Voice, 0.55f, 0.98f, 1.02f, 0.40f, 1, 30),
            Sound(SoundCue.HeroDeath, "HeroDeath", SoundBus.Voice, 0.60f, 1, 1, 1, 1, 10),
            Sound(SoundCue.WaveWin, "WaveWin", SoundBus.Results, 0.55f, 1, 1, 0.5f, 1, 25),
            Sound(SoundCue.GameWin, "GameWin", SoundBus.Results, 0.60f, 1, 1, 1, 1, 5),
            Sound(SoundCue.Defeat, "Defeat", SoundBus.Results, 0.55f, 1, 1, 1, 1, 5),
            Sound(SoundCue.Reload, "Reload", SoundBus.Foley, 0.32f, 0.98f, 1.02f, 0.4f, 1, 80)
        };
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
    }

    private static SoundDefinition Sound(SoundCue cue, string prefix, SoundBus bus, float volume,
        float minPitch, float maxPitch, float interval, int voices, int priority)
    {
        AudioClip[] clips = Directory.GetFiles(ClipsPath, prefix + "_*.wav").OrderBy(path => path)
            .Select(path => AssetDatabase.LoadAssetAtPath<AudioClip>(path.Replace('\\', '/'))).ToArray();
        if (clips.Length == 0 || clips.Any(clip => clip == null))
        {
            throw new InvalidOperationException("Missing rendered audio for " + cue);
        }

        return new SoundDefinition
        {
            Cue = cue, Clips = clips, Bus = bus, Volume = volume,
            Pitch = new Vector2(minPitch, maxPitch), MinimumInterval = interval,
            MaximumVoices = voices, Priority = priority
        };
    }
}
