using UnityEngine;

/// <summary>
/// 下拉箭头旋转辅助：点击按钮调用 ToggleArrow() 切换 ▼ / ▲。
/// </summary>
public class DropdownArrow : MonoBehaviour
{
    public RectTransform arrow;
    public bool isOpen;

    private void Awake()
    {
        if (arrow == null)
            arrow = GetComponent<RectTransform>();
        if (arrow != null)
            arrow.pivot = new Vector2(0.5f, 0.5f);
        ApplyRotation();
    }

    public void ToggleArrow()
    {
        isOpen = !isOpen;
        ApplyRotation();
    }

    public void SetOpen(bool open)
    {
        isOpen = open;
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        if (arrow == null) return;
        arrow.localRotation = Quaternion.Euler(0f, 0f, isOpen ? 180f : 0f);
    }
}
