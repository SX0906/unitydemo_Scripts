using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour, IGameFlow
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        HitStopManager_test.Reset();
        LoadScene(SceneNames.Gameplay);
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneNames.Gameplay)
        {
            // 自动绑定玩家 UI
            var player = FindFirstObjectByType<TestFSM_test>();
            var playerUI = FindFirstObjectByType<PlayerVitalsUI_test>();
            if (player != null && playerUI != null)
                playerUI.Bind(player.GetComponent<PlayerVitals>());

            // 自动绑定敌人 UI（场景中可能有多个敌人）
            var enemyFSMs = FindObjectsByType<EnemyFSM_test>(FindObjectsSortMode.None);
            var enemyUIs = FindObjectsByType<EnemyVitalsUI_test>(FindObjectsSortMode.None);
            for (int i = 0; i < enemyFSMs.Length && i < enemyUIs.Length; i++)
            {
                var vitals = enemyFSMs[i].GetComponent<EnemyVitals>();
                if (vitals != null) enemyUIs[i].Bind(vitals);
            }
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
