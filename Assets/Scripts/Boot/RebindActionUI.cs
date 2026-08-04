using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// 单个按键的改键 UI 行。挂在每个 ActionRow Prefab 上。
/// 通过 Initialize() 注入 InputAction，不依赖 .asset 文件。
/// </summary>
public class RebindActionUI : MonoBehaviour
{
    [Header("要改键的 Action（由 SettingsPanel 注入）")]
    public InputAction action;

    [Header("绑定索引（一个 Action 可能有多个绑定，通常 0 是主按键）")]
    public int bindingIndex = 0;

    [Header("显示名称")]
    public string displayName;

    [Header("UI 组件")]
    public TextMeshProUGUI actionLabel;
    public Button rebindButton;
    public TextMeshProUGUI bindingText;
    public Button resetButton;

    private string _saveKey;

    public void Initialize(InputAction action, int bindingIndex, string displayName)
    {
        this.action = action;
        this.bindingIndex = bindingIndex;
        this.displayName = displayName;

        _saveKey = $"rebind_{action.actionMap.name}_{action.name}_{bindingIndex}";
        actionLabel.text = displayName;

        LoadBinding();
        UpdateBindingDisplay();

        rebindButton.onClick.AddListener(StartRebinding);
        resetButton.onClick.AddListener(ResetBinding);
    }

    private void LoadBinding()
    {
        string saved = PlayerPrefs.GetString(_saveKey, "");
        if (!string.IsNullOrEmpty(saved))
        {
            action.ApplyBindingOverride(bindingIndex, saved);
        }
    }

    private void UpdateBindingDisplay()
    {
        if (action != null)
            bindingText.text = action.GetBindingDisplayString(bindingIndex);
    }

    private void StartRebinding()
    {
        bindingText.text = "等待输入...";
        action.Disable();

        var rebind = action.PerformInteractiveRebinding(bindingIndex)
            .OnComplete(operation =>
            {
                string path = operation.action.bindings[bindingIndex].effectivePath;
                PlayerPrefs.SetString(_saveKey, path);
                PlayerPrefs.Save();

                bindingText.text = action.GetBindingDisplayString(bindingIndex);
                operation.Dispose();
                action.Enable();
            })
            .OnCancel(operation =>
            {
                UpdateBindingDisplay();
                operation.Dispose();
                action.Enable();
            })
            .WithControlsExcluding("Mouse")
            .WithControlsExcluding("<Keyboard>/escape");

        rebind.Start();
    }

    private void ResetBinding()
    {
        action.RemoveBindingOverride(bindingIndex);
        PlayerPrefs.DeleteKey(_saveKey);
        PlayerPrefs.Save();
        UpdateBindingDisplay();
    }
}
