using System;
using DG.Tweening;
using UnityEngine;

public class Shooter : MonoBehaviour
{
    [SerializeField] private BulletContainer  _bulletContainer;
    
    [SerializeField] private Bullet _bulletPrefab;
    [SerializeField] private ParticleSystem _shootVFX;
    [SerializeField] private float _shootForce;

    private void Start()
    {
        _bulletContainer.Init();
    }

    private void Shoot(){

        if (!_bulletContainer.TryShoot(out var bulletData))
            return;
        
        var from = transform.position;
        
        Vector2 shootPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        
        Vector3 direction = new Vector3(shootPosition.x, shootPosition.y, 0) - from;
        direction.Normalize();
        
        var bullet = Instantiate(_bulletPrefab, from, Quaternion.identity);
        bullet.Init(bulletData);
        bullet.Launch(direction, _shootForce);
        OnShoot(direction);

    }
    
    private void OnShoot(Vector3 direction)
    {
        Camera.main.transform.DOKill(true);
        Camera.main.transform.DOPunchPosition(Vector3.one * 0.2f, 0.2f, 100, 5);
        
        _shootVFX.transform.rotation = Quaternion.LookRotation(direction);
        _shootVFX.Play();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Shoot();
        }
    }
}