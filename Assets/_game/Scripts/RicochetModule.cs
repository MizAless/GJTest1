using System.Linq;
using UnityEngine;

public class RicochetModule : BulletModule
{
    private int _maxHitCount;

    private int _hitCount = 0;
    
    public RicochetModule(int maxHitCount)
    {
        _maxHitCount = maxHitCount;
    }

    public override void OnHitEnemy(Enemy enemy, ref bool shouldDestroy)
    {
        var orderedEnemies = G.Enemies.OrderBy(e => Vector3.Distance(e.transform.position, Bullet.transform.position)).ToList(); 
        
        if (orderedEnemies.Count < 2)
            return;

        var closestNextEnemy = orderedEnemies[1];
        
        var direction = (closestNextEnemy.transform.position - Bullet.transform.position).normalized;
        Bullet.ChangeDirection(direction);

        _hitCount++;

        shouldDestroy = _hitCount >= _maxHitCount;
    }

    public override void OnReset()
    {
        _hitCount = 0;
    }
}