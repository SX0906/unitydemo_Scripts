using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class EnemyChaseConfig
{
    [Header("追击走路速度")] public float chaseWalkSpeed = 3.5f;
    [Header("追击奔跑速度")] public float chaseRunSpeed = 5.5f;
    [Header("奔跑触发距离")] public float runDistance = 6f;
}

[System.Serializable]
public class EnemyAIConfig
{
    [Header("测试关闭AI")] public bool disableAIForTest = false;
    [Header("寻路更新间隔")] public float repathInterval = 0.15f;

    [Header("进入绕圈距离容差")] public float orbitEnterBuffer = 0.25f;
    [Header("退出绕圈距离容差")] public float orbitExitBuffer = 0.8f;
    [Header("NavAgent停止距离比绕圈距离少多少")] public float navStopDistanceOffset = 0.35f;

    [Header("技能接近停止距离修正")]
    public float skillApproachStopOffset = 0.25f;
}

[System.Serializable]
public class EnemyAnimatorParamConfig
{
    [Header("移动参数")] public string movementParam = "Movement";
    [Header("锁定模式")] public string lockOnParam = "LockOn";
    public string horizontalParam = "Horizontal";
    public string verticalParam = "Vertical";
    [Header("Idle值")] public float idleValue = 0;
    [Header("行走值")] public float walkValue = 1f;
    [Header("奔跑值")] public float runValue = 1.5f;
}

[System.Serializable]
public class EnemyOrbitConfig
{
    [Header("绕圈速度")] public float orbitSpeed = 1.2f;
    [Header("顺时针绕圈")] public bool clockwise = true;
    [Header("转向速度")] public float orbitFaceSpeed = 10f;
    [Header("后退触发距离")] public float backMinDistance = 2.2f;
    [Header("后退速度")] public float backSpeed = 2.2f;
}

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(EnemyCombatController))]
public class EnemyMoveControllerBT : ActorBase
{
    [Header("追击配置")] public EnemyChaseConfig chaseConfig = new();
    [Header("AI配置")] public EnemyAIConfig aiConfig = new();
    [Header("动画参数")] public EnemyAnimatorParamConfig animatorParams = new();
    [Header("绕圈配置")] public EnemyOrbitConfig orbitConfig = new();

    private EnemyCombatController combat;
    private EnemyHealthController healthController;
    private EnemyAttackSkillPlayer skillPlayer;
    private EnemyBTNode rootNode;

    private float nextRepathTime;
    private bool isInOrbitAction;
    private bool isNormalMove;
    private bool isRunState;
    private bool orbitModeLocked;
    private Vector2 lockOnCacheInput;

    protected override void Awake()
    {
        base.Awake();

        combat = GetComponent<EnemyCombatController>();
        healthController = GetComponent<EnemyHealthController>();
        skillPlayer = GetComponent<EnemyAttackSkillPlayer>();

        if (NavAgent != null)
        {
            NavAgent.updatePosition = false;
            NavAgent.updateRotation = false;
            NavAgent.speed = chaseConfig.chaseWalkSpeed;
            NavAgent.stoppingDistance = GetOrbitStopDistance();
        }

        BuildBehaviorTree();
    }

    private void Start()
    {
        // 延迟设置isStopped，确保NavAgent已在NavMesh上
        if (NavAgent != null && CanUseNavAgent())
        {
            NavAgent.isStopped = false;
        }
    }

    private void Update()
    {
        // 检查当前目标是否已死亡
        if (combat != null && combat.Target != null)
        {
            HealthSystem targetHealth = combat.Target.GetComponent<HealthSystem>();
            if (targetHealth != null && targetHealth.IsDead)
            {
                combat.LoseTarget();
            }
        }

        if (combat != null && combat.Target == null)
            combat.FindTargetByTag();

        if (rootNode != null)
            rootNode.Tick();

        ApplyGravityForEnemy();
        UpdateAnimatorParams();
    }

    private void BuildBehaviorTree()
    {
        rootNode = new EnemyBTSelectorNode(
            new EnemyBTSequenceNode(
                new EnemyBTConditionNode(() => aiConfig.disableAIForTest),
                new EnemyBTActionNode(StopAction)
            ),

            new EnemyBTSequenceNode(
                new EnemyBTConditionNode(() => actorState.isHit),
                new EnemyBTActionNode(StopAction)
            ),

            // 玩家血量低于阈值且在2米范围内，敌人发起处决
            new EnemyBTSequenceNode(
                new EnemyBTConditionNode(CanExecutePlayer),
                new EnemyBTActionNode(ExecutePlayerAction)
            ),

            new EnemyBTSequenceNode(
                new EnemyBTConditionNode(() => combat != null && combat.CanEnterCombat()),
                new EnemyBTConditionNode(IsPlayerAttacking),
                new EnemyBTActionNode(() =>
                    healthController != null && healthController.TryStartAutoBlock()
                        ? EnemyBTState.Success
                        : EnemyBTState.Failure)
            ),

            // 当前已有可释放技能，直接释放
            new EnemyBTSequenceNode(
                new EnemyBTConditionNode(() => combat != null && combat.CanEnterCombat()),
                new EnemyBTConditionNode(() => skillPlayer != null && skillPlayer.HasAvailableSkillNow()),
                new EnemyBTActionNode(combat.SkillOnlyAttackAction)
            ),

            // 有技能准备好了，但距离不够，主动靠近到释放距离内
            new EnemyBTSequenceNode(
                new EnemyBTConditionNode(() => combat != null && combat.CanEnterCombat()),
                new EnemyBTConditionNode(ShouldApproachSkillRange),
                new EnemyBTActionNode(ApproachSkillRangeAction)
            ),

            new EnemyBTSequenceNode(
                new EnemyBTConditionNode(() => combat != null && combat.CanEnterCombat()),
                new EnemyBTConditionNode(ShouldStayOrEnterOrbit),
                new EnemyBTActionNode(OrbitAction)
            ),

            new EnemyBTSequenceNode(
                new EnemyBTConditionNode(() => combat != null && combat.CanEnterCombat()),
                new EnemyBTActionNode(ChaseAction)
            ),

            new EnemyBTActionNode(StopAction)
        );
    }

    private bool CanExecutePlayer()
    {
        if (ExecutionManager.Instance == null)
            return false;

        if (combat == null || combat.Target == null)
            return false;

        // 检查玩家是否已死亡
        HealthSystem playerHealth = combat.Target.GetComponent<HealthSystem>();
        if (playerHealth == null || playerHealth.IsDead)
            return false;

        // 检查玩家血量是否低于阈值
        if (!ExecutionManager.CanExecute(combat.Target.gameObject))
            return false;

        // 检查敌人是否在2米范围内
        float distance = Vector3.Distance(transform.position, combat.Target.position);
        float executionDistance = ExecutionManager.Instance.config.enemyExecutionDistance;
        return distance <= executionDistance;
    }

    private EnemyBTState ExecutePlayerAction()
    {
        if (combat == null || combat.Target == null)
            return EnemyBTState.Failure;

        // 发起处决
        if (ExecutionManager.TryStartExecution(gameObject, combat.Target.gameObject))
        {
            return EnemyBTState.Success;
        }

        return EnemyBTState.Failure;
    }

    private bool IsPlayerAttacking()
    {
        if (combat == null || combat.Target == null)
            return false;

        PlayerCombatController playerCombat = combat.Target.GetComponent<PlayerCombatController>();
        return playerCombat != null && playerCombat.IsCurrentlyAttacking();
    }

    private bool ShouldApproachSkillRange()
    {
        if (skillPlayer == null || combat == null || combat.Target == null)
            return false;

        if (!CanMove())
            return false;

        return skillPlayer.TryGetApproachSkill(out _);
    }

    private bool ShouldStayOrEnterOrbit()
    {
        if (combat == null || combat.Target == null)
        {
            orbitModeLocked = false;
            return false;
        }

        float distance = Vector3.Distance(transform.position, combat.Target.position);
        float enterDistance = combat.OrbitRange + aiConfig.orbitEnterBuffer;
        float exitDistance = combat.OrbitRange + aiConfig.orbitExitBuffer;

        if (exitDistance <= enterDistance)
            exitDistance = enterDistance + 0.3f;

        if (orbitModeLocked)
        {
            if (distance > exitDistance)
                orbitModeLocked = false;
        }
        else
        {
            if (distance <= enterDistance)
                orbitModeLocked = true;
        }

        return orbitModeLocked;
    }

    private EnemyBTState ApproachSkillRangeAction()
    {
        if (combat == null || combat.Target == null || skillPlayer == null || !CanUseNavAgent() || !CanMove())
            return StopAction();

        if (!skillPlayer.TryGetApproachSkill(out EnemyAttackSkill approachSkill))
            return EnemyBTState.Failure;

        Transform target = combat.Target;
        float skillUseDistance = Mathf.Max(0.05f, approachSkill.atkskillUseDistance);
        float desiredDistance = Mathf.Max(0.1f, skillUseDistance - aiConfig.skillApproachStopOffset);
        float distance = Vector3.Distance(transform.position, target.position);

        if (distance <= desiredDistance)
        {
            StopNavAgentOnly();
            ResetMoveStates();
            FaceTarget(target);
            return EnemyBTState.Success;
        }

        orbitModeLocked = false;
        ResumeNavAgentOnly();

        isInOrbitAction = false;
        isRunState = distance >= chaseConfig.runDistance;

        float speed = isRunState ? chaseConfig.chaseRunSpeed : chaseConfig.chaseWalkSpeed;
        NavAgent.speed = speed;
        NavAgent.stoppingDistance = desiredDistance;

        UpdateNavPath(target.position);
        isNormalMove = MoveByNavAgentSteeringToDistance(speed, desiredDistance, target);
        NavAgent.nextPosition = transform.position;
        FaceTarget(target);

        return EnemyBTState.Running;
    }

    private EnemyBTState OrbitAction()
    {
        if (combat == null || combat.Target == null || !CanMove())
            return StopAction();

        StopNavAgentOnly();

        isInOrbitAction = true;
        isNormalMove = false;
        isRunState = false;

        Transform target = combat.Target;
        FaceTarget(target);

        float distance = Vector3.Distance(transform.position, target.position);
        Vector3 moveDir;

        if (distance < orbitConfig.backMinDistance)
        {
            moveDir = -transform.forward * orbitConfig.backSpeed;
            lockOnCacheInput = new Vector2(0, -1);
        }
        else
        {
            Vector3 rightDir = orbitConfig.clockwise ? transform.right : -transform.right;
            moveDir = rightDir * orbitConfig.orbitSpeed;
            lockOnCacheInput = new Vector2(orbitConfig.clockwise ? 1 : -1, 0);
        }

        moveDir.y = 0f;
        CharacterController.Move(moveDir * Time.deltaTime);

        if (CanUseNavAgent())
            NavAgent.nextPosition = transform.position;

        return EnemyBTState.Running;
    }

    private EnemyBTState ChaseAction()
    {
        if (combat == null || combat.Target == null || !CanUseNavAgent() || !CanMove())
            return StopAction();

        ResumeNavAgentOnly();
        RestoreOrbitStopDistance();

        isInOrbitAction = false;
        float distance = Vector3.Distance(transform.position, combat.Target.position);
        isRunState = distance >= chaseConfig.runDistance;

        float speed = isRunState ? chaseConfig.chaseRunSpeed : chaseConfig.chaseWalkSpeed;
        NavAgent.speed = speed;

        UpdateNavPath(combat.GetChaseDestination());
        isNormalMove = MoveByNavAgentSteering(speed);
        NavAgent.nextPosition = transform.position;
        FaceTarget(combat.Target);

        return EnemyBTState.Running;
    }

    private bool MoveByNavAgentSteering(float speed)
    {
        if (NavAgent.pathPending || !NavAgent.hasPath || NavAgent.remainingDistance <= NavAgent.stoppingDistance)
            return false;

        Vector3 dir = NavAgent.steeringTarget - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            return false;

        CharacterController.Move(dir.normalized * speed * Time.deltaTime);
        return true;
    }

    private bool MoveByNavAgentSteeringToDistance(float speed, float desiredDistance, Transform target)
    {
        if (target == null)
            return false;

        float currentDistance = Vector3.Distance(transform.position, target.position);
        if (currentDistance <= desiredDistance || NavAgent.pathPending || !NavAgent.hasPath)
            return false;

        Vector3 dir = NavAgent.steeringTarget - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            return false;

        CharacterController.Move(dir.normalized * speed * Time.deltaTime);
        return true;
    }

    private EnemyBTState StopAction()
    {
        StopNavAgentOnly();
        ResetMoveStates();
        return EnemyBTState.Running;
    }

    private bool CanMove()
    {
        if (skillPlayer != null && skillPlayer.IsUsingSkill)
            return false;

        return !actorState.isHit && !actorState.isAttacking && !actorState.isGuarding;
    }

    public override bool CanUseNavAgent()
    {
        return NavAgent != null && NavAgent.enabled && NavAgent.isActiveAndEnabled && NavAgent.isOnNavMesh;
    }

    private float GetOrbitStopDistance()
    {
        return combat != null
            ? Mathf.Max(0.1f, combat.OrbitRange - aiConfig.navStopDistanceOffset)
            : 0.5f;
    }

    private void RestoreOrbitStopDistance()
    {
        if (CanUseNavAgent())
            NavAgent.stoppingDistance = GetOrbitStopDistance();
    }

    private void FaceTarget(Transform target)
    {
        if (target == null)
            return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            return;

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            Quaternion.LookRotation(dir.normalized),
            orbitConfig.orbitFaceSpeed * Time.deltaTime
        );
    }

    private void StopNavAgentOnly()
    {
        if (!CanUseNavAgent())
            return;

        NavAgent.isStopped = true;
        NavAgent.ResetPath();
        NavAgent.nextPosition = transform.position;
    }

    private void ResumeNavAgentOnly()
    {
        if (!CanUseNavAgent())
            return;

        NavAgent.nextPosition = transform.position;
        NavAgent.isStopped = false;
    }

    private void ApplyGravityForEnemy()
    {
        if (CharacterController == null)
            return;

        if (skillPlayer != null && skillPlayer.IsUsingSkill)
            return;

        if (CharacterController.isGrounded && verticalVelocity.y < 0)
            verticalVelocity.y = moveConfig.groundedForce;

        verticalVelocity.y += moveConfig.gravity * Time.deltaTime;
        CharacterController.Move(verticalVelocity * Time.deltaTime);
    }

    private void UpdateAnimatorParams()
    {
        if (Animator == null)
            return;

        if (actorState.isHit || actorState.isAttacking || actorState.isGuarding)
        {
            SetAnimatorIdle();
            return;
        }

        if (isInOrbitAction)
        {
            Animator.SetFloat(animatorParams.lockOnParam, 1f, 0.1f, Time.deltaTime);
            Animator.SetFloat(animatorParams.horizontalParam, lockOnCacheInput.x, 0.1f, Time.deltaTime);
            Animator.SetFloat(animatorParams.verticalParam, lockOnCacheInput.y, 0.1f, Time.deltaTime);
            Animator.SetFloat(animatorParams.movementParam, animatorParams.idleValue);
        }
        else
        {
            float moveValue = isNormalMove
                ? isRunState ? animatorParams.runValue : animatorParams.walkValue
                : animatorParams.idleValue;

            Animator.SetFloat(animatorParams.lockOnParam, 0, 0.1f, Time.deltaTime);
            Animator.SetFloat(animatorParams.movementParam, moveValue, 0.1f, Time.deltaTime);
            Animator.SetFloat(animatorParams.horizontalParam, 0, 0.1f, Time.deltaTime);
            Animator.SetFloat(animatorParams.verticalParam, 0, 0.1f, Time.deltaTime);
        }
    }

    // 仅提取重复代码为辅助方法，不改变任何原有逻辑
    private void ResetMoveStates()
    {
        isInOrbitAction = false;
        isNormalMove = false;
        isRunState = false;
        lockOnCacheInput = Vector2.zero;
    }

    private void UpdateNavPath(Vector3 destination)
    {
        if (Time.time >= nextRepathTime && NavAgent.isOnNavMesh)
        {
            nextRepathTime = Time.time + aiConfig.repathInterval;
            NavAgent.SetDestination(destination);
        }
    }

    private void SetAnimatorIdle()
    {
        Animator.SetFloat(animatorParams.movementParam, animatorParams.idleValue);
        Animator.SetFloat(animatorParams.lockOnParam, 0);
        Animator.SetFloat(animatorParams.horizontalParam, 0);
        Animator.SetFloat(animatorParams.verticalParam, 0);
    }
}