using UnityEngine;

/// <summary>
/// Power 期间禁用正常相机控制器，Timeline 结束后恢复。
/// 运镜由 Timeline 的 Cinemachine Track 管理。
/// </summary>
public class PowerCameraTimelineController : MonoBehaviour
{
    [Tooltip("玩家身上的 FSMCamera 或 PlayerCameraController")]
    public MonoBehaviour normalCameraController;

    public void OnPowerStart()
    {
        Debug.Log("[PowerCam] 禁用正常相机控制器");
        if (normalCameraController != null)
            normalCameraController.enabled = false;
    }

    public void OnPowerExit()
    {
        Debug.Log("[PowerCam] 恢复正常相机控制器");
        if (normalCameraController != null)
            normalCameraController.enabled = true;
    }

    private void OnDestroy()
    {
        if (normalCameraController != null)
            normalCameraController.enabled = true;
    }
}
