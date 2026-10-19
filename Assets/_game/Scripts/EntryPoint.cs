using UnityEngine;

using UnityEngine.SceneManagement;

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

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.R))
            return;

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
