using System.Collections;

public class GameScript
{
    public IEnumerator Start()
    {
        G.EnemySpawner.CallSpawn(6);
        yield return null;
        yield return IsEnemiesCountBiggerThen(0);
        G.UpgradeGiver.ChooseUpgrade();

    }
    
    public IEnumerator IsEnemiesCountBiggerThen(int amount)
    {
        while (G.Enemies.Count > amount)
        {
            yield return null;
        }
    }
}