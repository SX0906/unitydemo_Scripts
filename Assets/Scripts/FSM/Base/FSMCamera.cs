using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// FSMCamera — 战斗相机控制器（基于 PlayerCameraController 增强）
/// 
/// 战斗中（锁定目标时）不再直接看向敌人，而是计算偏移后的目标方向，
/// 确保玩家和敌人同时出现在镜头内，并根据距离动态抬高视角。
/// </summary>
public class FSMCamera : MonoBehaviour
{
    [Header("相机跟踪目标，拖 LookRoot")]
    public Transform cameraTarget;

    [Header("角度限制")]
    public float topClamp = 70f;
    public float bottomClamp = -30f;

    [Header("灵敏度")]
    public float lookSensitivity = 1f;

    [Header("锁定")]
    public bool isLockOn;
    public Transform lockOnTarget;
    public float lockRotateSpeed = 10f;

    [Header("战斗构图")]
    [Tooltip("look 方向绕 Y 轴往左偏移角度（度），让画面不居中而更有纵深感")]
    public float combatLookOffsetAngle = 5f;

    [Tooltip("相机侧位偏移角度（度），往右偏，与 look 左偏形成互补构图")]
    public float combatCameraSideOffsetAngle = 5f;

    [Tooltip("超过此距离开始抬升镜头")]
    public float distanceThresholdForLift = 8f;

    [Tooltip("距离每超过阈值 1 单位，look 点抬高多少")]
    public float liftPerUnit = 0.3f;

    [Tooltip("抬升高度的上限")]
    public float maxLift = 3f;

    [Tooltip("抬升平滑速度")]
    public float liftSmoothSpeed = 5f;

    private Vector2 lookInput;
    private float yaw;
    private float pitch;

    private float currentLift;
    private const float threshold = 0.01f;

    private void Start()
    {
        if (!cameraTarget)
        {
            Debug.LogError("FSMCamera：cameraTarget 没有设置。", this);
            enabled = false;
            return;
        }

        Vector3 euler = cameraTarget.rotation.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
    }

    private void LateUpdate()
    {
        if (!cameraTarget)
            return;

        if (isLockOn && lockOnTarget)
            UpdateCombatLook();
        else
            UpdateFreeLook();

        cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void UpdateFreeLook()
    {
        currentLift = Mathf.Lerp(currentLift, 0f, liftSmoothSpeed * Time.deltaTime);

        if (lookInput.sqrMagnitude >= threshold)
        {
            yaw += lookInput.x * lookSensitivity;
            pitch -= lookInput.y * lookSensitivity;
        }

        yaw = ClampAngle(yaw, float.MinValue, float.MaxValue);
        pitch = ClampAngle(pitch, bottomClamp, topClamp);
    }

    private void UpdateCombatLook()
    {
        Vector3 playerPos = cameraTarget.position;
        Vector3 enemyPos = lockOnTarget.position;

        // 1. 计算水平距离，近距离无意义时跳过
        Vector3 toEnemy = enemyPos - playerPos;
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude < 0.0001f)
            return;
        float distance = toEnemy.magnitude;

        // 2. 计算中点
        Vector3 midPoint = (playerPos + enemyPos) * 0.5f;

        // 3. 距离过大时抬高 look 点
        float targetLift = 0f;
        if (distance > distanceThresholdForLift)
        {
            targetLift = Mathf.Min((distance - distanceThresholdForLift) * liftPerUnit, maxLift);
        }
        currentLift = Mathf.Lerp(currentLift, targetLift, liftSmoothSpeed * Time.deltaTime);

        // 4. 合成最终 look 点（中点 + 高度抬升）
        Vector3 liftedLookPoint = midPoint + Vector3.up * currentLift;

        // 5. 计算从玩家到 look 点的基础方向
        Vector3 baseLookDir = (liftedLookPoint - playerPos).normalized;

        // 6. look 方向往左偏移 combatLookOffsetAngle 度
        Quaternion lookYawOffset = Quaternion.Euler(0f, -combatLookOffsetAngle, 0f);
        Vector3 offsetLookDir = lookYawOffset * baseLookDir;

        // 7. 从偏移后方向提取目标 yaw / pitch
        float targetYaw = Mathf.Atan2(offsetLookDir.x, offsetLookDir.z) * Mathf.Rad2Deg;

        // 8. 叠加相机侧位偏移（往右偏），与 look 左偏形成互补构图
        targetYaw += combatCameraSideOffsetAngle;

        float targetPitch = Mathf.Asin(Mathf.Clamp(offsetLookDir.y, -1f, 1f)) * Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(targetPitch, bottomClamp, topClamp);

        yaw = Mathf.LerpAngle(yaw, targetYaw, lockRotateSpeed * Time.deltaTime);
        pitch = Mathf.Lerp(pitch, targetPitch, lockRotateSpeed * Time.deltaTime);
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    private static float ClampAngle(float angle, float min, float max)
    {
        while (angle < -360f)
            angle += 360f;
        while (angle > 360f)
            angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }
}
