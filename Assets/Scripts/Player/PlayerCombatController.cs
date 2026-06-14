
using UnityEngine;
using System;

[System.Serializable]
public class PlayerCombatState
{
    public bool isCrouching;
}

[System.Serializable]
public class PlayerAttackConfig
{
    public string attackTriggerName = "LAtk";
    public float attackCooldown = 0.2f;
    public float attackDamage = 80f;

    [Header("命中顿帧")]
    public float hitStopDuration = 0.12f;
    public float hitStopTimeScale = 0.2f;
}

[System.Serializable]
public class PlayerAttackLockConfig
{
    public bool lockMoveDuringAttack = true;
    public string attackStateTag = "Attack";
    public float attackStateGraceTime = 0.15f;
    public bool allowAttackInputDuringAttack = true;

    [Header("移动动作限制")]
    public bool allowNormalAttackDuringRoll;
    public bool allowNormalAttackDuringSprint;
}

[System.Serializable]
public class PlayerAttackMoveConfig
{
    public bool useAnimationMove = true;
    public string animationMoveParam = "AnimationMove";
    public float attackMoveSpeed = 2.5f;
    public float threshold = 0.1f;
}

[System.Serializable]
public class PlayerAttackAutoFaceConfig
{
    [Header("攻击自动朝向敌人")]
    public bool enableAutoFaceTarget = true;
    public float searchRadius = 4f;
    [Range(0f, 180f)] public float maxSearchAngle = 120f;
    public LayerMask enemyLayerMask;
    public bool snapFaceOnAttackStart = true;
    public bool keepFacingDuringAttack = true;
    public float rotateSpeed = 720f;
    public float loseTargetDistanceMultiplier = 1.3f;
    public bool ignoreInvalidActor = true;
}

[System.Serializable]
public class AttackComboWindowConfig
{
    public string stateFullPath;
    public float allowNextAttackAfterStartTime = 0.2f;
}

[System.Serializable]
public class PlayerComboConfig
{
    public bool useComboInputBuffer = true;
    public bool bufferEarlyAttackInput = true;
    public float defaultAllowNextAttackAfterStartTime = 0.2f;
    public AttackComboWindowConfig[] comboWindowConfigs;
}

public class PlayerCombatController : MonoBehaviour
{
    [Header("玩家战斗状态")]
    public PlayerCombatState combatState = new();

    [Header("攻击参数")]
    public PlayerAttackConfig attackConfig = new();

    [Header("攻击移动锁")]
    public PlayerAttackLockConfig attackLockConfig = new();

    [Header("攻击动画位移")]
    public PlayerAttackMoveConfig attackMoveConfig = new();

    [Header("攻击自动朝向")]
    public PlayerAttackAutoFaceConfig autoFaceConfig = new();

    [Header("连击配置")]
    public PlayerComboConfig comboConfig = new();

    [Header("武器 Hitbox")]
    public WeaponHitbox weaponHitbox;

    private ActorBase actor;
    private Animator animator;
    private CharacterController characterController;
    private PlayerMoveController playerMove;

    private float nextAttackTime;
    private float attackStartTime;
    private bool hasBufferedNextAttackInput;
    private float rollEndTime; // 翻滚结束时间，用于判断是否在翻滚后窗口期内
    private const float ROLL_ATTACK_WINDOW = 0.2f; // 翻滚后攻击窗口时间
    private int currentAttackStateHash;
    private float currentAttackStateEnterTime;
    private Transform currentAttackTarget;

    private Coroutine hitStopCoroutine;
    private bool isHitStopping;

    private void Awake()
    {
        actor = GetComponent<ActorBase>();
        animator = GetComponentInChildren<Animator>();
        characterController = GetComponent<CharacterController>();
        playerMove = GetComponent<PlayerMoveController>();

        if (weaponHitbox != null)
        {
            weaponHitbox.OnHitEnemy += OnHitEnemy;
        }
    }

    private void Start() => combatState.isCrouching = false;

    private void Update()
    {
        TickAttackState();
        TickCurrentAttackState();
        TickBufferedNextAttackInput();
        TickAttackAutoFaceTarget();
        ApplyAttackAnimationMove();
    }

    private bool IsActorInvalid() => !actor;
    private bool IsHit() => actor && actor.actorState.isHit;
    private bool IsMovingActionLocked() => playerMove && playerMove.IsMoveActionLocked();
    private bool IsBusyForCrouch() => IsActorInvalid() || IsHit() || IsAttackMoveLocked() || IsMovingActionLocked();
    public bool IsCurrentlyAttacking() => actor && actor.actorState.isAttacking || IsInAttackAnimationTag();
    public bool IsAttackMoveLocked() => attackLockConfig.lockMoveDuringAttack && (actor && actor.actorState.isAttacking || IsInAttackAnimationTag());
    public bool IsInAttackAnimationTag() => IsInAnimatorTag(attackLockConfig.attackStateTag);

    private bool IsInAnimatorTag(string tagName)
    {
        if (!animator || string.IsNullOrEmpty(tagName)) return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsTag(tagName)) return true;

        if (animator.IsInTransition(0))
            return animator.GetNextAnimatorStateInfo(0).IsTag(tagName);

        return false;
    }

    public void TickAttackState()
    {
        if (!actor || !actor.actorState.isAttacking || !animator) return;
        if (Time.time - attackStartTime < attackLockConfig.attackStateGraceTime) return;
        if (animator.IsInTransition(0) || IsInAttackAnimationTag()) return;

        FinishAttack();
    }

    public bool CanAcceptAttackInput() => !IsActorInvalid() && !IsHit() && !combatState.isCrouching;

    // 移除移动动作阻止攻击的限制，允许在翻滚/冲刺期间缓冲攻击输入
    private bool IsMoveActionBlockingAttack() => false;

    public bool IsCombatActionLockedForMoveAction() => IsActorInvalid() || IsHit() || IsAttackMoveLocked() || combatState.isCrouching;

    private bool CanAttack() => CanAcceptAttackInput() &&
        (attackLockConfig.allowAttackInputDuringAttack && IsCurrentlyAttacking() || Time.time >= nextAttackTime);

    public void TryAttackWithComboWindow()
    {
        // 如果正在翻滚或在翻滚后窗口期内，立即触发攻击（不走连击逻辑）
        if (IsInRollAttackWindow())
        {
            TryRollAttack();
            return;
        }

        if (!CanAcceptAttackInput()) return;

        if (!IsCurrentlyAttacking())
        {
            ClearBufferedNextAttackInput();
            TryAttack();
            return;
        }

        if (!comboConfig.useComboInputBuffer)
        {
            TryAttack();
            return;
        }

        if (CanChainNextAttackNow())
        {
            ClearBufferedNextAttackInput();
            TryAttack();
            return;
        }

        if (comboConfig.bufferEarlyAttackInput)
            hasBufferedNextAttackInput = true;
    }

    private bool IsInRollAttackWindow()
    {
        bool isRolling = playerMove != null && playerMove.IsRolling();
        bool inRollEndWindow = Time.time - rollEndTime <= ROLL_ATTACK_WINDOW && rollEndTime > 0;
        return isRolling || inRollEndWindow;
    }

    public void SetRollEndTime(float time)
    {
        rollEndTime = time;
    }

    public void ResetRollEndTime()
    {
        rollEndTime = 0;
    }

    private void TryRollAttack()
    {
        if (!animator) return;
        
        // 直接触发攻击，走Roll->LAtk_4的过渡
        animator.ResetTrigger(attackConfig.attackTriggerName);
        animator.SetTrigger(attackConfig.attackTriggerName);
        
        actor.actorState.isAttacking = true;
        attackStartTime = Time.time;
        nextAttackTime = Time.time + attackConfig.attackCooldown;
        
        RefreshAttackTarget();
        
        if (autoFaceConfig.enableAutoFaceTarget && autoFaceConfig.snapFaceOnAttackStart && currentAttackTarget != null)
            FaceTargetImmediately(currentAttackTarget);
        
        ResetRollEndTime();
    }

    private void TryAttack()
    {
        if (!animator || !CanAttack()) return;

        if (!IsCurrentlyAttacking())
            nextAttackTime = Time.time + attackConfig.attackCooldown;

        actor.actorState.isAttacking = true;
        attackStartTime = Time.time;

        RefreshAttackTarget();

        if (autoFaceConfig.enableAutoFaceTarget && autoFaceConfig.snapFaceOnAttackStart && currentAttackTarget != null)
            FaceTargetImmediately(currentAttackTarget);

        animator.ResetTrigger(attackConfig.attackTriggerName);
        animator.SetTrigger(attackConfig.attackTriggerName);
    }

    public void FinishAttack()
    {
        if (actor) actor.actorState.isAttacking = false;
        currentAttackTarget = null;
    }

    public void CancelAttack()
    {
        FinishAttack();
        animator?.ResetTrigger(attackConfig.attackTriggerName);
        ClearBufferedNextAttackInput();
        weaponHitbox?.EndHitbox();
    }

    public void TickCurrentAttackState()
    {
        if (!animator) return;

        int hash = GetCurrentOrNextAttackStateHash();
        if (hash == 0)
        {
            currentAttackStateHash = 0;
            currentAttackStateEnterTime = 0;
            return;
        }

        if (currentAttackStateHash == hash) return;

        currentAttackStateHash = hash;
        currentAttackStateEnterTime = Time.time;
    }

    public void TickBufferedNextAttackInput()
    {
        if (!hasBufferedNextAttackInput) return;

        if (!CanAcceptAttackInput() || !IsCurrentlyAttacking())
        {
            ClearBufferedNextAttackInput();
            return;
        }

        if (!CanChainNextAttackNow()) return;

        ClearBufferedNextAttackInput();
        TryAttack();
    }

    private bool CanChainNextAttackNow()
    {
        int hash = GetCurrentOrNextAttackStateHash();
        return hash != 0 && Time.time - currentAttackStateEnterTime >= GetCurrentAttackAllowNextTime();
    }

    private int GetCurrentOrNextAttackStateHash()
    {
        if (!animator) return 0;

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (next.IsTag(attackLockConfig.attackStateTag)) return next.fullPathHash;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        return current.IsTag(attackLockConfig.attackStateTag) ? current.fullPathHash : 0;
    }

    private float GetCurrentAttackAllowNextTime()
    {
        if (currentAttackStateHash != 0 && comboConfig.comboWindowConfigs != null)
        {
            foreach (var config in comboConfig.comboWindowConfigs)
            {
                if (config != null && !string.IsNullOrEmpty(config.stateFullPath) &&
                    Animator.StringToHash(config.stateFullPath) == currentAttackStateHash)
                    return Mathf.Max(0f, config.allowNextAttackAfterStartTime);
            }
        }

        return Mathf.Max(0f, comboConfig.defaultAllowNextAttackAfterStartTime);
    }

    public void ClearBufferedNextAttackInput() => hasBufferedNextAttackInput = false;

    public void TickAttackAutoFaceTarget()
    {
        if (!autoFaceConfig.enableAutoFaceTarget || !autoFaceConfig.keepFacingDuringAttack || !IsCurrentlyAttacking())
            return;

        if (currentAttackTarget == null)
        {
            RefreshAttackTarget();
            return;
        }

        if (!IsAttackTargetValid(currentAttackTarget))
        {
            currentAttackTarget = null;
            return;
        }

        float loseDistance = autoFaceConfig.searchRadius * autoFaceConfig.loseTargetDistanceMultiplier;
        if ((currentAttackTarget.position - transform.position).sqrMagnitude > loseDistance * loseDistance)
        {
            currentAttackTarget = null;
            return;
        }

        FaceTargetSmoothly(currentAttackTarget);
    }

    private void RefreshAttackTarget() => currentAttackTarget = FindNearestAttackTarget();

    private Transform FindNearestAttackTarget()
    {
        if (!autoFaceConfig.enableAutoFaceTarget) return null;

        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            autoFaceConfig.searchRadius,
            autoFaceConfig.enemyLayerMask,
            QueryTriggerInteraction.Ignore
        );

        Transform nearest = null;
        float nearestScore = float.MaxValue;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        foreach (Collider col in colliders)
        {
            if (col == null) continue;

            Transform target = col.transform;
            ActorBase targetActor = col.GetComponentInParent<ActorBase>();
            if (targetActor != null) target = targetActor.transform;

            if (!IsAttackTargetValid(target)) continue;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.0001f) continue;

            float angle = Vector3.Angle(forward, toTarget.normalized);
            if (angle > autoFaceConfig.maxSearchAngle) continue;

            float score = toTarget.sqrMagnitude + angle * 0.02f;
            if (score < nearestScore)
            {
                nearestScore = score;
                nearest = target;
            }
        }

        return nearest;
    }

    private bool IsAttackTargetValid(Transform target)
    {
        if (target == null || target == transform || !target.gameObject.activeInHierarchy)
            return false;

        if (autoFaceConfig.ignoreInvalidActor)
        {
            ActorBase targetActor = target.GetComponentInParent<ActorBase>();
            if (targetActor != null) return true;
        }

        return true;
    }

    private void FaceTargetImmediately(Transform target)
    {
        if (target == null) return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void FaceTargetSmoothly(Transform target)
    {
        if (target == null) return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            autoFaceConfig.rotateSpeed * Time.deltaTime
        );
    }

    public void ApplyAttackAnimationMove()
    {
        if (!CanApplyAttackAnimationMove()) return;

        float value = animator.GetFloat(attackMoveConfig.animationMoveParam);
        if (Mathf.Abs(value) <= attackMoveConfig.threshold) return;

        Vector3 delta = transform.forward * value * attackMoveConfig.attackMoveSpeed * Time.deltaTime;

        if (characterController && characterController.enabled)
            characterController.Move(delta);
        else
            transform.position += delta;
    }

    private bool CanApplyAttackAnimationMove() =>
        attackMoveConfig.useAnimationMove && actor && !actor.actorState.isHit &&
        !IsMovingActionLocked() && animator && IsCurrentlyAttacking();

    public void OnAnimationAttackEvent(string hitStateName) => weaponHitbox?.BeginHitbox(hitStateName, attackConfig.attackDamage);
    public void OnAnimationAttackEndEvent() => weaponHitbox?.EndHitbox();
    public void ToggleCrouch() { if (!IsBusyForCrouch()) combatState.isCrouching = !combatState.isCrouching; }

    private void OnHitEnemy(GameObject enemy)
    {
        StartHitStop();
    }

    private void StartHitStop()
    {
        if (attackConfig.hitStopDuration <= 0 || isHitStopping) return;

        if (hitStopCoroutine != null)
            StopCoroutine(hitStopCoroutine);

        hitStopCoroutine = StartCoroutine(HitStopCoroutine());
    }

    private System.Collections.IEnumerator HitStopCoroutine()
    {
        isHitStopping = true;
        Time.timeScale = attackConfig.hitStopTimeScale;

        yield return new WaitForSecondsRealtime(attackConfig.hitStopDuration);

        Time.timeScale = 1f;
        isHitStopping = false;
        hitStopCoroutine = null;
    }

    private void OnDestroy()
    {
        if (weaponHitbox != null)
        {
            weaponHitbox.OnHitEnemy -= OnHitEnemy;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (autoFaceConfig == null || !autoFaceConfig.enableAutoFaceTarget) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, autoFaceConfig.searchRadius);
    }
#endif
}

