using UnityEngine;

public class EntryPoint : MonoBehaviour
{
    [SerializeField] private Explosion _explosionPrefab;
    [SerializeField] private EnemySpawner _enemySpawner;
    
    [Header("UI")] 
    [SerializeField] private UpgradeWindowView _upgradeWindowView;
    
    private GameScript _gameScript = new GameScript();
    
    private void Awake()
    {
        G.Init(_enemySpawner, _explosionPrefab);

        StartCoroutine(_gameScript.Start());
    }
}