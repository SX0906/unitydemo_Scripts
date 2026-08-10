using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("测试模式")]
    [SerializeField] private Toggle testModeToggle;

    private void Start()
    {
        if (testModeToggle == null) return;

        PlayerPrefs.SetInt("TestMode", 0);
        PlayerPrefs.Save();
        EnemyFSM.TestModeEnabled = false;
        testModeToggle.SetIsOnWithoutNotify(false);
    }

    public void StartGame()
    {
        Time.timeScale = 1f;

        if (GameManager.Instance == null)
        {
            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<GameManager>();
        }

        GameManager.Instance.StartGame();
    }

    /// <summary>测试模式 Toggle 的 onValueChanged 回调</summary>
    public void OnTestModeToggled(bool value)
    {
        EnemyFSM.TestModeEnabled = value;
        PlayerPrefs.SetInt("TestMode", value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
