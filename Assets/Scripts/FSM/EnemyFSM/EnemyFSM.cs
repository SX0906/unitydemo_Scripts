using UnityEngine;
using System.Collections;

public class EnemyFSM : MonoBehaviour
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private CharacterController controller;

    private BehaviorTree behaviorTree;
    private EnemyFSMBT btBuilder;

    private EnemySkillManager skillManager;
    private EnemyWeaponHitDetector enemyWeaponHitDetector;

    private int _blockCount = 0; // 格挡计数，每第3次触发弹刀攻击

    [Header("测试模式")]
    public bool testMode = false;
    public static bool TestModeEnabled;

    [Header("视野参数")]
    public float visionRange = 8f;
    public float visionAngle = 120f;
    [Range(0f, 90f)] public float visionVerticalHalfAngle = 45f;
    public float loseRange = 10f;

    [Header("玩家检测")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float visionHeightOffset = 1f;
    [SerializeField] private int visionRayCount = 10;
    [SerializeField] private int visionVerticalRayCount = 3;

    [Header("地面检测")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundCheckDistance = 0.15f;

    [HideInInspector] public bool hasTarget;
    [HideInInspector] public Transform targetPlayer;
    [HideInInspector] public Vector3 lastKnownPlayerPos;

    [Header("协战通信")]
    [Tooltip("发现玩家后，通知此半径内的其它敌人一起追击")]
    public float alertRadius = 16f;

    [Header("顿帧设置")]
    //public float hitStopTimeScale = 0.1f;
    //public float hitStopDuration = 0.2f;
    public int hitStopFrameCount = 8;
    private float memoryDuration = 3f;
    private float memoryTimer;
    private bool wasDetecting;
    private float attackDeclineUntil;

    public bool IsGrounded => CheckGrounded();
    public LayerMask PlayerLayer => playerLayer;   // 供EnemyAttackState范围检测用

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
            fsm.stateType == EnemyStateType.PARRYATTACK ||
            fsm.stateType == EnemyStateType.GETUP
        );

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        CombatAudioPlayer audioPlayer = GetComponent<CombatAudioPlayer>();
        skillManager = GetComponent<EnemySkillManager>();
        enemyWeaponHitDetector = GetComponentInChildren<EnemyWeaponHitDetector>();
        fsm = new EnemyFSMControl();
        testMode = PlayerPrefs.GetInt("TestMode", 0) == 1; TestModeEnabled = testMode;

        // 顿帧管理器宿主注册
        HitStopManager.EnsureHost(this);

        fsm.AddState(EnemyStateType.IDLE, new EnemyIdleState(animator, fsm, this, controller));
        fsm.AddState(EnemyStateType.MOVE, new EnemyMoveState(animator, fsm, transform, controller, this));
        fsm.AddState(EnemyStateType.HIT, new EnemyHitState(animator, fsm, transform, controller, audioPlayer));
        fsm.AddState(EnemyStateType.AIR_HIT, new EnemyAirHitState(animator, fsm, transform, controller, this, audioPlayer));
        fsm.AddState(EnemyStateType.BLOCK, new EnemyBlockState(animator, fsm, this, transform, audioPlayer));
        fsm.AddState(EnemyStateType.DODGE, new EnemyDodgeState(animator, fsm, this, transform, controller));
        fsm.AddState(EnemyStateType.ATTACK, new EnemyAttackState(animator, fsm, this, transform));
        fsm.AddState(EnemyStateType.BLOCKBREAK, new EnemyBlockBreakState(animator, fsm, this, transform, audioPlayer));
        fsm.AddState(EnemyStateType.KNOCKDOWN, new EnemyKnockDownState(animator, fsm, this));
        fsm.AddState(EnemyStateType.FALLTOFLOOR, new EnemyFallToFloorState(animator, fsm, this, controller));
        fsm.AddState(EnemyStateType.GETUP, new EnemyGetUpState(animator, fsm, this));
        fsm.AddState(EnemyStateType.LOCK_MOVE, new EnemyLockMoveState(animator, fsm, transform, controller, this));
        fsm.AddState(EnemyStateType.DEATH, new EnemyDeathState(animator,fsm,this,controller));
        fsm.AddState(EnemyStateType.PARRYATTACK, new EnemyParryAttackState(animator, fsm, this, transform, audioPlayer));
        fsm.SetState(EnemyStateType.IDLE);

        btBuilder = new EnemyFSMBT();
        behaviorTree = btBuilder.BuildTree(this);

        // 架势系统：攒满 → 爆架势
        var vitals = GetComponent<EnemyVitals>();
        if (vitals != null)
            vitals.OnPostureFull += () => fsm.SetState(EnemyStateType.BLOCKBREAK);
    }

    private void OnEnable()
    {
        EnemyCombatCoordinator.Register(this);
    }

    private void OnDisable()
    {
        EnemyCombatCoordinator.Unregister(this);
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

    public bool CanAttemptAttack => Time.time >= attackDeclineUntil;

    public void MarkAttackDeclined(float duration = 1f)
    {
        attackDeclineUntil = Time.time + duration;
    }

    private void Update()
    {
        ActorCollisionEscape.Tick(controller);

        // 全局兜底：任何状态下只要脚底有其它角色，忽略碰撞并斜向弹开下落
        if (ActorCollisionEscape.IsOverlappingActor(controller, out int otherLayer))
            ActorCollisionEscape.ResolveOverlap(controller, otherLayer);

        if (fsm.stateType == EnemyStateType.DEATH)
        {
            fsm.OnTick();
            return;
        }

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

        float sphereRadius = 0.3f;
        int verticalSegments = Mathf.Max(1, visionVerticalRayCount - 1);

        for (int i = 0; i < visionRayCount && !detectedThisFrame; i++)
        {
            float currentAngle = -halfAngle + (visionAngle / (visionRayCount - 1)) * i;
            Vector3 horizontalForward = transform.forward;
            horizontalForward.y = 0f;
            horizontalForward.Normalize();
            if (horizontalForward.sqrMagnitude < 0.001f)
                horizontalForward = Vector3.forward;

            for (int j = 0; j <= verticalSegments && !detectedThisFrame; j++)
            {
                float pitch = Mathf.Lerp(
                    -visionVerticalHalfAngle,
                    visionVerticalHalfAngle,
                    (float)j / verticalSegments);
                Vector3 rayDir = Quaternion.Euler(pitch, currentAngle, 0f) * horizontalForward;
                rayDir.Normalize();

                if (Physics.SphereCast(rayOrigin, sphereRadius, rayDir,
                    out RaycastHit hit, visionRange))
                {
                    if ((playerLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
                    {
                        detectedThisFrame = true;
                        detectedHit = hit;
                        break;
                    }
                }
            }
        }

        if (detectedThisFrame)
        {
            bool firstSight = !wasDetecting;
            targetPlayer = detectedHit.transform;
            lastKnownPlayerPos = detectedHit.transform.position;
            memoryTimer = memoryDuration;
            hasTarget = true;

            // 首次看到玩家：广播警报；后续帧只刷新共享位置
            if (firstSight)
                EnemyCombatCoordinator.ReportPlayerSpotted(this, targetPlayer, lastKnownPlayerPos, alertRadius);
            else
                EnemyCombatCoordinator.RefreshSharedTarget(targetPlayer, lastKnownPlayerPos);
        }
        else if (hasTarget && targetPlayer != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, targetPlayer.position);
            if (distanceToTarget <= loseRange && memoryTimer > 0)
            {
                memoryTimer -= Time.deltaTime;
                lastKnownPlayerPos = targetPlayer.position;
            }
            else if (EnemyCombatCoordinator.TryGetSharedAlert(out Transform sharedPlayer, out Vector3 sharedPos))
            {
                // 自己的记忆结束但队内还有人知道玩家位置时，继续共享追击
                targetPlayer = sharedPlayer;
                lastKnownPlayerPos = sharedPos;
                memoryTimer = memoryDuration;
                hasTarget = true;
            }
            else
            {
                hasTarget = false;
                targetPlayer = null;
                memoryTimer = 0;
            }
        }
        wasDetecting = detectedThisFrame;
    }

    /// <summary>
    /// 接收其它敌人广播的共享警报，获得玩家位置并开始追击。
    /// </summary>
    public void ReceiveSharedAlert(Transform player, Vector3 position)
    {
        if (player == null || hasTarget) return;

        targetPlayer = player;
        lastKnownPlayerPos = position;
        memoryTimer = Mathf.Max(memoryTimer, memoryDuration);
        hasTarget = true;
    }

    public void OnAttackComboCheck()
    {
        fsm.GetState<EnemyAttackState>(EnemyStateType.ATTACK)?.OnAttackComboCheck();
    }
    
    public void OnAreaAttack()
    {
        fsm.GetState<EnemyAttackState>(EnemyStateType.ATTACK)?.OnAreaAttack();
    }

    private IEnumerator HitStopCoroutine()
    {
        // 已交由 HitStopManager 统一管理，此协程不再直接使用
        yield break;
    }

    private void PlayHitStop(Transform attacker)
    {
        // 冻结敌人自己
        HitStopManager.FreezeAnimator(animator, hitStopFrameCount);

        // 冻结攻击者
        if (attacker != null)
        {
            Animator attackerAnim = attacker.GetComponentInChildren<Animator>();
            if (attackerAnim != null)
                HitStopManager.FreezeAnimator(attackerAnim, hitStopFrameCount);
        }
    }

    private const float BlockAngle = 45f;

    public bool TakeDamage(string hitDirTag, Vector3 hitDirection, bool isLauncher, Transform attacker, float damage, bool powerAttack = false, float knockbackForce = 0f, float knockbackDuration = 0f, float knockbackUpForce = 0f)
    {
        if (fsm.stateType == EnemyStateType.DEATH) return false;

        // 按模式调整玩家对敌人的伤害
        damage *= GameModeSettings.PlayerDamageMultiplier;

        // 愤怒状态下受到伤害 +20%
        var vitals = GetComponent<EnemyVitals>();
        if (vitals != null && vitals.RagePercent >= 1f)
            damage *= 1.2f;

        // === 霸体判定 ===
        bool isSuperArmor = (fsm.stateType == EnemyStateType.ATTACK
            && skillManager != null && skillManager.CurrentSkill != null
            && skillManager.CurrentSkill.superArmor)
            || fsm.stateType == EnemyStateType.PARRYATTACK;

        if (isSuperArmor)
        {
            vitals?.TakeDamage(damage);
            vitals?.OnHitReceived();
            if (vitals != null && vitals.IsDead)
            {
                fsm.SetState(EnemyStateType.DEATH);
                return true;   // ← 扣血了
            }
            PlayHitStop(attacker);
            return true;       // ← 扣血了
        }

        if (fsm.stateType == EnemyStateType.BLOCKBREAK)
        {
            vitals?.TakeDamage(damage);
            if (vitals != null && vitals.IsDead)
            {
                fsm.SetState(EnemyStateType.DEATH);
                return true;   // ← 扣血了
            }
            fsm.SetState(EnemyStateType.KNOCKDOWN);
            _blockCount = 0;
            return true;       // ← 扣血了
        }

        // Power攻击
        if (powerAttack)
        {
            vitals?.TakeDamage(damage);
            vitals?.OnHitReceived();
            if (vitals != null && vitals.IsDead)
            {
                fsm.SetState(EnemyStateType.DEATH);
                return true;   // ← 扣血了
            }
            fsm.GetState<EnemyHitState>(EnemyStateType.HIT).SetHitDirectionTag(hitDirTag);
            fsm.GetState<EnemyHitState>(EnemyStateType.HIT).SetAttacker(attacker);
            if (fsm.stateType == EnemyStateType.HIT)
            {
                _blockCount = 0;
                fsm.GetState<EnemyHitState>(EnemyStateType.HIT).Rehit();
                fsm.GetState<EnemyHitState>(EnemyStateType.HIT).StartKnockback(hitDirection, knockbackForce, knockbackDuration, knockbackUpForce);
                PlayHitStop(attacker);
                return true;   // ← 扣血了
            }
            _blockCount = 0;
            fsm.SetState(EnemyStateType.HIT);
            fsm.GetState<EnemyHitState>(EnemyStateType.HIT).StartKnockback(hitDirection, knockbackForce, knockbackDuration, knockbackUpForce);
            PlayHitStop(attacker);
            return true;       // ← 扣血了
        }

        // KNOCKDOWN
        if (fsm.stateType == EnemyStateType.KNOCKDOWN)
        {
            vitals?.TakeDamage(damage * 2f);
            if (vitals != null && vitals.IsDead)
            {
                fsm.SetState(EnemyStateType.DEATH);
                return true;   // ← 扣血了
            }
            vitals?.OnHitReceived();
            PlayHitStop(attacker);
            return true;       // ← 扣血了
        }

        // 倒地/起身无敌
        if (fsm.stateType == EnemyStateType.FALLTOFLOOR ||
            fsm.stateType == EnemyStateType.GETUP)
        {
            return false;      // ← 没扣血
        }

        // 浮空/空中受击
        if (isLauncher || !IsGrounded)
        {
            vitals?.TakeDamage(damage);
            if (vitals != null && vitals.IsDead)
            {
                fsm.SetState(EnemyStateType.DEATH);
                return true;   // ← 扣血了
            }
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
                _blockCount = 0;
                fsm.SetState(EnemyStateType.AIR_HIT);
            }
            return true;       // ← 扣血了
        }

        // 格挡/闪避判定
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
            if (roll < 0.65f)
            {
                _blockCount++;
                if (_blockCount >= 3)
                {
                    _blockCount = 0;
                    fsm.GetState<EnemyParryAttackState>(EnemyStateType.PARRYATTACK).SetAttacker(attacker);
                    fsm.SetState(EnemyStateType.PARRYATTACK);
                    PlayHitStop(attacker);
                    return false;  // ← 没扣血（弹刀攻击）
                }
                if (fsm.stateType == EnemyStateType.BLOCK)
                {
                    fsm.GetState<EnemyBlockState>(EnemyStateType.BLOCK).SetAttacker(attacker);
                    fsm.GetState<EnemyBlockState>(EnemyStateType.BLOCK).Rehit();
                    PlayHitStop(attacker);
                    return false;  // ← 没扣血（格挡）
                }
                if (fsm.stateType == EnemyStateType.HIT || fsm.stateType == EnemyStateType.DODGE)
                {
                    fsm.GetState<EnemyBlockState>(EnemyStateType.BLOCK).SetAttacker(attacker);
                    fsm.SetState(EnemyStateType.BLOCK);
                    PlayHitStop(attacker);
                    return false;  // ← 没扣血（格挡）
                }
                fsm.GetState<EnemyBlockState>(EnemyStateType.BLOCK).SetAttacker(attacker);
                fsm.SetState(EnemyStateType.BLOCK);
                PlayHitStop(attacker);
                return false;      // ← 没扣血（格挡）
            }
            else if (roll < 0.75f)
            {
                if (fsm.stateType == EnemyStateType.DODGE)
                {
                    fsm.GetState<EnemyDodgeState>(EnemyStateType.DODGE).SetAttacker(attacker);
                    fsm.GetState<EnemyDodgeState>(EnemyStateType.DODGE).Rehit();
                    return false;  // ← 没扣血（闪避）
                }
                if (fsm.stateType == EnemyStateType.HIT || fsm.stateType == EnemyStateType.BLOCK)
                {
                    fsm.GetState<EnemyDodgeState>(EnemyStateType.DODGE).SetAttacker(attacker);
                    fsm.SetState(EnemyStateType.DODGE);
                    return false;  // ← 没扣血（闪避）
                }
                fsm.GetState<EnemyDodgeState>(EnemyStateType.DODGE).SetAttacker(attacker);
                fsm.SetState(EnemyStateType.DODGE);
                return false;      // ← 没扣血（闪避）
            }
        }

        // 普通受击
        vitals?.TakeDamage(damage);
        vitals?.OnHitReceived();
        if (vitals != null && vitals.IsDead)
        {
            fsm.SetState(EnemyStateType.DEATH);
            return true;           // ← 扣血了
        }


        if (fsm.stateType == EnemyStateType.HIT)
        {
            _blockCount = 0;
            fsm.GetState<EnemyHitState>(EnemyStateType.HIT).SetHitDirectionTag(hitDirTag);
            fsm.GetState<EnemyHitState>(EnemyStateType.HIT).SetAttacker(attacker);
            fsm.GetState<EnemyHitState>(EnemyStateType.HIT).Rehit();
            fsm.GetState<EnemyHitState>(EnemyStateType.HIT).StartKnockback(hitDirection, knockbackForce, knockbackDuration, knockbackUpForce);
            PlayHitStop(attacker);
            return true;           // ← 扣血了
        }
        _blockCount = 0;
        fsm.GetState<EnemyHitState>(EnemyStateType.HIT).SetHitDirectionTag(hitDirTag);
        fsm.GetState<EnemyHitState>(EnemyStateType.HIT).SetAttacker(attacker);
        fsm.SetState(EnemyStateType.HIT);
        fsm.GetState<EnemyHitState>(EnemyStateType.HIT).StartKnockback(hitDirection, knockbackForce, knockbackDuration, knockbackUpForce);
        PlayHitStop(attacker);
        return true;               // ← 扣血了
    }

    public void ApplyKnockback(Vector3 direction, float force, float duration, float upForce = 0f)
    {
        if (fsm.stateType == EnemyStateType.DEATH) return;
        if (force <= 0f || duration <= 0f) return;

        var hitState = fsm.GetState<EnemyHitState>(EnemyStateType.HIT);
        hitState.SetHitDirectionTag("F");
        hitState.SetAttacker(null);

        if (fsm.stateType == EnemyStateType.HIT)
            hitState.Rehit();
        else
            fsm.SetState(EnemyStateType.HIT);

        hitState.StartKnockback(direction, force, duration, upForce);
    }

    public void SetEnemyWeaponDamage(float damage)
    {
        if (enemyWeaponHitDetector != null)
            enemyWeaponHitDetector.SetCurrentDamage(damage);
    }

    public void OnEnemyHitWindowOpen()
    {
        enemyWeaponHitDetector?.OnEnemyHitWindowOpen();
    }

    public void OnEnemyHitWindowClose()
    {
        enemyWeaponHitDetector?.OnEnemyHitWindowClose();
    }
}
