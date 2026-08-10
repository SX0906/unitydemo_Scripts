using UnityEngine;

/// <summary>
/// 主菜单的 1V1 / 1V2 / 1V4 点击选择模式脚本。
/// UI 搭建后把三个选项按钮的 onClick 分别绑定到 SelectMode1V1/1V2/1V4。
/// </summary>
public class GameModeSelector : MonoBehaviour
{
    public void SelectMode1V1()
    {
        SelectMode(GameModeSettings.Mode1V1);
    }

    public void SelectMode1V2()
    {
        SelectMode(GameModeSettings.Mode1V2);
    }

    public void SelectMode1V4()
    {
        SelectMode(GameModeSettings.Mode1V4);
    }

    public void SelectMode(int mode)
    {
        GameModeSettings.SetMode(mode);

        var dropdown = FindFirstObjectByType<ModeDropdown>();
        if (dropdown != null)
            dropdown.OnModeSelected(GameModeSettings.CurrentMode);
    }
}
