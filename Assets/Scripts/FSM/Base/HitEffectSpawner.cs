using UnityEngine;

/// <summary>
/// 命中特效生成器 —— 挂在武器 GameObject 上（与 WeaponHitDetector 同级）。
/// 命中时在武器与敌人碰撞体的最近接触点生成特效，
/// 朝向基于武器瞬时运动速度的切平面投影（保证挥砍特效沿刀刃运动方向）。
/// </summary>
public class HitEffectSpawner : MonoBehaviour
{
    [Header("命中特效预制件")]
    public GameObject hitEffectPrefab;

    [Header("生成参数")]
    [Tooltip("沿命中法线外移的距离")]
    public float surfaceOffset = 0.05f;
    [Tooltip("随机角度偏移范围（度）")]
    [Range(0f, 180f)] public float randomAngleRange = 15f;
    [Tooltip("随机缩放范围")]
    public Vector2 randomScaleRange = new Vector2(0.9f, 1.1f);
    [Tooltip("特效存活时间，0 表示不自动销毁")]
    public float lifetime = 3f;

    [Header("朝向模式")]
    public OrientationMode orientationMode = OrientationMode.FaceWeaponVelocity;

    public enum OrientationMode
    {
        /// <summary>特效 Z+ 对齐武器瞬时速度在表面切平面上的投影方向</summary>
        FaceWeaponVelocity,
        /// <summary>特效 Z+ 对齐命中法线</summary>
        FaceNormal
    }

    // ===== 速度追踪 =====
    private Vector3 _prevPosition;
    private Vector3 _weaponVelocity;
    private Collider _weaponCollider;

    [Header("调试")]
    [SerializeField] private bool _showDebugRay;

    private void Awake()
    {
        _weaponCollider = GetComponent<Collider>();
        _prevPosition = transform.position;
    }

    private void Update()
    {
        Vector3 currentPos = transform.position;
        _weaponVelocity = (currentPos - _prevPosition) / Time.deltaTime;
        _prevPosition = currentPos;
    }

    /// <summary>
    /// 由 WeaponHitDetector / EnemyWeaponHitDetector 在 OnTriggerEnter 中调用。
    /// </summary>
    public GameObject SpawnAtContact(Collider targetCollider)
    {
        if (hitEffectPrefab == null || targetCollider == null || _weaponCollider == null)
            return null;

        // ---- 1. 计算接触点：敌人碰撞体上离武器最近的点 ----
        Vector3 weaponCenter = _weaponCollider.bounds.center;
        Vector3 contactPoint = targetCollider.ClosestPoint(weaponCenter);

        // 回退：包围盒最近点
        if (Vector3.Distance(contactPoint, weaponCenter) > 3f || float.IsNaN(contactPoint.x))
            contactPoint = targetCollider.ClosestPointOnBounds(weaponCenter);

        // ---- 2. 命中法线（从敌人表面指向武器） ----
        Vector3 hitNormal = (weaponCenter - contactPoint).normalized;
        if (hitNormal.magnitude < 0.01f)
            hitNormal = -transform.root.forward;

        // ---- 3. 特效朝向 ----
        Vector3 facingDirection;
        switch (orientationMode)
        {
            case OrientationMode.FaceWeaponVelocity:
                facingDirection = ComputeSlashTangent(hitNormal);
                break;
            case OrientationMode.FaceNormal:
            default:
                facingDirection = hitNormal;
                break;
        }

        // ---- 4. 生成 ----
        Vector3 spawnPos = contactPoint + hitNormal * surfaceOffset;
        Quaternion rot = Quaternion.LookRotation(facingDirection);
        rot *= Quaternion.Euler(
            Random.Range(-randomAngleRange, randomAngleRange),
            Random.Range(-randomAngleRange, randomAngleRange),
            Random.Range(-randomAngleRange, randomAngleRange)
        );

        float scl = Random.Range(randomScaleRange.x, randomScaleRange.y);
        GameObject go = Instantiate(hitEffectPrefab, spawnPos, rot);
        go.transform.localScale *= scl;

        if (lifetime > 0f) Destroy(go, lifetime);

#if UNITY_EDITOR
        if (_showDebugRay)
        {
            Debug.DrawRay(contactPoint, hitNormal * 0.3f, Color.red, 2f);
            Debug.DrawRay(contactPoint, facingDirection * 0.5f, Color.yellow, 2f);
        }
#endif

        return go;
    }

    /// <summary>
    /// 将武器瞬时速度投影到目标表面切平面，得到挥砍切线方向。
    /// 刺击时切线分量 ≈ 0，回退用武器本地前方向投影。
    /// </summary>
    private Vector3 ComputeSlashTangent(Vector3 hitNormal)
    {
        Vector3 velocity = _weaponVelocity;

        float normalComponent = Vector3.Dot(velocity, hitNormal);
        Vector3 tangent = velocity - normalComponent * hitNormal;

        if (tangent.magnitude < 0.1f)
        {
            Vector3 weaponForward = transform.root.forward;
            float fwdNormal = Vector3.Dot(weaponForward, hitNormal);
            tangent = weaponForward - fwdNormal * hitNormal;

            if (tangent.magnitude < 0.01f)
                tangent = Vector3.Cross(hitNormal, Vector3.up).normalized;
        }

        return tangent.normalized;
    }
}