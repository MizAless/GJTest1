using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BulletContainerView : MonoBehaviour
{
    [SerializeField] private BulletContainer _bulletContainer;

    [SerializeField] private GameObject _UIContainer;
    [SerializeField] private Image _bulletViewTemplate;

    [SerializeField] private Color _activeColor = new Color32(255, 255, 255, 255);
    [SerializeField] private Color _disabledColor = new Color32(255, 255, 255, 30);

    private List<Image> _bullets = new();

    private void Awake()
    {
        _bulletContainer.Shooted += BulletContainerOnShooted;
        _bulletContainer.Reloaded += BulletContainerOnShooted;
    }

    private void BulletContainerOnShooted()
    {
        UpdateView(_bulletContainer.BulletsRemaining);
    }

    private void Start()
    {
        for (int i = 0; i < _bulletContainer.MaxBullets; i++)
        {
            _bullets.Add(Instantiate(_bulletViewTemplate, _UIContainer.transform));
        }
    }
    
    public void UpdateView(int currentBullets)
    {
        for (int i = 0; i < _bullets.Count; i++)
        {
            _bullets[i].color = currentBullets > i ? _activeColor : _disabledColor;
        }
    }
}
