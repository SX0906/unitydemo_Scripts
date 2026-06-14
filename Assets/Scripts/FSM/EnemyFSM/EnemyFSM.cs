using UnityEngine;
using System.Collections;

public class EnemyFSM : MonoBehaviour
{
    private EnemyFSMControl fsm;
    private CharacterController controller;

    private BehaviorTree behaviorTree;
    private EnemyFSMBT btBuilder;

    private EnemySkillManager skillManager;

    [Header("测试模式")]
    public bool testMode = false;

    [Header("视野参数")]
    public float visionRange = 8f;
    public float visionAngle = 120f;
    public float loseRange = 10f;

    [Header("玩家检测")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float visionHeightOffset = 1f;
    [SerializeField] private int visionRayCount = 10;

    [Header("地面检测")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundCheckDistance = 0.15f;

    [HideInInspector] public bool hasTarget;
    [HideInInspector] public Transform targetPlayer;
    [HideInInspector] public Vector3 lastKnownPlayerPos;

    [Header("顿帧设置")]
    public float hitStopTimeScale = 0.1f;
    public float hitStopDuration = 0.2f;
    public Coroutine hitStopCoroutine;
    private float memoryDuration = 2f;
    private float memoryTimer;

    public bool IsGrounded => CheckGrounded();

    private bool CheckGrounded()
    {
        if (controller == null) return false;
        Vector3 origin = transform.position + controller.center;
        float radius = controller.radius * 0.8f;
        float checkDist = controller.height / 2f - controller.radius + groundCheckDistance;
        return Physics.SphereCast(origin, radius, Vector3.down, out _, checkDist, groundLayer);
    }

    public bool IsInHitState =>
        fsm != null && (
            fsm.stateType == EnemyStateType.HIT ||
            fsm.stateType == EnemyStateType.AIR_HIT ||
            fsm.stateType == EnemyStateType.BLOCK ||
            fsm.stateType == EnemyStateType.DODGE ||
            fsm.stateType == EnemyStateType.BLOCKBREAK ||
            fsm.stateType == EnemyStateType.KNOCKDOWN ||
            fsm.stateType == EnemyStateType.FALLTOFLOOR ||
            fsm.stateType == EnemyStateType.GETUP
        );

    private void Awake()
    {
        Animator animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        CombatAudioPlayer audioPlayer = GetComponent<CombatAudioPlayer>();
        skillManager = GetComponent<EnemySkillManager>();
        fsm = new EnemyFSMControl();

        fsm.AddState(EnemyStateType.IDLE, new EnemyIdleState(animator, fsm, this, controller));
        fsm.AddState(EnemyStateType.MOVE, new EnemyMoveState(animator, fsm, transform, controller, this));
        fsm.AddState(EnemyStateType.HIT, new EnemyHitState(animator, fsm, transform, audioPlayer));
        fsm.AddState(EnemyStateType.AIR_HIT, new EnemyAirHitState(animator, fsm, transform, controller, this, audioPlayer));
        fsm.AddState(EnemyStateType.BLOCK, new EnemyBlockState(animator, fsm, this, transform, audioPlayer));
        fsm.AddState(EnemyStateType.DODGE, new EnemyDodgeState(animator, fsm, this, transform, controller));
        fsm.AddState(EnemyStateType.ATTACK, new EnemyAttackState(animator, fsm, this, transform));
        fsm.AddState(EnemyStateType.BLOCKBREAK, new EnemyBlockBreakState(animator, fsm, this, transform, audioPlayer));
        fsm.AddState(EnemyStateType.KNOCKDOWN, new EnemyKnockDownState(animator, fsm, this));
        fsm.AddState(EnemyStateType.FALLTOFLOOR, new EnemyFallToFloorState(animator, fsm, this, controller));
        fsm.AddState(EnemyStateType.GETUP, new EnemyGetUpState(animator, fsm, this));
        fsm.AddState(EnemyStateType.LOCK_MOVE, new EnemyLockMoveState(animator, fsm, transform, controller, this));

        fsm.SetState(EnemyStateType.IDLE);

        btBuilder = new EnemyFSMBT();
        behaviorTree = btBuilder.BuildTree(this);

        // 架势系统：攒满 → 爆架势
        var vitals = GetComponent<EnemyVitals>();
        if (vitals != null)
            vitals.OnPostureFull += () => fsm.SetState(EnemyStateType.BLOCKBREAK);
    }

    public void DirectSetState(EnemyStateType state)
    {
        fsm?.SetState(state);
    }

    public EnemyAttackState GetAttackState()
    {
        return fsm?.GetState<EnemyAttackState>(EnemyStateType.ATTACK);
    }

    public void BT_SetDesiredState(EnemyStateType state)
    {
        if (behaviorTree != null)
            behaviorTree.blackboard.desiredState = state;
    }

    private void Update()
    {
        if (testMode)
        {
            if (!IsInHitState && fsm.stateType != EnemyStateType.IDLE)
                fsm.SetState(EnemyStateType.IDLE);
            hasTarget = false;
            targetPlayer = null;
            memoryTimer = 0;
            fsm.OnTick();
            return;
        }

        UpdateVision();

        if (!IsInHitState)
        {
            if (fsm.stateType == EnemyStateType.ATTACK)
            {
                fsm.OnTick();
                return;
            }

            behaviorTree.Tick();

            if (fsm.stateType != EnemyStateType.ATTACK)
            {
                switch (behaviorTree.blackboard.desiredState)
                {
                    case EnemyStateType.MOVE:
                        if (fsm.stateType != EnemyStateType.MOVE)
                            fsm.SetState(EnemyStateType.MOVE);
                        break;
                    case EnemyStateType.LOCK_MOVE:
                        if (fsm.stateType != EnemyStateType.LOCK_MOVE)
                            fsm.SetState(EnemyStateType.LOCK_MOVE);
                        break;
                    case EnemyStateType.IDLE:
                    default:
                        if (fsm.stateType != EnemyStateType.IDLE)
                            fsm.SetState(EnemyStateType.IDLE);
                        break;
                }
            }
        }
        else
        {
            behaviorTree.blackboard.btControlEnabled = false;
        }

        fsm.OnTick();
    }

    private void UpdateVision()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * visionHeightOffset;
        float halfAngle = visionAngle * 0.5f;
        bool detectedThisFrame = false;
        RaycastHit detectedHit = default;

        for (int i = 0; i < visionRayCount; i++)
        {
            float currentAngle = -halfAngle + (visionAngle / (visionRayCount - 1)) * i;
            Vector3 rayDir = Quaternion.Euler(0f, currentAngle, 0f) * transform.forward;
            rayDir.y = 0f;

            if (Physics.Raycast(rayOrigin, rayDir.normalized, out RaycastHit hit, visionRange))
            {
                if ((playerLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
                {
                    detectedThisFrame = true;
                    detectedHit = hit;
                    break;
                }
            }
        }

        if (detectedThisFrame)
        {
            targetPlayer = detectedHit.transform;
            lastKnownPlayerPos = detectedHit.transform.position;
            memoryTimer = memoryDuration;
            hasTarget = true;
        }
        else if (hasTarget && targetPlayer != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, targetPlayer.position);
            if (distanceToTarget <= loseRange && memoryTimer > 0)
            {
                memoryTimer -= Time.deltaTime;
                lastKnownPlayerPos = targetPlayer.position;
            }
            else
            {
                hasTarget = false;
                targetPlayer = null;
                memoryTimer = 0;
            }
        }
    }

    public void OnAttackComboCheck()
    {
        fsm.GetState<EnemyAttackState>(EnemyStateType.ATTACK)?.OnAttackComboCheck();
    }

    private IEnumerator HitStopCoroutine()
    {
        Time.timeScale = hitStopTimeScale;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = 1f;
        hitStopCoroutine = null;
    }

    private void PlayHitStop()
    {
        if (hitStopCoroutine != null)
            StopCoroutine(hitStopCoroutine);
        hitStopCoroutine = StartCoroutine(HitStopCoroutine());
    }

    private const float BlockAngle = 45f;

    public void TakeDamage(string hitDirTag, Vector3 hitDirection, bool isLauncher, Transform attacker, float damage)
    {
        var vitals = GetComponent<EnemyVitals>();
        Debug.Log($"[EnemyFSM.TakeDamage] 调用! damage={damage}, vitals={vitals != null}, state={fsm.stateType}, inBlock={attacker != null && Vector3.Angle(transform.forward, (attacker.position - transform.position).normalized) <= BlockAngle}");

        if (fsm.stateType == EnemyStateType.BLOCKBREAK)
        {
            vitals?.TakeDamage(damage);
            fsm.SetState(EnemyStateType.KNOCKDOWN);
            return;
        }

        // KNOCKDOWN：只扣血 + 攒怒气，不播受击动画
        if (fsm.stateType == EnemyStateType.KNOCKDOWN)
        {
            vitals?.TakeDamage(damage);
            vitals?.OnHitReceived();
            return;
        }

        if (fsm.stateType == EnemyStateType.FALLTOFLOOR ||
            fsm.stateType == EnemyStateType.GETUP)
        {
            return;
        }

        if (isLauncher || !IsGrounded)
        {
            vitals?.TakeDamage(damage);
            vitals?.OnHitReceived();
            fsm.GetState<EnemyAirHitState>(EnemyStateType.AIR_HIT).SetHitDirection(hitDirection);
            if (isLauncher)
                fsm.GetState<EnemyAirHitState>(EnemyStateType.AIR_HIT).StartFollowPlayerRootMotion(attacker);
            if (fsm.stateType == EnemyStateType.AIR_HIT)
            {
                fsm.GetState<EnemyAirHitState>(EnemyStateType.AIR_HIT).RefreshAirTime();
                fsm.GetState<EnemyAirHitState>(EnemyStateType.AIR_HIT).Rehit();
            }
            else
            {
                fsm.SetState(EnemyStateType.AIR_HIT);
            }
            return;
        }

        bool inBlockRange = false;
        if (attacker != null)
        {
            Vector3 toAttacker = attacker.position - transform.position;
            toAttacker.y = 0;
            if (toAttacker.magnitude > 0.01f)
            {
                float angle = Vector3.Angle(transform.forward, toAttacker);
                inBlockRange = angle <= BlockAngle;
            }
        }

        if (inBlockRange)
        {
            float roll = Random.value;
            if (roll < 0.6f)
            {
                // 格挡：不减血
                if (fsm.stateType == EnemyStateType.BLOCK)
                {
                    fsm.GetState<EnemyBlockState>(EnemyStateType.BLOCK).SetAttacker(attacker);
                    fsm.GetState<EnemyBlockState>(EnemyStateType.BLOCK).Rehit();
                    return;
                }
                if (fsm.stateType == EnemyStateType.HIT || fsm.stateType == EnemyStateType.DODGE)
                {
                    fsm.GetState<EnemyBlockState>(EnemyStateType.BLOCK).SetAttacker(attacker);
                    fsm.SetState(EnemyStateType.BLOCK);
                    return;
                }
                fsm.GetState<EnemyBlockState>(EnemyStateType.BLOCK).SetAttacker(attacker);
                fsm.SetState(EnemyStateType.BLOCK);
                return;
            }
            else if (roll < 0.7f)
            {
                // DODGE：不减血
                if (fsm.stateType == EnemyStateType.DODGE)
                {
                    fsm.GetState<EnemyDodgeState>(EnemyStateType.DODGE).SetAttacker(attacker);
                    fsm.GetState<EnemyDodgeState>(EnemyStateType.DODGE).Rehit();
                    return;
                }
                if (fsm.stateType == EnemyStateType.HIT || fsm.stateType == EnemyStateType.BLOCK)
                {
                    fsm.GetState<EnemyDodgeState>(EnemyStateType.DODGE).SetAttacker(attacker);
                    fsm.SetState(EnemyStateType.DODGE);
                    return;
                }
                fsm.GetState<EnemyDodgeState>(EnemyStateType.DODGE).SetAttacker(attacker);
                fsm.SetState(EnemyStateType.DODGE);
                return;
            }
        }

        // 普通受击：扣血 + 攒怒气
        vitals?.TakeDamage(damage);
        vitals?.OnHitReceived();
        Debug.Log($"[EnemyFSM.TakeDamage] 进入普通受击, 扣血={damage}, 剩余血量={vitals?.currentHealth}");

        if (fsm.stateType == EnemyStateType.HIT)
        {
            fsm.GetState<EnemyHitState>(EnemyStateType.HIT).SetHitDirectionTag(hitDirTag);
            fsm.GetState<EnemyHitState>(EnemyStateType.HIT).SetAttacker(attacker);
            fsm.GetState<EnemyHitState>(EnemyStateType.HIT).Rehit();
            PlayHitStop();
            return;
        }
        fsm.GetState<EnemyHitState>(EnemyStateType.HIT).SetHitDirectionTag(hitDirTag);
        fsm.GetState<EnemyHitState>(EnemyStateType.HIT).SetAttacker(attacker);
        fsm.SetState(EnemyStateType.HIT);
        PlayHitStop();
    }
}
