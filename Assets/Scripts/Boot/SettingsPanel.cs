using UnityEngine;
using UnityEngine.InputSystem;
using GameInput;

/// <summary>
/// 按键设置面板。挂在 MainMenu 场景的 SettingsPanel 物体上。
/// 负责创建改键行，注入 InputAction，控制开关。
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("主菜单面板（打开设置时隐藏）")]
    [SerializeField] private GameObject mainMenuPanel;

    [Header("改键行预制体")]
    [SerializeField] private RebindActionUI rebindRowPrefab;

    [Header("行容器")]
    [SerializeField] private Transform rowsContainer;

    private PlayerControl _playerControl;

    private void Start()
    {
        _playerControl = new PlayerControl();

        // 列出需要改键的操作
        AddRebindRow(_playerControl.Player.Attack,   0, "攻击");
        AddRebindRow(_playerControl.Player.RAtk,     0, "升龙");
        AddRebindRow(_playerControl.Player.Dodge,    0, "闪避");
        AddRebindRow(_playerControl.Player.Jump,     0, "跳跃");
        AddRebindRow(_playerControl.Player.Run,      0, "奔跑");
        AddRebindRow(_playerControl.Player.LockOn,   0, "锁定");
        AddRebindRow(_playerControl.Player.Power,    0, "Power");
        AddRebindRow(_playerControl.Player.Guard,    0, "防御");
    }

    private void AddRebindRow(InputAction action, int bindingIndex, string displayName)
    {
        var row = Instantiate(rebindRowPrefab, rowsContainer);
        row.Initialize(action, bindingIndex, displayName);
    }

    public void Open()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    private void OnDestroy()
    {
        _playerControl?.Dispose();
    }
}
