using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class PhysicalBlood : MonoBehaviour
{
    [SerializeField, Min(0.001f)] private float _surfaceOffset = 0.008f;
    private ParticleSystem _particles;
    private ParticleSystem.Particle[] _buffer;
    private readonly HashSet<uint> _settled = new HashSet<uint>();
    private float _floorHeight;

    public float CleanupDelay => _particles.main.duration + _particles.main.startLifetime.constantMax + 0.2f;
    public int SettledCount => _settled.Count;

    private void Awake()
    {
        _particles = GetComponent<ParticleSystem>();
        _buffer = new ParticleSystem.Particle[_particles.main.maxParticles];
        StaticShooterStage stage = StaticShooterStage.Instance;
        if (stage != null && stage.GroundSurface != null)
        {
            _floorHeight = stage.GroundSurface.position.y;
            var collision = _particles.collision;
            collision.SetPlane(0, stage.GroundSurface);
        }
    }

    private void LateUpdate()
    {
        int count = _particles.GetParticles(_buffer);
        for (int index = 0; index < count; index++)
        {
            ParticleSystem.Particle particle = _buffer[index];
            float contactHeight = _floorHeight + particle.GetCurrentSize(_particles) * 0.4f + _surfaceOffset;
            if (!_settled.Contains(particle.randomSeed)
                && particle.position.y <= contactHeight && particle.velocity.y <= 0.2f)
            {
                _settled.Add(particle.randomSeed);
            }

            if (_settled.Contains(particle.randomSeed))
            {
                Vector3 position = particle.position;
                position.y = _floorHeight + _surfaceOffset;
                particle.position = position;
                particle.velocity = Vector3.zero;
                particle.angularVelocity3D = Vector3.zero;
                particle.rotation3D = new Vector3(90, particle.randomSeed % 360, 0);
                _buffer[index] = particle;
            }
        }

        // Airborne droplets use the particle physics solver. Only landed quads are pinned flat.
        _particles.SetParticles(_buffer, count);
    }
}
