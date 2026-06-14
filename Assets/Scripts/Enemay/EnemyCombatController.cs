using UnityEngine;

[System.Serializable]
public class EnemyTargetConfig
{
    public Transform target;
    public string playerTag = "Player";
}

[System.Serializable]
public class EnemySenseConfig
{
    [Header("探测范围")] public float detectRange = 12f;
    [Header("丢失目标范围")] public float loseRange = 18f;
    [Header("绕圈触发范围(固定5米)")] public float orbitRange = 5f;
    [Header("是否需要视野遮挡检测")] public bool requireLineOfSight = true;
    public LayerMask obstacleMask;

    [Range(0f, 180f)] public float viewAngle = 120f;
    [Header("目标丢失记忆时间")] public float memoryTime = 3f;
    [Header("到达记忆点判定距离")] public float lastKnownReachDistance = 1.2f;
    [Header("到达记忆点后忘记目标")] public bool forgetWhenReachLastKnownPosition = true;
}

public class EnemyCombatController : MonoBehaviour
{
    [Header("目标配置")]
    public EnemyTargetConfig targetConfig = new();

    [Header("感知配置")]
    public EnemySenseConfig senseConfig = new();

    private ActorBase actor;
    private EnemyAttackSkillPlayer skillPlayer;

    private bool detected;
    private bool canSee;
    private bool hasLastKnown;
    private Vector3 lastKnownPos;
    private float memoryEndTime;
    private float forceKeepTargetEndTime;

    public Transform Target => targetConfig.target;
    public bool CanSeeTarget => canSee;
    public bool HasTarget => targetConfig.target != null;
    public float OrbitRange => senseConfig.orbitRange;

    private void Awake()
    {
        actor = GetComponent<ActorBase>();
        skillPlayer = GetComponent<EnemyAttackSkillPlayer>();
    }

    public void FindTargetByTag()
    {
        if (HasTarget || string.IsNullOrEmpty(targetConfig.playerTag))
            return;

        GameObject player = GameObject.FindGameObjectWithTag(targetConfig.playerTag);

        if (player != null && CanDetect(player.transform) && IsTargetAlive(player))
            SetTarget(player.transform);
    }

    private bool IsTargetAlive(GameObject target)
    {
        if (target == null) return false;
        HealthSystem health = target.GetComponent<HealthSystem>();
        if (health == null) return true; // 如果没有血量系统，默认认为活着
        return !health.IsDead; // 只有IsDead为false时，说明还活着
    }

    private void SetTarget(Transform t)
    {
        targetConfig.target = t;
        detected = true;
        canSee = true;
        SaveLastKnownPosition(t.position);
    }

    public void ForceKeepTarget(float duration)
    {
        forceKeepTargetEndTime = Time.time + duration;
    }

    public bool CanEnterCombat()
    {
        if (!HasTarget || actor == null || actor.actorState.isHit)
            return false;

        // 检查目标是否已死亡
        if (!IsTargetAlive(Target.gameObject))
        {
            LoseTarget();
            return false;
        }

        if (GetDistanceSqr(transform.position, Target.position) > senseConfig.loseRange * senseConfig.loseRange)
        {
            LoseTarget();
            return false;
        }

        if (Time.time < forceKeepTargetEndTime)
            return true;

        canSee = CanDetect(Target);

        if (canSee)
        {
            SaveLastKnownPosition(Target.position);
            return true;
        }

        if (!HasValidMemory())
        {
            LoseTarget();
            return false;
        }

        if (senseConfig.forgetWhenReachLastKnownPosition && ReachedLastKnownPosition())
        {
            LoseTarget();
            return false;
        }

        return true;
    }

    public bool IsInSkillActiveRange()
    {
        if (!HasTarget || skillPlayer == null)
            return false;

        return skillPlayer.HasAvailableSkillNow();
    }

    public bool IsInOrbitRange()
    {
        if (!HasTarget)
            return false;

        return GetDistanceSqr(transform.position, Target.position)
               <= senseConfig.orbitRange * senseConfig.orbitRange;
    }

    public EnemyBTState SkillOnlyAttackAction()
    {
        if (!HasTarget || actor == null || actor.actorState.isHit)
            return EnemyBTState.Failure;

        if (skillPlayer == null)
            return EnemyBTState.Failure;

        if (skillPlayer.IsUsingSkill)
            return EnemyBTState.Running;

        if (!skillPlayer.HasAvailableSkillNow())
            return EnemyBTState.Failure;

        // 技能释放前，立即转向玩家
        FaceTargetImmediately();

        bool started = skillPlayer.TryUseRandomAvailableSkill();

        return started ? EnemyBTState.Running : EnemyBTState.Failure;
    }

    private void FaceTargetImmediately()
    {
        if (!HasTarget)
            return;

        Vector3 dir = Target.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    public Vector3 GetChaseDestination()
    {
        if (HasTarget && canSee)
            return Target.position;

        if (hasLastKnown)
            return lastKnownPos;

        return transform.position;
    }

    public void LoseTarget()
    {
        string oldTag = targetConfig.playerTag;

        targetConfig = new EnemyTargetConfig
        {
            playerTag = oldTag
        };

        detected = false;
        canSee = false;
        hasLastKnown = false;
    }

    private bool CanDetect(Transform t)
    {
        return t != null &&
               InDetectRange(t) &&
               InViewAngle(t) &&
               HasLineOfSight(t);
    }

    private bool InDetectRange(Transform t)
    {
        return GetDistanceSqr(transform.position, t.position)
               <= senseConfig.detectRange * senseConfig.detectRange;
    }

    private bool InViewAngle(Transform t)
    {
        Vector3 forward = transform.forward;
        Vector3 dir = t.position - transform.position;

        forward.y = 0;
        dir.y = 0;

        if (forward.sqrMagnitude < 0.001f || dir.sqrMagnitude < 0.001f)
            return true;

        return Vector3.Angle(forward.normalized, dir.normalized)
               <= senseConfig.viewAngle * 0.5f;
    }

    private bool HasLineOfSight(Transform t)
    {
        if (!senseConfig.requireLineOfSight)
            return true;

        Vector3 dir = t.position - transform.position;

        return dir.magnitude <= 0.01f ||
               !Physics.Raycast(
                   transform.position,
                   dir.normalized,
                   dir.magnitude,
                   senseConfig.obstacleMask,
                   QueryTriggerInteraction.Ignore
               );
    }

    private void SaveLastKnownPosition(Vector3 pos)
    {
        lastKnownPos = pos;
        hasLastKnown = true;
        memoryEndTime = Time.time + senseConfig.memoryTime;
    }

    private bool HasValidMemory()
    {
        return detected &&
               hasLastKnown &&
               Time.time <= memoryEndTime;
    }

    private bool ReachedLastKnownPosition()
    {
        return hasLastKnown &&
               GetDistanceSqr(transform.position, lastKnownPos)
               <= senseConfig.lastKnownReachDistance * senseConfig.lastKnownReachDistance;
    }

    private float GetDistanceSqr(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude;
    }
}
