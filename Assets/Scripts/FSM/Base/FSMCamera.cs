using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// FSMCamera — 战斗相机控制器（方案 B：操控 Cinemachine3rdPersonFollow）
/// 
/// 挂载位置：Player 物体（与 TestFSM 同级）
/// 锁定时通过 Cinemachine 的 ShoulderOffset / CameraDistance / FOV 控制构图
/// </summary>
public class FSMCamera : MonoBehaviour
{
    [Header("参考目标")]
    [Tooltip("拖 LookRoot 空物体")]
    public Transform cameraTarget;

    [Header("Cinemachine 引用")]
    [Tooltip("拖场景中的 Cinemachine Virtual Camera")]
    public CinemachineCamera vcam;

    [Tooltip("拖 VCam 上的 Cinemachine3rdPersonFollow 组件")]
    public CinemachineThirdPersonFollow thirdPersonFollow;

    [Header("角度限制")]
    public float topClamp = 70f;
    public float bottomClamp = -30f;

    [Header("灵敏度")]
    public float lookSensitivity = 1f;

    [Header("锁定")]
    public bool isLockOn;
    public Transform lockOnTarget;
    public float lockRotateSpeed = 10f;

    [Header("锁定 — Look 点计算")]
    [Tooltip("中点上方基础抬升高度（米）")]
    public float baseLookHeight = 1f;

    [Tooltip("超过此距离开始额外抬升 look 点")]
    public float distanceThresholdForLift = 5f;

    [Tooltip("距离每超 1 单位 look 点额外抬高")]
    public float liftPerUnit = 0.4f;

    [Tooltip("额外抬升上限")]
    public float maxLift = 4f;

    [Tooltip("lift 平滑速度")]
    public float liftSmoothSpeed = 5f;

    [Header("锁定 — 构图偏移")]
    [Tooltip("look 方向往左偏，让画面不居中")]
    public float combatLookOffsetAngle = 5f;

    [Header("动态 CameraDistance")]
    [Tooltip("最远距离时的 CameraDistance")]
    public float farCameraDistance = 8f;

    [Header("动态 ShoulderOffset（高度）")]
    [Tooltip("远距离时 ShoulderOffset.y 增加多少（让相机更高，俯视感更强）")]
    public float farShoulderOffsetYBoost = 2f;

    [Header("动态 FOV")]
    public float closeFOV = 72f;
    public float farFOV = 50f;

    [Header("动态变换阈值")]
    [Tooltip("距离 ≤ 此值使用近距离参数")]
    public float minDistanceThreshold = 3f;

    [Tooltip("距离 ≥ 此值使用远距离参数")]
    public float maxDistanceThreshold = 15f;

    [Header("平滑速度")]
    public float distanceSmoothSpeed = 6f;
    public float fovSmoothSpeed = 6f;
    public float shoulderSmoothSpeed = 5f;

    // --- 运行时缓存的 Cinemachine 默认值（锁定解除后恢复） ---
    private float _defaultFOV;
    private float _defaultCameraDistance;
    private Vector3 _defaultShoulderOffset;

    // --- 内部平滑状态 ---
    private Vector2 lookInput;
    private float yaw;
    private float pitch;
    private float currentLift;
    private float currentFOV;
    private float currentCameraDistance;
    private Vector3 currentShoulderOffset;

    private const float InputThreshold = 0.01f;

    private void Start()
    {
        if (!cameraTarget)
        {
            Debug.LogError("FSMCamera：cameraTarget（LookRoot）没有设置。", this);
            enabled = false;
            return;
        }

        if (!vcam)
            vcam = GetComponentInChildren<CinemachineCamera>();

        if (vcam && !thirdPersonFollow)
            thirdPersonFollow = vcam.GetComponent<CinemachineThirdPersonFollow>();

        // 从 Cinemachine 读取当前默认值
        if (vcam)
        {
            _defaultFOV = vcam.Lens.FieldOfView;
            currentFOV  = _defaultFOV;
        }
        else
        {
            _defaultFOV = 60f;
            currentFOV  = 60f;
        }

        if (thirdPersonFollow)
        {
            _defaultCameraDistance  = thirdPersonFollow.CameraDistance;
            _defaultShoulderOffset  = thirdPersonFollow.ShoulderOffset;
            currentCameraDistance   = _defaultCameraDistance;
            currentShoulderOffset   = _defaultShoulderOffset;
        }
        else
        {
            _defaultCameraDistance  = 4f;
            _defaultShoulderOffset  = new Vector3(0f, -0.24f, 0.5f);
            currentCameraDistance   = 4f;
            currentShoulderOffset   = _defaultShoulderOffset;
        }

        Vector3 euler = cameraTarget.rotation.eulerAngles;
        yaw   = euler.y;
        pitch = euler.x;
    }

    private void LateUpdate()
    {
        // —— 1. 更新 LookRoot 旋转 ——
        if (isLockOn && lockOnTarget)
            UpdateCombatLook();
        else
            UpdateFreeLook();

        cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // —— 2. 将动态值写回 Cinemachine ——
        ApplyCinemachineParams();
    }

    // ================= 自由视角 =================

    private void UpdateFreeLook()
    {
        currentLift = Mathf.Lerp(currentLift, 0f, liftSmoothSpeed * Time.deltaTime);

        if (lookInput.sqrMagnitude >= InputThreshold)
        {
            yaw   += lookInput.x * lookSensitivity;
            pitch -= lookInput.y * lookSensitivity;
        }

        yaw   = ClampAngle(yaw,   float.MinValue, float.MaxValue);
        pitch = ClampAngle(pitch, bottomClamp,      topClamp);

        // 平滑恢复默认值
        currentFOV            = Mathf.Lerp(currentFOV,            _defaultFOV,             fovSmoothSpeed      * Time.deltaTime);
        currentCameraDistance  = Mathf.Lerp(currentCameraDistance,  _defaultCameraDistance,  distanceSmoothSpeed * Time.deltaTime);
        currentShoulderOffset  = Vector3.Lerp(currentShoulderOffset, _defaultShoulderOffset, shoulderSmoothSpeed * Time.deltaTime);
    }

    // ================= 锁定视角 =================

    private void UpdateCombatLook()
    {
        Vector3 playerPos = cameraTarget.position;
        Vector3 enemyPos  = lockOnTarget.position;

        Vector3 toEnemy = enemyPos - playerPos;
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude < 0.0001f) return;
        float distance = toEnemy.magnitude;

        // --- look 点：中点 + 1m 基础 + 距离额外抬升 ---
        Vector3 midPoint = (playerPos + enemyPos) * 0.5f + Vector3.up * baseLookHeight;

        float targetLift = 0f;
        if (distance > distanceThresholdForLift)
            targetLift = Mathf.Min((distance - distanceThresholdForLift) * liftPerUnit, maxLift);
        currentLift = Mathf.Lerp(currentLift, targetLift, liftSmoothSpeed * Time.deltaTime);

        Vector3 liftedLookPoint = midPoint + Vector3.up * currentLift;

        // --- yaw / pitch ---
        Vector3 baseLookDir = (liftedLookPoint - playerPos).normalized;
        Quaternion yawOffset = Quaternion.Euler(0f, -combatLookOffsetAngle, 0f);
        Vector3 offsetDir = yawOffset * baseLookDir;

        float targetYaw   = Mathf.Atan2(offsetDir.x, offsetDir.z) * Mathf.Rad2Deg;
        float targetPitch = Mathf.Asin(Mathf.Clamp(offsetDir.y, -1f, 1f)) * Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(targetPitch, bottomClamp, topClamp);

        yaw   = Mathf.LerpAngle(yaw,   targetYaw,   lockRotateSpeed * Time.deltaTime);
        pitch = Mathf.Lerp(pitch, targetPitch, lockRotateSpeed * Time.deltaTime);

        // --- 动态 FOV ---
        float t = Mathf.Clamp01((distance - minDistanceThreshold) / (maxDistanceThreshold - minDistanceThreshold));
        float targetFOV = Mathf.Lerp(closeFOV, farFOV, t);
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, fovSmoothSpeed * Time.deltaTime);

        // --- 动态 CameraDistance ---
        float targetDist = Mathf.Lerp(_defaultCameraDistance, farCameraDistance, t);
        currentCameraDistance = Mathf.Lerp(currentCameraDistance, targetDist, distanceSmoothSpeed * Time.deltaTime);

        // --- 动态 ShoulderOffset（高度跟随距离） ---
        float targetShoulderY = _defaultShoulderOffset.y + (farShoulderOffsetYBoost * t);
        Vector3 targetShoulder = new Vector3(
            _defaultShoulderOffset.x,
            targetShoulderY,
            _defaultShoulderOffset.z
        );
        currentShoulderOffset = Vector3.Lerp(currentShoulderOffset, targetShoulder, shoulderSmoothSpeed * Time.deltaTime);
    }

    // ================= 写回 Cinemachine =================

    private void ApplyCinemachineParams()
    {
        if (vcam)
            vcam.Lens.FieldOfView = currentFOV;

        if (thirdPersonFollow)
        {
            thirdPersonFollow.CameraDistance = currentCameraDistance;
            thirdPersonFollow.ShoulderOffset = currentShoulderOffset;
        }
    }

    // ================= 输入 =================

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    // ================= 工具 =================

    private static float ClampAngle(float angle, float min, float max)
    {
        while (angle < -360f) angle += 360f;
        while (angle > 360f)  angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }
}