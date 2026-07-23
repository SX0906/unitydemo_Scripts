using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 命中特效生成器。
///
/// 挂载位置：
/// 与 WeaponHitDetector / EnemyWeaponHitDetector 和武器 Collider
/// 挂在同一个武器 GameObject 上。
///
/// 朝向规则：
/// 特效本地 Z+ 轴 = 刀刃命中位置的运动轨迹方向
/// 特效本地 Y+ 轴 = 刀根到刀尖方向在轨迹垂直平面上的投影
/// </summary>
public class HitEffectSpawner : MonoBehaviour
{
    [Header("命中特效预制件")]
    public GameObject hitEffectPrefab;

    [Header("刀刃轨迹挂点")]
    [Tooltip("刀刃靠近刀柄的位置")]
    public Transform bladeBase;

    [Tooltip("刀尖位置")]
    public Transform bladeTip;

    [Tooltip("轨迹位移小于该值时，使用最近一次有效轨迹")]
    [Min(0.00001f)]
    public float minTrajectoryDistance = 0.001f;

    [Tooltip("是否将刀刃运动方向投影到敌人表面。一般刀光关闭，贴着表面飞散的火花可以开启")]
    public bool projectTrajectoryOntoSurface;

    [Header("生成参数")]
    [Tooltip("沿敌人表面法线向外移动的距离")]
    public float surfaceOffset = 0.02f;

    [FormerlySerializedAs("randomAngleRange")]
    [Tooltip("仅绕轨迹方向随机滚转，不会改变轨迹方向")]
    [Range(0f, 180f)]
    public float randomRollRange;

    [Tooltip("随机缩放范围")]
    public Vector2 randomScaleRange = new Vector2(0.9f, 1.1f);

    [Tooltip("特效存活时间，0 表示不自动销毁")]
    public float lifetime = 3f;

    [Header("特效本地轴修正")]
    [Tooltip(
        "用于修正特效预制体自身的本地朝向。" +
        "先使用 (0,0,0)；如果方向相反可尝试 (0,180,0)"
    )]
    public Vector3 effectEulerOffset;

    [Header("朝向模式")]
    public OrientationMode orientationMode =
        OrientationMode.FaceWeaponVelocity;

    public enum OrientationMode
    {
        /// <summary>
        /// 特效 Z+ 对齐刀刃命中位置的运动轨迹。
        /// </summary>
        FaceWeaponVelocity,

        /// <summary>
        /// 特效 Z+ 对齐敌人表面法线。
        /// </summary>
        FaceNormal
    }

    [Header("调试")]
    [SerializeField]
    private bool showDebugRay;

    [Tooltip("调试射线显示时间")]
    [SerializeField]
    private float debugRayDuration = 2f;

    private Collider _weaponCollider;

    // 上一个完整动画帧的刀刃位置
    private Vector3 _previousBladeBase;
    private Vector3 _previousBladeTip;

    // 最近一个完整动画帧的刀刃位置
    private Vector3 _currentBladeBase;
    private Vector3 _currentBladeTip;

    // 最近一次有效的刀刃运动方向
    private Vector3 _lastValidTrajectory;

    private bool _trajectoryInitialized;
    private bool _hasTrajectoryHistory;
    private bool _missingBladePointWarningShown;

    private void Awake()
    {
        _weaponCollider = GetComponent<Collider>();

        if (_weaponCollider == null)
        {
            Debug.LogError(
                "HitEffectSpawner：当前武器物体上没有 Collider。",
                this
            );
        }
    }

    private void OnEnable()
    {
        ResetTrajectorySamples();
    }

    /// <summary>
    /// 在 LateUpdate 记录 Animator 完成本帧姿态后的刀刃位置。
    /// </summary>
    private void LateUpdate()
    {
        if (!TryGetLiveBladeSegment(
                out Vector3 newBase,
                out Vector3 newTip))
        {
            _trajectoryInitialized = false;
            _hasTrajectoryHistory = false;
            return;
        }

        if (!_trajectoryInitialized)
        {
            InitializeTrajectory(newBase, newTip);
            return;
        }

        _previousBladeBase = _currentBladeBase;
        _previousBladeTip = _currentBladeTip;

        _currentBladeBase = newBase;
        _currentBladeTip = newTip;

        _hasTrajectoryHistory = true;

        // 优先使用刀尖位移。
        // 旋转挥砍时刀柄移动可能很小，但刀尖运动最明显。
        Vector3 tipDelta =
            _currentBladeTip - _previousBladeTip;

        Vector3 previousMiddle =
            (_previousBladeBase + _previousBladeTip) * 0.5f;

        Vector3 currentMiddle =
            (_currentBladeBase + _currentBladeTip) * 0.5f;

        Vector3 middleDelta =
            currentMiddle - previousMiddle;

        Vector3 candidate =
            tipDelta.sqrMagnitude >= middleDelta.sqrMagnitude
                ? tipDelta
                : middleDelta;

        if (IsValidTrajectory(candidate))
        {
            _lastValidTrajectory = candidate.normalized;
        }
    }

    /// <summary>
    /// 由 WeaponHitDetector / EnemyWeaponHitDetector
    /// 在 OnTriggerEnter 中调用。
    /// </summary>
    public GameObject SpawnAtContact(Collider targetCollider)
    {
        if (hitEffectPrefab == null)
        {
            Debug.LogWarning(
                "HitEffectSpawner：没有设置 hitEffectPrefab。",
                this
            );

            return null;
        }

        if (targetCollider == null || _weaponCollider == null)
            return null;

        if (!TryGetLiveBladeSegment(
                out Vector3 liveBladeBase,
                out Vector3 liveBladeTip))
        {
            if (!_missingBladePointWarningShown)
            {
                Debug.LogWarning(
                    "HitEffectSpawner：没有设置 BladeBase 或 BladeTip，" +
                    "无法根据刀刃轨迹生成命中特效。",
                    this
                );

                _missingBladePointWarningShown = true;
            }

            return null;
        }

        CalculateContact(
            targetCollider,
            liveBladeBase,
            liveBladeTip,
            out Vector3 contactPoint,
            out Vector3 hitNormal
        );

        Vector3 trajectoryDirection =
            ComputeBladeTrajectory(
                contactPoint,
                hitNormal,
                liveBladeBase,
                liveBladeTip,
                out Vector3 bladeDirection
            );

        Vector3 facingDirection;

        switch (orientationMode)
        {
            case OrientationMode.FaceNormal:
                facingDirection = hitNormal;
                break;

            case OrientationMode.FaceWeaponVelocity:
            default:
                facingDirection = trajectoryDirection;
                break;
        }

        if (facingDirection.sqrMagnitude < 0.0001f)
            facingDirection = hitNormal;

        facingDirection.Normalize();

        Vector3 effectUp = BuildStableUpDirection(
            facingDirection,
            bladeDirection,
            hitNormal
        );

        Quaternion rotation = Quaternion.LookRotation(
            facingDirection,
            effectUp
        );

        // 只绕特效本地 Z 轴随机滚转。
        // 不随机 X/Y，因此不会破坏轨迹方向。
        if (randomRollRange > 0f)
        {
            float randomRoll = Random.Range(
                -randomRollRange,
                randomRollRange
            );

            rotation *= Quaternion.AngleAxis(
                randomRoll,
                Vector3.forward
            );
        }

        // 修正特效预制体自身的本地轴。
        rotation *= Quaternion.Euler(effectEulerOffset);

        Vector3 spawnPosition =
            contactPoint + hitNormal * surfaceOffset;

        float minimumScale = Mathf.Min(
            randomScaleRange.x,
            randomScaleRange.y
        );

        float maximumScale = Mathf.Max(
            randomScaleRange.x,
            randomScaleRange.y
        );

        float scale = Random.Range(
            minimumScale,
            maximumScale
        );

        GameObject instance = Instantiate(
            hitEffectPrefab,
            spawnPosition,
            rotation
        );

        instance.transform.localScale *= scale;

        if (lifetime > 0f)
        {
            Destroy(instance, lifetime);
        }

#if UNITY_EDITOR
        if (showDebugRay)
        {
            // 红色：敌人表面法线
            Debug.DrawRay(
                contactPoint,
                hitNormal * 0.5f,
                Color.red,
                debugRayDuration
            );

            // 黄色：实际刀刃轨迹
            Debug.DrawRay(
                contactPoint,
                trajectoryDirection * 0.8f,
                Color.yellow,
                debugRayDuration
            );

            // 蓝色：刀根到刀尖方向
            Debug.DrawRay(
                contactPoint,
                bladeDirection * 0.6f,
                Color.blue,
                debugRayDuration
            );

            // 绿色：最终特效朝向
            Debug.DrawRay(
                contactPoint,
                facingDirection * 0.8f,
                Color.green,
                debugRayDuration
            );
        }
#endif

        return instance;
    }

    /// <summary>
    /// 重置刀刃位置历史。
    /// </summary>
    private void ResetTrajectorySamples()
    {
        _trajectoryInitialized = false;
        _hasTrajectoryHistory = false;
        _lastValidTrajectory = Vector3.zero;

        if (TryGetLiveBladeSegment(
                out Vector3 currentBase,
                out Vector3 currentTip))
        {
            InitializeTrajectory(
                currentBase,
                currentTip
            );
        }
    }

    private void InitializeTrajectory(
        Vector3 currentBase,
        Vector3 currentTip)
    {
        _previousBladeBase = currentBase;
        _previousBladeTip = currentTip;

        _currentBladeBase = currentBase;
        _currentBladeTip = currentTip;

        _trajectoryInitialized = true;
        _hasTrajectoryHistory = false;
    }

    private bool TryGetLiveBladeSegment(
        out Vector3 currentBase,
        out Vector3 currentTip)
    {
        if (bladeBase == null || bladeTip == null)
        {
            currentBase = Vector3.zero;
            currentTip = Vector3.zero;
            return false;
        }

        currentBase = bladeBase.position;
        currentTip = bladeTip.position;

        return true;
    }

    /// <summary>
    /// 计算命中点与敌人表面法线。
    ///
    /// Trigger 没有真实 ContactPoint，因此先取得刀刃上距离
    /// 敌人中心最近的点，再结合 ClosestPoint 和
    /// Physics.ComputePenetration 估算。
    /// </summary>
    private void CalculateContact(
        Collider targetCollider,
        Vector3 currentBase,
        Vector3 currentTip,
        out Vector3 contactPoint,
        out Vector3 hitNormal)
    {
        Vector3 targetCenter =
            targetCollider.bounds.center;

        Vector3 bladePoint = ClosestPointOnSegment(
            currentBase,
            currentTip,
            targetCenter
        );

        contactPoint =
            targetCollider.ClosestPoint(bladePoint);

        bool hasPenetration = Physics.ComputePenetration(
            _weaponCollider,
            _weaponCollider.transform.position,
            _weaponCollider.transform.rotation,
            targetCollider,
            targetCollider.transform.position,
            targetCollider.transform.rotation,
            out Vector3 separationDirection,
            out float separationDistance
        );

        if (hasPenetration &&
            separationDirection.sqrMagnitude > 0.0001f)
        {
            // ComputePenetration 返回：
            // 武器 Collider 离开目标 Collider 所需移动的方向。
            // 该方向可以作为目标表面的朝外方向。
            hitNormal = separationDirection.normalized;
        }
        else
        {
            hitNormal = bladePoint - contactPoint;

            if (hitNormal.sqrMagnitude < 0.0001f)
            {
                hitNormal =
                    bladePoint - targetCenter;
            }

            if (hitNormal.sqrMagnitude < 0.0001f)
            {
                hitNormal =
                    _weaponCollider.bounds.center -
                    targetCollider.bounds.center;
            }

            if (hitNormal.sqrMagnitude < 0.0001f)
            {
                hitNormal = Vector3.up;
            }

            hitNormal.Normalize();
        }

        // 当刀刃已经进入敌人 Collider 时，
        // ClosestPoint 可能直接返回刀刃内部位置。
        // 从表面外侧沿法线反向 Raycast，重新取得表面点。
        if (hasPenetration)
        {
            float rayDistance =
                targetCollider.bounds.extents.magnitude +
                _weaponCollider.bounds.extents.magnitude +
                Mathf.Max(separationDistance, 0.5f);

            Vector3 rayOrigin =
                bladePoint + hitNormal * rayDistance;

            Ray ray = new Ray(
                rayOrigin,
                -hitNormal
            );

            if (targetCollider.Raycast(
                    ray,
                    out RaycastHit hit,
                    rayDistance * 2f))
            {
                contactPoint = hit.point;

                if (hit.normal.sqrMagnitude > 0.0001f)
                {
                    Vector3 raycastNormal =
                        hit.normal.normalized;

                    // 保证 Raycast 法线与已计算的外侧方向一致。
                    if (Vector3.Dot(
                            raycastNormal,
                            hitNormal) < 0f)
                    {
                        raycastNormal = -raycastNormal;
                    }

                    hitNormal = raycastNormal;
                }
            }
        }
    }

    /// <summary>
    /// 根据刀刃上接近命中点的位置，计算该位置的运动轨迹。
    /// 这能够同时包含刀柄平移和武器旋转。
    /// </summary>
    private Vector3 ComputeBladeTrajectory(
        Vector3 contactPoint,
        Vector3 hitNormal,
        Vector3 liveBladeBase,
        Vector3 liveBladeTip,
        out Vector3 bladeDirection)
    {
        Vector3 liveBladeVector =
            liveBladeTip - liveBladeBase;

        if (liveBladeVector.sqrMagnitude < 0.0001f)
        {
            bladeDirection = transform.up;
        }
        else
        {
            bladeDirection =
                liveBladeVector.normalized;
        }

        Vector3 previousBase;
        Vector3 previousTip;
        Vector3 currentBase;
        Vector3 currentTip;

        if (_trajectoryInitialized)
        {
            bool livePoseMatchesCachedPose =
                (liveBladeBase - _currentBladeBase)
                    .sqrMagnitude < 0.0000001f &&
                (liveBladeTip - _currentBladeTip)
                    .sqrMagnitude < 0.0000001f;

            if (livePoseMatchesCachedPose &&
                _hasTrajectoryHistory)
            {
                // 当前 Transform 还没有进入新动画姿态，
                // 使用最近两个完整 LateUpdate 采样。
                previousBase = _previousBladeBase;
                previousTip = _previousBladeTip;

                currentBase = _currentBladeBase;
                currentTip = _currentBladeTip;
            }
            else
            {
                // Animator 已进入新姿态，但 LateUpdate 尚未采样。
                // 使用最近缓存姿态到当前实时姿态。
                previousBase = _currentBladeBase;
                previousTip = _currentBladeTip;

                currentBase = liveBladeBase;
                currentTip = liveBladeTip;
            }
        }
        else
        {
            previousBase = liveBladeBase;
            previousTip = liveBladeTip;

            currentBase = liveBladeBase;
            currentTip = liveBladeTip;
        }

        Vector3 currentBladeVector =
            currentTip - currentBase;

        float bladePosition = 0.5f;

        if (currentBladeVector.sqrMagnitude > 0.0001f)
        {
            bladePosition = Vector3.Dot(
                contactPoint - currentBase,
                currentBladeVector
            ) / currentBladeVector.sqrMagnitude;

            bladePosition =
                Mathf.Clamp01(bladePosition);
        }

        Vector3 previousContactPosition =
            Vector3.Lerp(
                previousBase,
                previousTip,
                bladePosition
            );

        Vector3 currentContactPosition =
            Vector3.Lerp(
                currentBase,
                currentTip,
                bladePosition
            );

        Vector3 trajectory =
            currentContactPosition -
            previousContactPosition;

        trajectory = ProcessTrajectoryProjection(
            trajectory,
            hitNormal
        );

        if (IsValidTrajectory(trajectory))
        {
            _lastValidTrajectory =
                trajectory.normalized;

            return _lastValidTrajectory;
        }

        // Physics Trigger 和 Animator 的更新时机不同，
        // 本次采样可能恰好为零，因此使用最近一次有效轨迹。
        if (_lastValidTrajectory.sqrMagnitude > 0.0001f)
        {
            Vector3 cachedTrajectory =
                ProcessTrajectoryProjection(
                    _lastValidTrajectory,
                    hitNormal
                );

            if (cachedTrajectory.sqrMagnitude > 0.0001f)
            {
                return cachedTrajectory.normalized;
            }
        }

        // 最后的确定性回退：
        // 使用“表面法线 × 刀刃方向”得到表面切线，
        // 不再使用角色 root.forward。
        Vector3 fallback = Vector3.Cross(
            hitNormal,
            bladeDirection
        );

        if (fallback.sqrMagnitude < 0.0001f)
        {
            fallback = Vector3.Cross(
                bladeDirection,
                Vector3.up
            );
        }

        if (fallback.sqrMagnitude < 0.0001f)
        {
            fallback = Vector3.Cross(
                bladeDirection,
                Vector3.right
            );
        }

        return fallback.normalized;
    }

    private Vector3 ProcessTrajectoryProjection(
        Vector3 trajectory,
        Vector3 hitNormal)
    {
        if (!projectTrajectoryOntoSurface)
            return trajectory;

        Vector3 projected = Vector3.ProjectOnPlane(
            trajectory,
            hitNormal
        );

        return projected;
    }

    /// <summary>
    /// 计算稳定的特效本地 Y 轴。
    /// 优先让它与刀根到刀尖方向一致。
    /// </summary>
    private static Vector3 BuildStableUpDirection(
        Vector3 facingDirection,
        Vector3 bladeDirection,
        Vector3 hitNormal)
    {
        Vector3 upDirection = Vector3.ProjectOnPlane(
            bladeDirection,
            facingDirection
        );

        if (upDirection.sqrMagnitude < 0.0001f)
        {
            upDirection = Vector3.ProjectOnPlane(
                hitNormal,
                facingDirection
            );
        }

        if (upDirection.sqrMagnitude < 0.0001f)
        {
            upDirection = Vector3.ProjectOnPlane(
                Vector3.up,
                facingDirection
            );
        }

        if (upDirection.sqrMagnitude < 0.0001f)
        {
            upDirection = Vector3.ProjectOnPlane(
                Vector3.right,
                facingDirection
            );
        }

        return upDirection.normalized;
    }

    private bool IsValidTrajectory(
        Vector3 trajectory)
    {
        float minimumDistance =
            Mathf.Max(
                minTrajectoryDistance,
                0.00001f
            );

        return trajectory.sqrMagnitude >=
               minimumDistance * minimumDistance;
    }

    private static Vector3 ClosestPointOnSegment(
        Vector3 segmentStart,
        Vector3 segmentEnd,
        Vector3 point)
    {
        Vector3 segment =
            segmentEnd - segmentStart;

        float lengthSqr =
            segment.sqrMagnitude;

        if (lengthSqr < 0.000001f)
            return segmentStart;

        float t = Vector3.Dot(
            point - segmentStart,
            segment
        ) / lengthSqr;

        t = Mathf.Clamp01(t);

        return segmentStart + segment * t;
    }
}