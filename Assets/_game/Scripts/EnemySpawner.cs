using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private float _spawnTime = 0.2f;
    [SerializeField] private Enemy _enemyPrefab;
    [SerializeField] private float _spawnRadius = 7;
    [SerializeField] private Transform _spawnPoint;
    private const float RowSpacing = 0.8f;
    private const float ColumnAngle = 14f;
    private int _spawnedGroups;
    public bool IsSpawning { get; private set; }
    public IEnumerator SpawnPack(int[] health, List<Enemy> members = null)
    {
        if (IsSpawning)
        {
            yield break;
        }
        IsSpawning = true;
        Shooter shooter = FindAnyObjectByType<Shooter>();
        Vector3 origin = Vector3.zero;
        if (shooter != null)
        {
            origin = shooter.transform.position;
        }
        else if (_spawnPoint != null)
        {
            origin = _spawnPoint.position;
        }
        // Radial files keep targets aligned from the player's viewpoint, allowing real piercing hits.
        int columns = 1;
        if (_spawnedGroups % 2 == 1 || health.Length > 6)
        {
            columns = 2;
        }
        _spawnedGroups++;
        float baseAngle = Random.value * 360f;
        int rows = Mathf.CeilToInt((float)health.Length / columns);
        float frontRadius = Mathf.Max(3f, _spawnRadius - (rows - 1) * RowSpacing);
        for (int index = 0; index < health.Length; index++)
        {
            int column = index % columns;
            int row = index / columns;
            float angle = (baseAngle + (column - (columns - 1) * 0.5f) * ColumnAngle) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            Vector3 position = origin + direction * (frontRadius + row * RowSpacing);
            if (StaticShooterStage.Instance != null)
            {
                // Three advancing files, with depth-separated rows for piercing shots.
                int lane = index % 3;
                position = new Vector3(-6.5f + lane * 6.5f + Random.Range(-0.4f, 0.4f),
                    0, 19 + index / 3 * 3.5f);
            }
            Enemy enemy = Instantiate(_enemyPrefab, position, Quaternion.identity);
            enemy.Configure(health[index]);
            if (StaticShooterStage.Instance != null)
            {
                enemy.gameObject.AddComponent<ApproachingEnemyVisual>();
            }
            G.Enemies.Add(enemy);
            members?.Add(enemy);
            if (index < health.Length - 1)
            {
                yield return new WaitForSeconds(Mathf.Min(_spawnTime, 0.2f));
            }
        }
        IsSpawning = false;
    }
    public void CallSpawn(int enemyCount)
    {
        int[] health = new int[enemyCount];
        for (int index = 0; index < health.Length; index++)
        {
            health[index] = 30;
        }
        StartCoroutine(SpawnPack(health));
    }
}
