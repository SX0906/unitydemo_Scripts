using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
     public void StartGame()
     {
         Time.timeScale = 1f;
         SceneManager.LoadScene(SceneNames.Gameplay);
     }

    // public void StartGame()
    // {
    //     // 找场景里的 GameManager（Boot 注入的）
    //     GameManager.Instance?.StartGame();
    // }
}
