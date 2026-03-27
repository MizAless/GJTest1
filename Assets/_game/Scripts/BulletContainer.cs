using System;
using System.Collections;
using UnityEngine;

public class BulletContainer : MonoBehaviour
{
    public int MaxBullets => _maxBullets;
    
    [SerializeField] private int _maxBullets = 6;
    [SerializeField] private float _reloadTime;

    private int _currentBullets;
    
    private bool _isReloading = false;

    public int CurrentBullets => _currentBullets;
    
    public event Action Shooted;
    public event Action Reloaded;

    private void Start()
    {
        _currentBullets = _maxBullets;
        
        // StartCoroutine(Reloading());
    }

    // private IEnumerator Reloading()
    // {
    //     while (true)
    //     {
    //         TryReload();
    //         yield return new WaitForSeconds(_reloadTime);
    //     }
    // }
    
    public void TryReload()
    {
        if (_currentBullets >= _maxBullets)
            return;
        
        _currentBullets++;
        Reloaded?.Invoke();
    }
    
    private void TryStartFullReloading()
    {
        if (_currentBullets >= _maxBullets)
            return;
        
        StartCoroutine(StartFullReloading());
    }
    
    private IEnumerator StartFullReloading()
    {
        if (_isReloading)
            yield break;

        _isReloading = true;
        
        yield return FullReloadCoroutine();
        
        _isReloading = false;
    }
    
    private IEnumerator FullReloadCoroutine()
    {
        yield return new WaitForSeconds(_reloadTime);
        
        _currentBullets = _maxBullets;
        Reloaded?.Invoke();
    }

    public bool TryShoot()
    {
        if (_currentBullets < 1)
            return false;

        _currentBullets--;
        
        Shooted?.Invoke();
        
        if (_currentBullets == 0)
            TryStartFullReloading();
        
        return true;
    }
}