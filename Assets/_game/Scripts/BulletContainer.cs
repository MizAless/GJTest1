using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletContainer : MonoBehaviour
{
    [SerializeField] private float _reloadTime = 1;
    [SerializeField] private int _maxBullets = 6;
    
    public int MaxBullets => _maxBullets;

    public int BulletsRemaining => _bullets.Count - _shootedBulletsCount;
    
    private List<BulletData> _bullets = new List<BulletData>();
    private BulletData _currentBullet;
    private int _currentBulletIndex => _shootedBulletsCount % _bullets.Count;
    private int _shootedBulletsCount;
    private bool _isReloading;

    public event Action Shooted;
    public event Action Reloaded;

    // public void Init()
    // {
       //  for (int i = 0; i < MaxBullets; i++)
       //  {
       //      if (i % 3 == 0)
			    // _bullets.Add(PenetrationBullet.Create());
       //      if (i % 3 == 1)
       //          _bullets.Add(RicochetBullet.Create());
       //      if (i % 3 == 2)
       //          _bullets.Add(ExplosionBullet.Create());
       //  }
    // }

    public void Init()
    {
        for (int i = 0; i < MaxBullets; i++)
        {
            _bullets.Add(DefaultBullet.Create());
        }
    }
    
    public bool TryShoot(out BulletData bulletData)
    {
        bulletData = null;
        
        if (_isReloading)
            return false;
        
        if (_shootedBulletsCount == _bullets.Count)
            return false;
        
        bulletData = _bullets[_currentBulletIndex];
        
        _shootedBulletsCount++;
        
        Shooted?.Invoke();
        
        if (_shootedBulletsCount == _bullets.Count)
            TryStartFullReloading();
        
        return true;
    }

    private void TryStartFullReloading()
    {
        StartCoroutine(FullReloadCoroutine());
    }

    private IEnumerator FullReloadCoroutine()
    {
        if (_isReloading)
            yield break;

        _isReloading = true;
        
        yield return new WaitForSeconds(_reloadTime);

        _shootedBulletsCount = 0;
        ResetBullets();
        
        Reloaded?.Invoke();
        
        _isReloading = false;
    }
    
    private void ResetBullets()
    {
        foreach (var bullet in _bullets)
        {
            bullet.OnReset();
        }
    }
}
