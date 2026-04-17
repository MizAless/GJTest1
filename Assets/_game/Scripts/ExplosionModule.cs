using UnityEngine;

public class ExplosionModule : BulletModule
{
    public override void OnHitEnemy(Enemy enemy, ref bool shouldDestroy)
    {
        Object.Instantiate(G.DataBase.ExplosionPrefab, enemy.transform.position, Quaternion.identity);
    }
}