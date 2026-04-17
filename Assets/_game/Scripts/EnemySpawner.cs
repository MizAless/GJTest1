using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private float _spawnTime = 1;
    [SerializeField] private Enemy _enemyPrefab;
    [SerializeField] private float _spawnRadius;
    [SerializeField] private Transform _spawnPoint;
    
    private int _plannedSpawnCount = 0;

    private void Start()
    {
        StartCoroutine(Spawning());
    }

    private IEnumerator Spawning()
    {
        while (true)
        {
            if (_plannedSpawnCount == 0)
            {
                yield return new WaitForSeconds(_spawnTime);
                continue;
            }
            
            var randomPosition2d = Random.insideUnitCircle;
            randomPosition2d.Normalize();
            randomPosition2d *= _spawnRadius;
            var randomPosition = new Vector3(randomPosition2d.x, randomPosition2d.y, 0) + _spawnPoint.position;
            
            var newEnemy = Instantiate(_enemyPrefab,  randomPosition, transform.rotation);
            G.Enemies.Add(newEnemy);
            _plannedSpawnCount--;
            
            yield return new WaitForSeconds(_spawnTime);
        }
    }
    
    public void CallSpawn(int enemyCount)
    {
        _plannedSpawnCount += enemyCount;
    }
}