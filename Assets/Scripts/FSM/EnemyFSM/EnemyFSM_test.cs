using UnityEngine;
using System.Collections;

public partial class EnemyFSM_test : MonoBehaviour, ICombatTarget, ICombatant, EnemyFSMBT_test.IEnemyFsmAccess
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private CharacterController controller;
    private BehaviorTree behaviorTree;
    private EnemyFSMBT_test btBuilder;
    private EnemySkillManager_test skillManager;
    private EnemyWeaponHitDetector_test enemyWeaponHitDetector;
    private EnemyVitals vitals;
    private int _blockCount;
    private EnemyAttackState_test attackState;

    [Header("测试模式")] public bool testMode;
    [Header("视野参数")] public float visionRange = 8f; public float visionAngle = 120f; public float loseRange = 10f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float visionHeightOffset = 1f;
    [SerializeField] private int visionRayCount = 10;
    [Header("地面检测")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundCheckDistance = 0.15f;
    [HideInInspector] public bool hasTarget;
    [HideInInspector] public Transform targetPlayer;
    [HideInInspector] public Vector3 lastKnownPlayerPos;
    [Header("顿帧设置")] public int hitStopFrameCount = 8;
    private float memoryDuration = 2f;
    private float memoryTimer;
    private const float BlockAngle = 160f;

    public bool IsGrounded
    {
        get
        {
            if (controller == null) return false;
            Vector3 origin = transform.position + controller.center;
            float radius = controller.radius * 0.8f;
            float checkDist = controller.height / 2f - controller.radius + groundCheckDistance;
            return Physics.SphereCast(origin, radius, Vector3.down, out _, checkDist, groundLayer);
        }
    }

    public bool IsInHitState => fsm != null && (fsm.stateType == EnemyStateType.HIT || fsm.stateType == EnemyStateType.AIR_HIT || fsm.stateType == EnemyStateType.BLOCK || fsm.stateType == EnemyStateType.DODGE || fsm.stateType == EnemyStateType.BLOCKBREAK || fsm.stateType == EnemyStateType.KNOCKDOWN || fsm.stateType == EnemyStateType.FALLTOFLOOR || fsm.stateType == EnemyStateType.PARRYATTACK || fsm.stateType == EnemyStateType.GETUP);

    // ===== ICombatTarget =====
    Transform ICombatTarget.Transform => transform;
    bool ICombatTarget.IsAlive => vitals != null && !vitals.IsDead;

    bool ICombatTarget.TakeHit(HitContext hit)
    {
        if (vitals == null || vitals.IsDead) return false;
        if (hit.IgnoreBlock) { vitals.TakeDamage(hit.Damage); vitals.OnHitReceived(); if (vitals.IsDead) fsm.SetState(EnemyStateType.DEATH); return true; }
        return TakeDamageInternal(hit);
    }

    // ===== ICombatant =====
    Transform ICombatant.Transform => transform;
    ActorVitals ICombatant.Vitals => vitals;

    // ===== IEnemyFsmAccess =====
    Transform EnemyFSMBT_test.IEnemyFsmAccess.Transform => transform;
    bool EnemyFSMBT_test.IEnemyFsmAccess.IsGrounded => IsGrounded;
    bool EnemyFSMBT_test.IEnemyFsmAccess.HasTarget => hasTarget;
    Transform EnemyFSMBT_test.IEnemyFsmAccess.TargetPlayer => targetPlayer;
    LayerMask EnemyFSMBT_test.IEnemyFsmAccess.PlayerLayer => playerLayer;
    void EnemyFSMBT_test.IEnemyFsmAccess.SetDesiredState(EnemyStateType s) => BT_SetDesiredState(s);
    void EnemyFSMBT_test.IEnemyFsmAccess.DirectSetState(EnemyStateType s) => DirectSetState(s);
    void EnemyFSMBT_test.IEnemyFsmAccess.SetWeaponDamage(float dmg) => enemyWeaponHitDetector?.SetCurrentDamage(dmg);
    EnemyAttackState_test EnemyFSMBT_test.IEnemyFsmAccess.GetAttackState() => attackState;

    public void DirectSetState(EnemyStateType s) { fsm?.SetState(s); }
    public void BT_SetDesiredState(EnemyStateType s) { if (behaviorTree != null) behaviorTree.blackboard.desiredState = s; }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        CombatAudioPlayer audioPlayer = GetComponent<CombatAudioPlayer>();
        skillManager = GetComponent<EnemySkillManager_test>();
        enemyWeaponHitDetector = GetComponentInChildren<EnemyWeaponHitDetector_test>();
        vitals = GetComponent<EnemyVitals>();
        fsm = new EnemyFSMControl();
        HitStopManager_test.EnsureHost(this);

        fsm.AddState(EnemyStateType.IDLE, new EnemyIdleState(animator, fsm, this, controller));
        fsm.AddState(EnemyStateType.MOVE, new EnemyMoveState(animator, fsm, transform, controller, this));
        fsm.AddState(EnemyStateType.HIT, new EnemyHitState(animator, fsm, transform, audioPlayer));
        fsm.AddState(EnemyStateType.AIR_HIT, new EnemyAirHitState(animator, fsm, transform, controller, this, audioPlayer));
        fsm.AddState(EnemyStateType.BLOCK, new EnemyBlockState(animator, fsm, this, transform, audioPlayer));
        fsm.AddState(EnemyStateType.DODGE, new EnemyDodgeState(animator, fsm, this, transform, controller));
        attackState = new EnemyAttackState_test(animator, fsm, this, transform, this);
        fsm.AddState(EnemyStateType.ATTACK, attackState);
        fsm.AddState(EnemyStateType.BLOCKBREAK, new EnemyBlockBreakState(animator, fsm, this, transform, audioPlayer));
        fsm.AddState(EnemyStateType.KNOCKDOWN, new EnemyKnockDownState(animator, fsm, this));
        fsm.AddState(EnemyStateType.FALLTOFLOOR, new EnemyFallToFloorState(animator, fsm, this, controller));
        fsm.AddState(EnemyStateType.GETUP, new EnemyGetUpState(animator, fsm, this));
        fsm.AddState(EnemyStateType.LOCK_MOVE, new EnemyLockMoveState(animator, fsm, transform, controller, this));
        fsm.AddState(EnemyStateType.DEATH, new EnemyDeathState(animator, fsm, this, controller));
        fsm.AddState(EnemyStateType.PARRYATTACK, new EnemyParryAttackState(animator, fsm, this, transform, audioPlayer));
        fsm.SetState(EnemyStateType.IDLE);

        btBuilder = new EnemyFSMBT_test();
        behaviorTree = btBuilder.BuildTree(this, skillManager);

        vitals.OnPostureFull += () => fsm.SetState(EnemyStateType.BLOCKBREAK);

        var ui = GetComponentInChildren<EnemyVitalsUI_test>();
        if (ui != null) ui.Bind(vitals);
    }

    private void Update()
    {
        if (fsm.stateType == EnemyStateType.DEATH) { fsm.OnTick(); return; }
        if (testMode) { if (!IsInHitState && fsm.stateType != EnemyStateType.IDLE) fsm.SetState(EnemyStateType.IDLE); hasTarget = false; targetPlayer = null; memoryTimer = 0; fsm.OnTick(); return; }
        UpdateVision();
        if (!IsInHitState)
        {
            if (fsm.stateType == EnemyStateType.ATTACK) { fsm.OnTick(); return; }
            behaviorTree.Tick();
            if (fsm.stateType != EnemyStateType.ATTACK)
            {
                switch (behaviorTree.blackboard.desiredState)
                {
                    case EnemyStateType.MOVE: if (fsm.stateType != EnemyStateType.MOVE) fsm.SetState(EnemyStateType.MOVE); break;
                    case EnemyStateType.LOCK_MOVE: if (fsm.stateType != EnemyStateType.LOCK_MOVE) fsm.SetState(EnemyStateType.LOCK_MOVE); break;
                    default: if (fsm.stateType != EnemyStateType.IDLE) fsm.SetState(EnemyStateType.IDLE); break;
                }
            }
        }
        else { behaviorTree.blackboard.btControlEnabled = false; }
        fsm.OnTick();
    }

    private void UpdateVision()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * visionHeightOffset;
        float halfAngle = visionAngle * 0.5f;
        bool detectedThisFrame = false;
        RaycastHit detectedHit = default;
        float sphereRadius = 0.3f;
        for (int i = 0; i < visionRayCount; i++)
        {
            float currentAngle = -halfAngle + (visionAngle / (visionRayCount - 1)) * i;
            Vector3 rayDir = Quaternion.Euler(0f, currentAngle, 0f) * transform.forward;
            rayDir.y = 0f;
            if (Physics.SphereCast(rayOrigin, sphereRadius, rayDir.normalized, out RaycastHit hit, visionRange))
            {
                if ((playerLayer.value & (1 << hit.collider.gameObject.layer)) != 0) { detectedThisFrame = true; detectedHit = hit; break; }
            }
        }
        if (detectedThisFrame) { targetPlayer = detectedHit.transform; lastKnownPlayerPos = detectedHit.transform.position; memoryTimer = memoryDuration; hasTarget = true; }
        else if (hasTarget && targetPlayer != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, targetPlayer.position);
            if (distanceToTarget <= loseRange && memoryTimer > 0) { memoryTimer -= Time.deltaTime; lastKnownPlayerPos = targetPlayer.position; }
            else { hasTarget = false; targetPlayer = null; memoryTimer = 0; }
        }
    }

    public void OnAttackComboCheck() { fsm.GetState<EnemyAttackState>(EnemyStateType.ATTACK)?.OnAttackComboCheck(); }
    public void OnAreaAttack() { fsm.GetState<EnemyAttackState>(EnemyStateType.ATTACK)?.OnAreaAttack(); }

    private bool TakeDamageInternal(HitContext hit)
    {
        if (vitals.IsDead) return false;
        float damage = hit.Damage;

        if (hit.Attacker != null)
        {
            Vector3 toAttacker = hit.Attacker.position - transform.position;
            toAttacker.y = 0;
            if (toAttacker.magnitude > 0.01f)
            {
                float angle = Vector3.Angle(transform.forward, toAttacker);
                if (angle <= BlockAngle)
                {
                    float roll = Random.value;
                    if (roll < 0.65f) { _blockCount++; if (_blockCount >= 3) { _blockCount = 0; fsm.GetState<EnemyParryAttackState>(EnemyStateType.PARRYATTACK)?.SetAttacker(hit.Attacker); fsm.SetState(EnemyStateType.PARRYATTACK); PlayHitStop(hit.Attacker); return false; } if (fsm.stateType == EnemyStateType.BLOCK) { fsm.GetState<EnemyBlockState>(EnemyStateType.BLOCK)?.SetAttacker(hit.Attacker); fsm.GetState<EnemyBlockState>(EnemyStateType.BLOCK)?.Rehit(); PlayHitStop(hit.Attacker); return false; } fsm.GetState<EnemyBlockState>(EnemyStateType.BLOCK)?.SetAttacker(hit.Attacker); fsm.SetState(EnemyStateType.BLOCK); PlayHitStop(hit.Attacker); return false; }
                    else if (roll < 0.75f) { if (fsm.stateType == EnemyStateType.DODGE) { fsm.GetState<EnemyDodgeState>(EnemyStateType.DODGE)?.SetAttacker(hit.Attacker); fsm.GetState<EnemyDodgeState>(EnemyStateType.DODGE)?.Rehit(); return false; } fsm.GetState<EnemyDodgeState>(EnemyStateType.DODGE)?.SetAttacker(hit.Attacker); fsm.SetState(EnemyStateType.DODGE); return false; }
                }
            }
        }

        vitals?.TakeDamage(damage);
        vitals?.OnHitReceived();
        if (vitals != null && vitals.IsDead) { fsm.SetState(EnemyStateType.DEATH); return true; }

        if (fsm.stateType == EnemyStateType.HIT) { _blockCount = 0; fsm.GetState<EnemyHitState>(EnemyStateType.HIT)?.SetHitDirectionTag(hit.DirTag); fsm.GetState<EnemyHitState>(EnemyStateType.HIT)?.SetAttacker(hit.Attacker); fsm.GetState<EnemyHitState>(EnemyStateType.HIT)?.Rehit(); PlayHitStop(hit.Attacker); return true; }
        _blockCount = 0; fsm.GetState<EnemyHitState>(EnemyStateType.HIT)?.SetHitDirectionTag(hit.DirTag); fsm.GetState<EnemyHitState>(EnemyStateType.HIT)?.SetAttacker(hit.Attacker); fsm.SetState(EnemyStateType.HIT); PlayHitStop(hit.Attacker);
        return true;
    }

    private void PlayHitStop(Transform attacker)
    {
        HitStopManager_test.FreezeAnimator(animator, hitStopFrameCount);
        if (attacker != null) { Animator attackerAnim = attacker.GetComponentInChildren<Animator>(); if (attackerAnim != null) HitStopManager_test.FreezeAnimator(attackerAnim, hitStopFrameCount); }
    }

    public void SetEnemyWeaponDamage(float damage) { enemyWeaponHitDetector?.SetCurrentDamage(damage); }
    public void OnEnemyHitWindowOpen() { enemyWeaponHitDetector?.OnEnemyHitWindowOpen(); }
    public void OnEnemyHitWindowClose() { enemyWeaponHitDetector?.OnEnemyHitWindowClose(); }

    public EnemyAttackState_test GetAttackState() => attackState;
}
