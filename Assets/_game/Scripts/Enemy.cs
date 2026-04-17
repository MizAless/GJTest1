using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private Transform _player;
    [SerializeField] private float _speed = 2f;
    [SerializeField] private ParticleSystem _onDeadVFX;

    private void Awake()
    {
        _player = FindAnyObjectByType<Shooter>().transform;
    }

    private void Update()
    {
        MoveToPlayer();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<Bullet>(out var _))
            OnHit();
    }

    public void TakeDamage()
    {
        OnHit();
        SelfDestroy();
    }
    
    private void OnHit()
    {
        var onDeadVFX =  Instantiate(_onDeadVFX, transform.position, transform.rotation);
        onDeadVFX.Play();
    }
    
    private void SelfDestroy()
    {
        G.Enemies.Remove(this);
        Destroy(gameObject);
    }

    private void MoveToPlayer()
    {
        transform.position = Vector3.MoveTowards(transform.position, _player.position, _speed * Time.deltaTime);    
    }
}