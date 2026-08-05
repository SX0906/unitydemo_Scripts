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
        // 同步静态状态到 UI
        if (testModeToggle != null)
            testModeToggle.isOn = EnemyFSM.TestModeEnabled;
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
    }
}