using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] float _spawnTime = .2f;
    [SerializeField] Enemy _enemyPrefab;
    [SerializeField] float _spawnRadius = 7;
    [SerializeField] Transform _spawnPoint;
    public bool IsSpawning { get; private set; }
    public IEnumerator SpawnPack(int[] health)
    {
        IsSpawning=true;
        float baseAngle=Random.value*360;
        for(int i=0;i<health.Length;i++)
        {
            float angle=(baseAngle+i*360f/health.Length)*Mathf.Deg2Rad;
            Vector3 origin=_spawnPoint!=null?_spawnPoint.position:Vector3.zero;
            Vector3 position=origin+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*_spawnRadius;
            var enemy=Instantiate(_enemyPrefab,position,Quaternion.identity);
            enemy.Configure(health[i]); G.Enemies.Add(enemy);
            yield return new WaitForSeconds(Mathf.Min(_spawnTime,.2f));
        }
        IsSpawning=false;
    }
    public void CallSpawn(int enemyCount)
    {
        var health=new int[enemyCount]; for(int i=0;i<health.Length;i++) health[i]=30;
        StartCoroutine(SpawnPack(health));
    }
}
