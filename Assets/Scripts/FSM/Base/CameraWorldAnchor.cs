using UnityEngine;

/// <summary>
/// 把相机维持在以玩家为中心的固定相对位置。挂在运镜相机上，或直接父节点到玩家下更稳。
/// </summary>
public class CameraWorldAnchor : MonoBehaviour
{
    public Transform target;
    public Vector3 relativeOffset = new Vector3(0, 2, -4);
    public float lookAtHeight = 1f;

    private void Update()
    {
        if (target == null) return;
        transform.position = target.position
            + target.right   * relativeOffset.x
            + target.up      * relativeOffset.y
            + target.forward * relativeOffset.z;
        transform.LookAt(target.position + Vector3.up * lookAtHeight);
    }
}
