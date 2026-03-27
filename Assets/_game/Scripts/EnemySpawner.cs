using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private float _spawnTime = 1;
    [SerializeField] private Enemy _enemyPrefab;
    [SerializeField] private float _spawnRadius;
    [SerializeField] private Transform _spawnPoint;
    private void Start()
    {
        StartCoroutine(Spawning());
    }

    private IEnumerator Spawning()
    {
        while (true)
        {
            var randomPosition2d = Random.insideUnitCircle;
            randomPosition2d.Normalize();
            randomPosition2d *= _spawnRadius;
            var randomPosition = new Vector3(randomPosition2d.x, randomPosition2d.y, 0) + _spawnPoint.position;
            
            Instantiate(_enemyPrefab,  randomPosition, transform.rotation);
            yield return new WaitForSeconds(_spawnTime);
        }
    }
}