using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private GameObject gameManagerPrefab;

    private void Start()
    {
        if (GameManager.Instance == null && gameManagerPrefab != null)
        {
            Instantiate(gameManagerPrefab);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadScene(SceneNames.MainMenu);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.MainMenu);
        }
    }
}
