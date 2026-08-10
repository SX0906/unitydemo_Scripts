using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// FSMCamera - 战斗相机控制器（操控 CinemachineThirdPersonFollow）
///
/// 挂载位置：Player 物体（与 TestFSM 同级）
/// 锁定时通过 Cinemachine 的 ShoulderOffset / CameraDistance / FOV 控制构图。
/// </summary>
public class FSMCamera : MonoBehaviour
{
    [Header("参考目标")]
    [Tooltip("拖 LookRoot 空物体")]
    public Transform cameraTarget;

    [Header("Cinemachine 引用")]
    [Tooltip("拖场景中的 Cinemachine Virtual Camera")]
    public CinemachineCamera vcam;

    [Tooltip("拖 VCam 上的 CinemachineThirdPersonFollow 组件")]
    public CinemachineThirdPersonFollow thirdPersonFollow;

    [Header("角度限制")]
    public float topClamp = 65f;
    public float bottomClamp = -30f;

    [Header("灵敏度")]
    public float lookSensitivity = 0.2f;

    [Header("锁定")]
    public bool isLockOn;
    public Transform lockOnTarget;
    public float lockRotateSpeed = 6f;

    [Header("软锁定镜头辅助")]
    private Transform softLockAssistTarget;
    [Tooltip("软锁定镜头转向速度")]
    public float softLockRotateSpeed = 4f;
    [Range(0f, 0.25f)]
    [Tooltip("目标进入视口多少范围后停止辅助")]
    public float softLockViewMargin = 0.3f;

    [Header("锁定 - Look 点计算")]
    [Tooltip("中点上方基础抬升高度（米）")]
    public float baseLookHeight = 1f;

    [Tooltip("超过此距离开始额外抬升 look 点")]
    public float distanceThresholdForLift = 1.8f;

    [Tooltip("距离每超 1 单位 look 点额外抬高")]
    public float liftPerUnit = 0.5f;

    [Tooltip("额外抬升上限")]
    public float maxLift = 2.5f;

    [Tooltip("lift 平滑速度")]
    public float liftSmoothSpeed = 5f;

    [Header("锁定 - 构图偏移")]
    [Tooltip("look 方向往左偏，让画面不居中")]
    public float combatLookOffsetAngle = 5f;

    [Header("动态 CameraDistance")]
    [Tooltip("最远距离时的 CameraDistance")]
    public float farCameraDistance = 7f;

    [Header("动态 ShoulderOffset（高度）")]
    [Tooltip("远距离时 ShoulderOffset.y 增加多少")]
    public float farShoulderOffsetYBoost = 1f;

    [Header("动态 FOV")]
    public float closeFOV = 72f;
    public float farFOV = 50f;

    [Header("动态变换阈值")]
    [Tooltip("距离小于或等于该值时使用近距离参数")]
    public float minDistanceThreshold = 3f;

    [Tooltip("距离大于或等于该值时使用远距离参数")]
    public float maxDistanceThreshold = 15f;

    [Header("锁定 - 高度差自适应")]
    [Tooltip("超过该高度差开始自动拉远/压低镜头")]
    public float heightDiffThreshold = 2f;

    [Tooltip("达到该高度差后效果完全生效")]
    public float heightDiffMax = 8f;

    [Tooltip("高度差最大时额外拉远的 CameraDistance")]
    public float heightExtraDistance = 4f;

    [Tooltip("高度差最大时镜头至少下压到的俯角（度）")]
    public float heightLookDownPitch = 15f;

    [Tooltip("高度差自适应拉远的上限")]
    public float maxAutoCameraDistance = 12f;

    [Tooltip("高度差自适应过渡平滑速度")]
    public float heightSmoothSpeed = 4f;

    [Header("平滑速度")]
    public float distanceSmoothSpeed = 6f;
    public float fovSmoothSpeed = 6f;
    public float shoulderSmoothSpeed = 5f;

    [Header("滚轮缩放")]
    [Tooltip("摄像机允许靠近角色的最小距离")]
    public float minZoomDistance = 0.1f;

    [Tooltip("摄像机允许远离角色的最大距离")]
    public float maxZoomDistance = 6.5f;

    [Tooltip("鼠标滚轮每次滚动改变的距离")]
    public float zoomStep = 0.5f;

    // 运行时缓存的 Cinemachine 默认值
    private float _defaultFOV;
    private float _defaultCameraDistance;
    private Vector3 _defaultShoulderOffset;

    // 内部平滑状态
    private Vector2 lookInput;
    private float yaw;
    private float pitch;
    private float currentLift;
    private float currentHeightFactor;
    private float currentFOV;
    private float currentCameraDistance;
    private Vector3 currentShoulderOffset;

    // 玩家通过滚轮选择的基础镜头距离
    private float targetZoomDistance;

    private const float InputThreshold = 0.01f;

    private void Start()
    {
        if (!cameraTarget)
        {
            Debug.LogError(
                "FSMCamera：cameraTarget（LookRoot）没有设置。",
                this);

            enabled = false;
            return;
        }

        if (!vcam)
            vcam = GetComponentInChildren<CinemachineCamera>();

        if (vcam && !thirdPersonFollow)
            thirdPersonFollow =
                vcam.GetComponent<CinemachineThirdPersonFollow>();

        // 从 Cinemachine 读取当前默认值
        if (vcam)
        {
            _defaultFOV = vcam.Lens.FieldOfView;
            currentFOV = _defaultFOV;
        }
        else
        {
            _defaultFOV = 60f;
            currentFOV = 60f;
        }

        if (thirdPersonFollow)
        {
            _defaultCameraDistance =
                thirdPersonFollow.CameraDistance;

            _defaultShoulderOffset =
                thirdPersonFollow.ShoulderOffset;

            currentCameraDistance =
                _defaultCameraDistance;

            currentShoulderOffset =
                _defaultShoulderOffset;
        }
        else
        {
            _defaultCameraDistance = 4f;
            _defaultShoulderOffset =
                new Vector3(0f, -0.24f, 0.5f);

            currentCameraDistance =
                _defaultCameraDistance;

            currentShoulderOffset =
                _defaultShoulderOffset;
        }

        // 以 Cinemachine 当前距离作为滚轮缩放初始距离
        targetZoomDistance = Mathf.Clamp(
            _defaultCameraDistance,
            minZoomDistance,
            maxZoomDistance);

        currentCameraDistance = targetZoomDistance;

        Vector3 euler = cameraTarget.rotation.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
    }

    private void LateUpdate()
    {
        // 1. 更新 LookRoot 旋转和镜头参数
        if (isLockOn && lockOnTarget)
            UpdateCombatLook();
        else if (softLockAssistTarget != null)
            UpdateSoftLockAssist();
        else
            UpdateFreeLook();

        cameraTarget.rotation =
            Quaternion.Euler(pitch, yaw, 0f);

        // 2. 将动态参数写回 Cinemachine
        ApplyCinemachineParams();
    }

    // ================= 自由视角 =================

    private void UpdateFreeLook()
    {
        ResetToFreeLookParams();

        if (lookInput.sqrMagnitude >= InputThreshold)
        {
            yaw += lookInput.x * lookSensitivity;
            pitch -= lookInput.y * lookSensitivity;
        }

        yaw = ClampAngle(
            yaw,
            float.MinValue,
            float.MaxValue);

        pitch = ClampAngle(
            pitch,
            bottomClamp,
            topClamp);
    }

    private void ResetToFreeLookParams()
    {
        currentLift = Mathf.Lerp(
            currentLift,
            0f,
            liftSmoothSpeed * Time.deltaTime);

        // FOV 和肩部偏移恢复默认值
        currentFOV = Mathf.Lerp(
            currentFOV,
            _defaultFOV,
            fovSmoothSpeed * Time.deltaTime);

        // 镜头距离移动到玩家通过滚轮选择的距离
        currentCameraDistance = Mathf.Lerp(
            currentCameraDistance,
            targetZoomDistance,
            distanceSmoothSpeed * Time.deltaTime);

        currentShoulderOffset = Vector3.Lerp(
            currentShoulderOffset,
            _defaultShoulderOffset,
            shoulderSmoothSpeed * Time.deltaTime);
    }

    // ================= 软锁定镜头辅助 =================

    private void UpdateSoftLockAssist()
    {
        Camera cam = Camera.main;
        if (cam == null || softLockAssistTarget == null || cameraTarget == null)
        {
            StopSoftLockAssist();
            UpdateFreeLook();
            return;
        }

        Vector3 targetPos = softLockAssistTarget.position + Vector3.up * baseLookHeight;
        Vector3 viewport = cam.WorldToViewportPoint(targetPos);
        float margin = softLockViewMargin;

        bool inView = viewport.z > 0f &&
            viewport.x > margin && viewport.x < 1f - margin &&
            viewport.y > margin && viewport.y < 1f - margin;

        if (inView)
        {
            StopSoftLockAssist();
            UpdateFreeLook();
            return;
        }

        Vector3 toTarget = targetPos - cameraTarget.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            UpdateFreeLook();
            return;
        }

        float targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        float flatDist = toTarget.magnitude;
        float vertical = targetPos.y - cameraTarget.position.y;
        float targetPitch = Mathf.Atan2(vertical, flatDist) * Mathf.Rad2Deg;

        yaw = Mathf.LerpAngle(yaw, targetYaw, softLockRotateSpeed * Time.deltaTime);
        pitch = Mathf.Lerp(pitch, targetPitch, softLockRotateSpeed * Time.deltaTime);
        pitch = Mathf.Clamp(pitch, bottomClamp, topClamp);

        ResetToFreeLookParams();
    }

    public void StartSoftLockAssist(Transform target)
    {
        if (isLockOn || target == null) return;
        softLockAssistTarget = target;
    }

    public void StopSoftLockAssist()
    {
        softLockAssistTarget = null;
    }

    // ================= 锁定视角 =================

    private void UpdateCombatLook()
    {
        Vector3 playerPos = cameraTarget.position;
        Vector3 enemyPos = lockOnTarget.position;

        Vector3 toEnemy = enemyPos - playerPos;
        toEnemy.y = 0f;

        if (toEnemy.sqrMagnitude < 0.0001f)
            return;

        float distance = toEnemy.magnitude;

        float heightDiff = Mathf.Abs(enemyPos.y - playerPos.y);
        float heightRange = Mathf.Max(heightDiffMax - heightDiffThreshold, 0.001f);
        float targetHeightFactor = heightDiff <= heightDiffThreshold
            ? 0f
            : Mathf.Clamp01((heightDiff - heightDiffThreshold) / heightRange);
        currentHeightFactor = Mathf.Lerp(
            currentHeightFactor,
            targetHeightFactor,
            heightSmoothSpeed * Time.deltaTime);

        // Look 点：中点 + 基础高度 + 距离额外抬升
        Vector3 midPoint =
            (playerPos + enemyPos) * 0.5f +
            Vector3.up * baseLookHeight;

        float targetLift = 0f;

        if (distance > distanceThresholdForLift)
        {
            targetLift = Mathf.Min(
                (distance - distanceThresholdForLift) *
                liftPerUnit,
                maxLift);
        }

        currentLift = Mathf.Lerp(
            currentLift,
            targetLift,
            liftSmoothSpeed * Time.deltaTime);

        Vector3 liftedLookPoint =
            midPoint + Vector3.up * currentLift;

        // 计算锁定状态下的 yaw 和 pitch
        Vector3 baseLookDir =
            (liftedLookPoint - playerPos).normalized;

        Quaternion yawOffset = Quaternion.Euler(
            0f,
            -combatLookOffsetAngle,
            0f);

        Vector3 offsetDir = yawOffset * baseLookDir;

        float targetYaw =
            Mathf.Atan2(offsetDir.x, offsetDir.z) *
            Mathf.Rad2Deg;

        float targetPitch =
            Mathf.Asin(
                Mathf.Clamp(offsetDir.y, -1f, 1f)) *
            Mathf.Rad2Deg;

        float heightLookDownBoost =
            currentHeightFactor * heightLookDownPitch;

        targetPitch = Mathf.Max(targetPitch, heightLookDownBoost);
        targetPitch = Mathf.Clamp(
            targetPitch,
            bottomClamp,
            topClamp);

        yaw = Mathf.LerpAngle(
            yaw,
            targetYaw,
            lockRotateSpeed * Time.deltaTime);

        pitch = Mathf.Lerp(
            pitch,
            targetPitch,
            lockRotateSpeed * Time.deltaTime);

        float thresholdRange =
            maxDistanceThreshold - minDistanceThreshold;

        float t = thresholdRange > 0.001f
            ? Mathf.Clamp01(
                (distance - minDistanceThreshold) /
                thresholdRange)
            : 0f;

        // 动态 FOV
        float targetFOV = Mathf.Lerp(
            closeFOV,
            farFOV,
            t);

        currentFOV = Mathf.Lerp(
            currentFOV,
            targetFOV,
            fovSmoothSpeed * Time.deltaTime);

        // 原有的锁定自动距离
        float automaticDistance = Mathf.Lerp(
            _defaultCameraDistance,
            farCameraDistance,
            t);

        // 将玩家滚轮设置作为相对默认距离的偏移
        float playerZoomOffset =
            targetZoomDistance -
            _defaultCameraDistance;

        float heightDistanceBoost =
            currentHeightFactor * heightExtraDistance;

        float targetDist = Mathf.Clamp(
            automaticDistance + playerZoomOffset + heightDistanceBoost,
            minZoomDistance,
            Mathf.Max(maxZoomDistance, maxAutoCameraDistance));

        currentCameraDistance = Mathf.Lerp(
            currentCameraDistance,
            targetDist,
            distanceSmoothSpeed * Time.deltaTime);

        // 动态 ShoulderOffset
        float targetShoulderY =
            _defaultShoulderOffset.y +
            farShoulderOffsetYBoost * t;

        Vector3 targetShoulder = new Vector3(
            _defaultShoulderOffset.x,
            targetShoulderY,
            _defaultShoulderOffset.z);

        currentShoulderOffset = Vector3.Lerp(
            currentShoulderOffset,
            targetShoulder,
            shoulderSmoothSpeed * Time.deltaTime);
    }

    // ================= 写回 Cinemachine =================

    private void ApplyCinemachineParams()
    {
        if (vcam)
            vcam.Lens.FieldOfView = currentFOV;

        if (thirdPersonFollow)
        {
            thirdPersonFollow.CameraDistance =
                currentCameraDistance;

            thirdPersonFollow.ShoulderOffset =
                currentShoulderOffset;
        }
    }

    // ================= 输入 =================

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    public void OnZoom(InputValue value)
    {
        Vector2 scrollInput = value.Get<Vector2>();
        float scrollY = scrollInput.y;

        // Value 类型的输入归零时也可能触发回调
        if (Mathf.Abs(scrollY) < InputThreshold)
            return;

        // 正数表示滚轮向上：减少距离，使镜头靠近角色
        targetZoomDistance = Mathf.Clamp(
            targetZoomDistance -
            Mathf.Sign(scrollY) * zoomStep,
            minZoomDistance,
            maxZoomDistance);
    }

    // ================= 工具 =================

    private static float ClampAngle(
        float angle,
        float min,
        float max)
    {
        while (angle < -360f)
            angle += 360f;

        while (angle > 360f)
            angle -= 360f;

        return Mathf.Clamp(angle, min, max);
    }
}
