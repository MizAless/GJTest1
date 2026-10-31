using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private float _maximumHealth = 100;
    [SerializeField, Min(0)] private float _invulnerabilityTime = 0.7f;
    private float _invulnerableUntil;

    public float Health { get; private set; }
    public float MaximumHealth => _maximumHealth;
    public bool IsDead => Health <= 0;

    private void Awake()
    {
        Health = _maximumHealth;
    }

    public bool TryTakeDamage(float amount)
    {
        RunDirector run = RunDirector.Instance;
        if (amount <= 0 || IsDead || Time.time < _invulnerableUntil
            || (run != null && !run.CombatActive))
        {
            return false;
        }

        Health = Mathf.Max(0, Health - amount);
        _invulnerableUntil = Time.time + _invulnerabilityTime;
        if (IsDead)
        {
            if (run != null)
            {
                run.Lose();
            }
            GameAudio.Play(SoundCue.HeroImpact);
            GameAudio.Play(SoundCue.HeroDeath);
        }
        else
        {
            GameAudio.Play(SoundCue.HeroImpact);
            GameAudio.Play(SoundCue.HeroPain);
        }

        return true;
    }
}
