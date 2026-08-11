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
        HitStopManager.Reset();
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
            ApplyGameMode();

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

    private void ApplyGameMode()
    {
        int mode = GameModeSettings.CurrentMode;

        bool enemy02 = mode == GameModeSettings.Mode1V2 || mode == GameModeSettings.Mode1V4;
        bool enemy03 = mode == GameModeSettings.Mode1V4;
        bool enemy04 = mode == GameModeSettings.Mode1V4;
        bool hud = mode == GameModeSettings.Mode1V1;

        SetActiveInScene("Enemy02", enemy02);
        SetActiveInScene("Enemy03", enemy03);
        SetActiveInScene("Enemy04", enemy04);
        SetActiveInScene("EnemyHUD", hud);
    }

    private static void SetActiveInScene(string objectName, bool active)
    {
        GameObject target = FindInSceneIncludingInactive(objectName);
        if (target == null)
        {
            Debug.LogWarning($"[GameManager] 场景中找不到 {objectName}");
            return;
        }

        target.SetActive(active);
    }

    private static GameObject FindInSceneIncludingInactive(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            Transform found = FindInChildren(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }

    private static Transform FindInChildren(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindInChildren(parent.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
