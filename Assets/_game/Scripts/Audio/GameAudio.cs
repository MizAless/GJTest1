using System;
using UnityEngine;

// One fixed pool per scene; Unity's global Random state is not used by cosmetic variation.
public class GameAudio : MonoBehaviour
{
    private sealed class Voice
    {
        public AudioSource Source;
        public int Definition = -1;
        public float EndsAt;
    }

    private static GameAudio _instance;
    private static bool _missingBank;
    private readonly System.Random _random = new System.Random();
    private GameAudioBank _bank;
    private Voice[] _voices;
    private float[] _nextAllowed;
    private int[] _active;
    private int[] _lastVariant;

    public static GameAudio Instance => _instance;
    public static event Action<SoundCue> Played;
    public SoundCue LastCue { get; private set; }
    public float LastPitch { get; private set; }
    public int LastVariant { get; private set; }
    public int DroppedVoices { get; private set; }
    public int PoolSize => _voices.Length;
    public int ActiveVoiceCount
    {
        get
        {
            int count = 0;
            foreach (Voice voice in _voices)
            {
                if (voice.Definition >= 0)
                {
                    count++;
                }
            }
            return count;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _missingBank = false;
        Played = null;
    }

    public static bool Play(SoundCue cue, Vector3? position = null, float pitchScale = 1, float delay = 0)
    {
        if (!Application.isPlaying || _missingBank)
        {
            return false;
        }

        if (_instance == null)
        {
            GameAudioBank bank = Resources.Load<GameAudioBank>("Audio/GameAudioBank");
            if (bank == null)
            {
                _missingBank = true;
                Debug.LogError("GameAudioBank is missing from Resources/Audio.");
                return false;
            }

            var root = new GameObject("Game audio — pooled voices");
            _instance = root.AddComponent<GameAudio>();
            _instance.Initialize(bank);
        }

        return _instance.PlayCue(cue, position, pitchScale, delay);
    }

    public static void SilenceCombat()
    {
        if (_instance == null)
        {
            return;
        }

        foreach (Voice voice in _instance._voices)
        {
            if (voice.Definition < 0)
            {
                continue;
            }

            SoundDefinition definition = _instance._bank.Sounds[voice.Definition];
            if (definition.Bus == SoundBus.Combat || (definition.Bus == SoundBus.Voice && definition.Priority >= 64))
            {
                _instance.Release(voice);
            }
        }
    }

    private void Initialize(GameAudioBank bank)
    {
        AudioListener listener = FindAnyObjectByType<AudioListener>();
        if (listener != null && listener.GetComponent<AudioSafetyLimiter>() == null)
        {
            listener.gameObject.AddComponent<AudioSafetyLimiter>();
        }
        _bank = bank;
        _nextAllowed = new float[bank.Sounds.Length];
        _active = new int[bank.Sounds.Length];
        _lastVariant = new int[bank.Sounds.Length];
        for (int index = 0; index < _lastVariant.Length; index++)
        {
            _lastVariant[index] = -1;
        }

        _voices = new Voice[bank.VoicePoolSize];
        for (int index = 0; index < _voices.Length; index++)
        {
            var child = new GameObject("Voice " + index);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0;
            source.spatialBlend = 0;
            source.ignoreListenerPause = true;
            _voices[index] = new Voice { Source = source };
        }
    }

    private bool PlayCue(SoundCue cue, Vector3? position, float pitchScale, float delay)
    {
        ReclaimFinished();
        int index = Array.FindIndex(_bank.Sounds, sound => sound.Cue == cue);
        if (index < 0)
        {
            return false;
        }

        SoundDefinition definition = _bank.Sounds[index];
        float now = Time.unscaledTime;
        if (definition.Clips.Length == 0 || now < _nextAllowed[index] || _active[index] >= definition.MaximumVoices)
        {
            DroppedVoices++;
            return false;
        }

        Voice voice = FindVoice(definition.Priority);
        if (voice == null)
        {
            DroppedVoices++;
            return false;
        }

        int variant = 0;
        if (definition.Clips.Length > 1)
        {
            variant = _lastVariant[index] < 0 ? _random.Next(definition.Clips.Length)
                : (_lastVariant[index] + _random.Next(1, definition.Clips.Length)) % definition.Clips.Length;
        }

        AudioClip clip = definition.Clips[variant];
        if (clip == null)
        {
            return false;
        }

        float pitch = Mathf.Clamp(RandomRange(definition.Pitch.x, definition.Pitch.y) * pitchScale, 0.5f, 3);
        float volume = definition.Volume * _bank.MasterVolume * RandomRange(1 - definition.VolumeVariation, 1);
        float pan = 0;
        if (position.HasValue && Camera.main != null)
        {
            Vector3 difference = position.Value - Camera.main.transform.position;
            pan = Mathf.Clamp(difference.x * 0.025f, -0.32f, 0.32f);
            float distance = new Vector2(difference.x, difference.y).magnitude;
            volume *= Mathf.Max(0.7f, 1 / (1 + distance * 0.035f));
        }

        voice.Source.clip = clip;
        voice.Source.pitch = pitch;
        voice.Source.volume = volume;
        voice.Source.panStereo = pan;
        voice.Source.priority = definition.Priority;
        voice.Source.PlayDelayed(Mathf.Max(0, delay));
        voice.Definition = index;
        voice.EndsAt = now + Mathf.Max(0, delay) + clip.length / pitch;
        _nextAllowed[index] = now + definition.MinimumInterval;
        _active[index]++;
        _lastVariant[index] = variant;
        LastCue = cue;
        LastPitch = pitch;
        LastVariant = variant;
        Played?.Invoke(cue);
        return true;
    }

    private Voice FindVoice(int priority)
    {
        Voice candidate = null;
        foreach (Voice voice in _voices)
        {
            if (voice.Definition < 0)
            {
                return voice;
            }

            if (_bank.Sounds[voice.Definition].Priority > priority
                && (candidate == null || voice.EndsAt < candidate.EndsAt))
            {
                candidate = voice;
            }
        }

        if (candidate != null)
        {
            Release(candidate);
        }

        return candidate;
    }

    private float RandomRange(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)_random.NextDouble());
    }

    private void ReclaimFinished()
    {
        foreach (Voice voice in _voices)
        {
            if (voice.Definition >= 0 && Time.unscaledTime >= voice.EndsAt)
            {
                Release(voice);
            }
        }
    }

    private void Release(Voice voice)
    {
        voice.Source.Stop();
        _active[voice.Definition]--;
        voice.Definition = -1;
    }

    private void Update()
    {
        ReclaimFinished();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
