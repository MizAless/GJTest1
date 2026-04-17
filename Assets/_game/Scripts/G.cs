using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine.PlayerLoop;

public static class G
{
    public static List<Enemy> Enemies = new List<Enemy>();
    public static DataBase DataBase = new DataBase();
    
    public static EnemySpawner EnemySpawner;
    
    public static UpgradeGiver UpgradeGiver;
    
    public static void Init(EnemySpawner enemySpawner, Explosion explosionPrefab)
    {
        EnemySpawner = enemySpawner;
        DataBase.ExplosionPrefab = explosionPrefab;
        
        UpgradeGiver = new UpgradeGiver();

        Clear();
    }
    
    private static void Clear()
    {
        Enemies.Clear();
    }
}

public class DataBase
{
    public Explosion ExplosionPrefab;
}