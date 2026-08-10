using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 主菜单模式下拉面板：开合面板、同步箭头、更新当前模式文字和按钮高亮。
/// </summary>
public class ModeDropdown : MonoBehaviour
{
    [Header("面板")]
    public GameObject dropdownPanel;

    [Header("箭头")]
    public DropdownArrow arrow;

    [Header("顶部文字")]
    public TextMeshProUGUI modeText;

    [Header("模式按钮")]
    public Button option1V1;
    public Button option1V2;
    public Button option1V4;

    [Header("高亮颜色")]
    public Color selectedColor = new Color(0.2f, 0.5f, 1f, 1f);
    public Color normalColor = Color.white;

    private bool isPanelOpen;

    private void Start()
    {
        RefreshModeUI(GameModeSettings.CurrentMode);
        ClosePanel();
    }

    public void TogglePanel()
    {
        isPanelOpen = !isPanelOpen;
        ApplyPanelState();
    }

    public void ClosePanel()
    {
        isPanelOpen = false;
        ApplyPanelState();
    }

    public void OnModeSelected(int mode)
    {
        RefreshModeUI(mode);
        ClosePanel();
    }

    private void RefreshModeUI(int mode)
    {
        if (modeText != null)
        {
            modeText.text = mode == GameModeSettings.Mode1V1
                ? "current mode: 1V1"
                : mode == GameModeSettings.Mode1V2
                    ? "current mode: 1V2"
                    : "current mode: 1V4";
        }

        SetButtonColor(option1V1, mode == GameModeSettings.Mode1V1);
        SetButtonColor(option1V2, mode == GameModeSettings.Mode1V2);
        SetButtonColor(option1V4, mode == GameModeSettings.Mode1V4);
    }

    private void SetButtonColor(Button button, bool selected)
    {
        if (button == null) return;

        Color color = selected ? selectedColor : normalColor;
        if (button.targetGraphic != null)
            button.targetGraphic.color = color;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = color;
        colors.pressedColor = color;
        colors.selectedColor = color;
        button.colors = colors;
    }

    private void ApplyPanelState()
    {
        if (dropdownPanel != null)
            dropdownPanel.SetActive(isPanelOpen);

        if (arrow != null)
            arrow.SetOpen(isPanelOpen);
    }
}
