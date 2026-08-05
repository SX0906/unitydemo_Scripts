using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using GameInput;

[RequireComponent(typeof(TestFSM))]
public class PauseMenu : MonoBehaviour
{
    private TestFSM testFSM;
    private bool isPaused;
    private GameObject pausePanel;
    private TextMeshProUGUI fontSample;
    private bool previousCursorVisible = true;
    private CursorLockMode previousLockState = CursorLockMode.None;

    private void Awake()
    {
        testFSM = GetComponent<TestFSM>();
        fontSample = FindFirstObjectByType<TextMeshProUGUI>();
        BuildPauseUI();
        pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetPaused(!isPaused);
    }

    public void SetPaused(bool paused)
    {
        if (isPaused == paused) return;

        if (paused)
        {
            previousCursorVisible = Cursor.visible;
            previousLockState = Cursor.lockState;
        }

        isPaused = paused;
        pausePanel.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
        Cursor.visible = paused ? true : previousCursorVisible;
        Cursor.lockState = paused ? CursorLockMode.None : previousLockState;
        testFSM.SetInputActive(!paused);
    }

    public void Resume() => SetPaused(false);

    public void Restart()
    {
        Time.timeScale = 1f;
        testFSM.SetInputActive(true);
        SceneManager.LoadScene(SceneNames.Gameplay);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        testFSM.SetInputActive(true);
        SceneManager.LoadScene(SceneNames.MainMenu);
    }

    private void BuildPauseUI()
    {
        EnsureEventSystem();

        var canvasGo = new GameObject(
            "PauseMenuCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var panelGo = new GameObject("PausePanel", typeof(RectTransform));
        panelGo.transform.SetParent(canvasGo.transform, false);

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        panelGo.AddComponent<CanvasRenderer>();
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f);

        pausePanel = panelGo;

        CreateText(panelRect, "PauseTitle", "PAUSED", new Vector2(0f, 180f), 40);
        CreateButton(panelRect, "ResumeButton", "RESUME", new Vector2(0f, 60f), Resume);
        CreateButton(panelRect, "RestartButton", "RESTART", new Vector2(0f, -40f), Restart);
        CreateButton(panelRect, "MainMenuButton", "MAIN MENU", new Vector2(0f, -140f), ReturnToMainMenu);
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        var eventSystemGo = new GameObject("PauseMenuEventSystem");
        eventSystemGo.AddComponent<EventSystem>();

        var inputModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();
        inputModule.actionsAsset = new PlayerControl().asset;
    }

    private Button CreateButton(
        RectTransform parent,
        string name,
        string label,
        Vector2 anchoredPosition,
        UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(280f, 52f);
        rect.anchoredPosition = anchoredPosition;

        go.AddComponent<CanvasRenderer>();
        var image = go.AddComponent<Image>();
        image.color = new Color(0.16f, 0.18f, 0.24f, 0.95f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        CreateText(rect, name + "Text", label, Vector2.zero, 28f);
        return button;
    }

    private TextMeshProUGUI CreateText(
        RectTransform parent,
        string name,
        string label,
        Vector2 anchoredPosition,
        float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(600f, 60f);
        rect.anchoredPosition = anchoredPosition;

        go.AddComponent<CanvasRenderer>();
        var text = go.AddComponent<TextMeshProUGUI>();
        ApplyFont(text);
        text.text = label;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        return text;
    }

    private void ApplyFont(TextMeshProUGUI text)
    {
        if (fontSample != null)
        {
            text.font = fontSample.font;
            text.fontSharedMaterial = fontSample.fontSharedMaterial;
        }
        else if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSharedMaterial = TMP_Settings.defaultFontAsset.material;
        }
    }
}
