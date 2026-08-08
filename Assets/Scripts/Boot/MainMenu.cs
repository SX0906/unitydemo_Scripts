using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("测试模式")]
    [SerializeField] private Toggle testModeToggle;

    private void Start()
    {
        if (testModeToggle == null) return;

        bool saved = PlayerPrefs.GetInt("TestMode", 0) == 1;
        EnemyFSM.TestModeEnabled = saved;
        testModeToggle.SetIsOnWithoutNotify(saved);
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneNames.Gameplay);
    }

    /// <summary>测试模式 Toggle 的 onValueChanged 回调</summary>
    public void OnTestModeToggled(bool value)
    {
        EnemyFSM.TestModeEnabled = value;
        PlayerPrefs.SetInt("TestMode", value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
