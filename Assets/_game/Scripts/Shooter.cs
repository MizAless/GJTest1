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

    public Enemy GetNearestEnemy()
    {
        Enemy nearest = null;
        float nearestDistance = float.PositiveInfinity;
        Vector3 from = transform.position;

        foreach (var enemy in G.Enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled)
                continue;

            float distance = (enemy.transform.position - from).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private void Shoot()
    {
        var target = GetNearestEnemy();
        var from = transform.position;
        Vector3 direction;
        if (target != null)
        {
            direction = target.transform.position - from;
        }
        else
        {
            var camera = Camera.main;
            var mousePosition = Input.mousePosition;
            mousePosition.z = from.z - camera.transform.position.z;
            direction = camera.ScreenToWorldPoint(mousePosition) - from;
        }

        direction.z = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.right;

        if (!_bulletContainer.TryShoot(out var bulletData))
            return;

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
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            Shoot();
        }
    }
}
