using UnityEngine;
using UnityEngine.SceneManagement;

public class EntryPoint : MonoBehaviour
{
    [SerializeField] Explosion _explosionPrefab;
    [SerializeField] EnemySpawner _enemySpawner;
    [SerializeField] UpgradeWindowView _upgradeWindowView;
    void Awake()
    {
        Time.timeScale=1;
        G.Init(_enemySpawner,_explosionPrefab);
        if(_upgradeWindowView!=null) _upgradeWindowView.gameObject.SetActive(false);
        var run=gameObject.AddComponent<RunDirector>(); run.Initialize(_enemySpawner);
    }
    void Update()
    {
        if(!Input.GetKeyDown(KeyCode.R)) return;
        Time.timeScale=1; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
