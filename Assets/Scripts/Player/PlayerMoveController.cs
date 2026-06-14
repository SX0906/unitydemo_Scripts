using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class PlayerReferenceGroup
{
    public Transform lookRoot;
    public Transform groundCheck;
    public PlayerCameraController playerCamera;
}

[System.Serializable]
public class PlayerInputActionNames
{
    public string moveActionName = "Move";
    public string sprintActionName = "Sprint";
    public string runActionName = "Run";
    public string lockOnActionName = "LockOn";
    public string crouchActionName = "Crouch";
    public string attackActionName = "Attack";
    public string rollActionName = "Roll";
}

[System.Serializable]
public class PlayerMoveConfig
{
    public float sprintSpeed = 6f;
    public float rotationSmoothTime = 0.08f;
}

[System.Serializable]
public class PlayerLockMoveConfig
{
    public float lockWalkSpeed = 2.5f;
    public float lockRunSpeed = 4.5f;
    public float lockRotateSpeed = 12f;
    public float lockSearchRadius = 15f;
    public LayerMask targetLayers;
}

[System.Serializable]
public class PlayerGroundCheckConfig
{
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayers;
}

[System.Serializable]
public class PlayerMoveRuntimeState
{
    public Vector2 moveInput;
    public bool hasMove;
    public bool runToggled;
    public bool isLockedOn;
    public Transform lockTarget;
}

[System.Serializable]
public class PlayerMoveActionConfig
{
    public string rollTriggerName = "Roll";
    public string rollStateTag = "Roll";
    public float rollGraceTime = 0.1f;
    public string sprintTriggerName = "Sprint";
    public string sprintStateTag = "Sprint";
    public float sprintGraceTime = 0.1f;
    public bool useAnimationMove = true;
    public string animationMoveParam = "AnimationMove";
    public float animationMoveSpeed = 8f;
    public float threshold = 0.01f;
    public bool requireGrounded = true;
    public float invincibleDelayAfterAction = 0.2f;
}

[System.Serializable]
public class PlayerMoveActionRuntimeState
{
    public bool isRolling;
    public bool isSprinting;
    public float rollStartTime;
    public float sprintStartTime;
    public float invincibleEndTime;
}

// 翻滚/冲刺移动/无敌状态
[System.Serializable]
public class PlayerMovePreActionState
{
    public Vector2 savedMoveInput;
    public bool savedHasMove;
    public bool savedRunToggled;
}

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerMoveController : ActorBase
{
    [Header("角色引用")] public PlayerReferenceGroup playerRefs = new();
    [Header("输入动作名")] public PlayerInputActionNames inputNames = new();
    [Header("基础移动参数")] public PlayerMoveConfig playerMoveConfig = new();
    [Header("锁定移动参数")] public PlayerLockMoveConfig lockMoveConfig = new();
    [Header("地面检测")] public PlayerGroundCheckConfig groundConfig = new();
    [Header("运行时状态")] public PlayerMoveRuntimeState playerMoveState = new();
    [Header("移动动作配置")] public PlayerMoveActionConfig moveActionConfig = new();
    [Header("动作状态")] public PlayerMoveActionRuntimeState moveActionState = new();
    [Header("动作前状态保存")] public PlayerMovePreActionState preActionState = new();

    private PlayerInput playerInput;
    private PlayerCombatController combat;
    private StaminaSystem stamina;
    private PlayerHealthController healthController;
    private PlayerSkillPlayer skillPlayer;
    private InputAction moveAction, sprintAction, runAction, lockOnAction, crouchAction, attackAction, rollAction;
    private float targetRotation, rotationVelocity;

    protected override void Awake()
    {
        base.Awake();
        playerInput = GetComponent<PlayerInput>();
        combat = GetComponent<PlayerCombatController>();
        stamina = GetComponent<StaminaSystem>();
        healthController = GetComponent<PlayerHealthController>();
        skillPlayer = GetComponent<PlayerSkillPlayer>();

        playerRefs.playerCamera ??= GetComponent<PlayerCameraController>();
        playerRefs.lookRoot ??= Camera.main?.transform;
        BindActions();
    }

    private void OnEnable()
    {
        BindActions();
        RegisterCallbacks(true);
    }

    private void OnDisable()
    {
        RegisterCallbacks(false);
    }

    private void Update()
    {
        ReadInput();
        CheckGrounded();
        TickMoveActions();
        HandleAllMovementLogic();
        ApplyGravity();
        ValidateLockState();
        UpdateAnimatorState();
        SyncCameraLockState();
    }

    private void HandleAllMovementLogic()
    {
        if (IsMoveActionLocked())
        {
            // 移动动作中不清除输入，但重置原始状态
            playerMoveState.moveInput = Vector2.zero;
            playerMoveState.hasMove = false;
            if (ShouldUseLockMove()) FaceLockTarget();
            ApplyMoveActionAnimationMove();
        }
        else if (CanMove())
        {
            if (ShouldUseLockMove()) HandleLockMove();
            else HandleFreeMove();
        }
        else
        {
            ClearMoveInputOnly();
            if (IsAttackLockedAndLocking()) FaceLockTarget();
        }
    }

    private bool CanMove()
    {
        bool isGuarding = healthController != null && healthController.IsGuarding();
        bool isPlayingSkill = skillPlayer != null && skillPlayer.IsSkillPlaying();

        return !actorState.isHit && !IsMoveActionLocked() && !(combat && combat.IsAttackMoveLocked()) && !isGuarding && !isPlayingSkill;
    }
    private bool ShouldUseLockMove() => playerMoveState.isLockedOn && playerMoveState.lockTarget;
    private bool IsAttackLockedAndLocking() => combat && combat.IsAttackMoveLocked() && ShouldUseLockMove();

    private void ValidateLockState()
    {
        if (!playerMoveState.isLockedOn) return;
        if (!playerMoveState.lockTarget ||
            Vector3.Distance(transform.position, playerMoveState.lockTarget.position) > lockMoveConfig.lockSearchRadius)
            ClearLockTarget();
    }

    private void BindActions()
    {
        playerInput ??= GetComponent<PlayerInput>();
        if (playerInput?.actions == null) return;

        moveAction = GetAction(inputNames.moveActionName);
        sprintAction = GetAction(inputNames.sprintActionName);
        runAction = GetAction(inputNames.runActionName);
        lockOnAction = GetAction(inputNames.lockOnActionName);
        crouchAction = GetAction(inputNames.crouchActionName);
        attackAction = GetAction(inputNames.attackActionName);
        rollAction = GetAction(inputNames.rollActionName);
    }

    private InputAction GetAction(string actionName) => string.IsNullOrEmpty(actionName) ? null : playerInput.actions[actionName];

    private void RegisterCallbacks(bool register)
    {
        Register(runAction, OnRunPerformed, register);
        Register(lockOnAction, OnLockOnPerformed, register);
        Register(crouchAction, OnCrouchPerformed, register);
        Register(attackAction, OnAttackPerformed, register);
        Register(sprintAction, OnSprintPerformed, register);
        Register(rollAction, OnRollPerformed, register);
    }

    private void Register(InputAction action, System.Action<InputAction.CallbackContext> callback, bool register)
    {
        if (action == null) return;
        if (register) action.performed += callback;
        else action.performed -= callback;
    }

    private void ReadInput()
    {
        if (!CanMove()) { ClearMoveInputOnly(); return; }

        // 移动动作中不读取输入，防止冲突
        if (IsMoveActionLocked()) return;

        playerMoveState.moveInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        if (playerMoveState.moveInput.sqrMagnitude < 0.0001f) playerMoveState.moveInput = Vector2.zero;
        playerMoveState.hasMove = playerMoveState.moveInput.sqrMagnitude > 0.01f;

        if (combat && combat.combatState.isCrouching) actorState.isRunning = false;
        else actorState.isRunning = playerMoveState.hasMove && playerMoveState.runToggled;
    }

    private void ClearMoveInputOnly()
    {
        playerMoveState.moveInput = Vector2.zero;
        playerMoveState.hasMove = false;
        actorState.isRunning = false;
    }

    private void StopRun()
    {
        playerMoveState.runToggled = false;
        actorState.isRunning = false;
    }

    private void CheckGrounded()
    {
        if (playerRefs.groundCheck)
            actorState.isGrounded = Physics.CheckSphere(playerRefs.groundCheck.position, groundConfig.groundCheckRadius, groundConfig.groundLayers, QueryTriggerInteraction.Ignore);
        else if (CharacterController)
            actorState.isGrounded = CharacterController.isGrounded;

        if (actorState.isGrounded && verticalVelocity.y < 0) verticalVelocity.y = moveConfig.groundedForce;
    }

    private void HandleFreeMove()
    {
        if (!CharacterController || !playerRefs.lookRoot) return;
        Vector3 moveDir = GetCameraRelativeMove();
        float speed = actorState.isRunning ? playerMoveConfig.sprintSpeed : moveConfig.walkSpeed;

        RotateToMoveDirection(moveDir);
        CharacterController.Move(moveDir * speed * Time.deltaTime);
    }

    private void HandleLockMove()
    {
        if (!CharacterController) return;
        FaceLockTarget();
        Vector3 moveDir = GetCameraRelativeMove();
        float speed = actorState.isRunning ? lockMoveConfig.lockRunSpeed : lockMoveConfig.lockWalkSpeed;
        CharacterController.Move(moveDir * speed * Time.deltaTime);
    }

    private void RotateToMoveDirection(Vector3 moveDirection)
    {
        if (!playerMoveState.hasMove || moveDirection.sqrMagnitude <= 0.0001f) return;

        targetRotation = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
        float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity, playerMoveConfig.rotationSmoothTime);
        transform.rotation = Quaternion.Euler(0, rotation, 0);
    }

    private Vector3 GetCameraRelativeMove()
    {
        if (!playerRefs.lookRoot) return Vector2.zero;
        Vector3 forward = playerRefs.lookRoot.forward, right = playerRefs.lookRoot.right;
        forward.y = right.y = 0;
        forward.Normalize(); right.Normalize();

        Vector3 dir = forward * playerMoveState.moveInput.y + right * playerMoveState.moveInput.x;
        if (dir.sqrMagnitude > 1) dir.Normalize();
        return dir;
    }

    private void FaceLockTarget()
    {
        if (!playerMoveState.lockTarget) return;
        Vector3 dir = playerMoveState.lockTarget.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), lockMoveConfig.lockRotateSpeed * Time.deltaTime);
    }

    private void ApplyGravity()
    {
        if (!CharacterController) return;
        verticalVelocity.y += moveConfig.gravity * Time.deltaTime;
        CharacterController.Move(verticalVelocity * Time.deltaTime);
    }

    private Transform FindBestLockTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, lockMoveConfig.lockSearchRadius, lockMoveConfig.targetLayers, QueryTriggerInteraction.Ignore);
        Transform best = null;
        float bestScore = float.MaxValue;
        Vector3 forward = playerRefs.lookRoot ? playerRefs.lookRoot.forward : transform.forward;
        forward.y = 0; forward.Normalize();

        foreach (Collider hit in hits)
        {
            Vector3 to = hit.transform.position - transform.position;
            to.y = 0;
            float distance = to.magnitude;
            if (distance < 0.01f) continue;

            float score = Vector3.Angle(forward, to.normalized) * 2 + distance;
            if (score < bestScore) { bestScore = score; best = hit.transform; }
        }
        return best;
    }

    public void SetLockTarget(Transform target)
    {
        playerMoveState.lockTarget = target;
        playerMoveState.isLockedOn = target != null;
    }

    public void ClearLockTarget()
    {
        playerMoveState.lockTarget = null;
        playerMoveState.isLockedOn = false;
    }

    private void SyncCameraLockState()
    {
        if (!playerRefs.playerCamera) return;
        playerRefs.playerCamera.isLockOn = playerMoveState.isLockedOn;
        playerRefs.playerCamera.lockOnTarget = playerMoveState.lockTarget;
    }

    public bool IsRolling() => moveActionState.isRolling;
    public bool IsSprinting() => moveActionState.isSprinting;
    public bool IsMoveActionLocked() => IsRolling() || IsSprinting();

    // 公开方法：判断角色是否处于无敌状态
    public bool IsInvincible()
    {
        return Time.time < moveActionState.invincibleEndTime;
    }

    [Header("躲避时间减缓设置")]
    public float dodgeSlowMotionDuration = 0.5f;
    public float dodgeSlowMotionTimeScale = 0.3f;
    
    private Coroutine dodgeSlowMotionCoroutine;
    private bool isDodgeSlowMotionActive;

    public void TriggerDodgeSlowMotion()
    {
        if (isDodgeSlowMotionActive) return;
        if (dodgeSlowMotionCoroutine != null) StopCoroutine(dodgeSlowMotionCoroutine);
        dodgeSlowMotionCoroutine = StartCoroutine(DodgeSlowMotionCoroutine());
    }

    private System.Collections.IEnumerator DodgeSlowMotionCoroutine()
    {
        isDodgeSlowMotionActive = true;
        Time.timeScale = dodgeSlowMotionTimeScale;
        yield return new WaitForSecondsRealtime(dodgeSlowMotionDuration);
        Time.timeScale = 1f;
        isDodgeSlowMotionActive = false;
        dodgeSlowMotionCoroutine = null;
    }

    private bool CanStartMoveAction() => !actorState.isHit && !IsMoveActionLocked() && (!moveActionConfig.requireGrounded || actorState.isGrounded) && !(skillPlayer != null && skillPlayer.IsSkillPlaying());

    // 开始移动动作保存原始状态
    private void StartMoveAction(string triggerName)
    {
        // 翻滚/冲刺保存状态
        preActionState.savedMoveInput = playerMoveState.moveInput;
        preActionState.savedHasMove = playerMoveState.hasMove;
        preActionState.savedRunToggled = playerMoveState.runToggled;

        // 触发动画
        if (Animator) { Animator.ResetTrigger(triggerName); Animator.SetTrigger(triggerName); }
    }

    private void TryRoll()
    {
        if (!CanStartMoveAction()) return;
        if (stamina != null && !stamina.TryRoll()) return;
        CancelCurrentAttack();
        moveActionState.isRolling = true;
        moveActionState.rollStartTime = Time.time;
        moveActionState.invincibleEndTime = Time.time + 10f;
        StartMoveAction(moveActionConfig.rollTriggerName);
    }

    private void TrySprint()
    {
        if (!CanStartMoveAction()) return;
        if (stamina != null && !stamina.TrySprint()) return;
        CancelCurrentAttack();
        moveActionState.isSprinting = true;
        moveActionState.sprintStartTime = Time.time;
        moveActionState.invincibleEndTime = Time.time + 10f;
        StartMoveAction(moveActionConfig.sprintTriggerName);
    }

    private void CancelCurrentAttack()
    {
        combat?.CancelAttack();
    }

    private void TickMoveActions()
    {
        TickMoveAction(ref moveActionState.isRolling, moveActionState.rollStartTime, moveActionConfig.rollGraceTime, moveActionConfig.rollStateTag);
        TickMoveAction(ref moveActionState.isSprinting, moveActionState.sprintStartTime, moveActionConfig.sprintGraceTime, moveActionConfig.sprintStateTag);
    }

    // 检测动作结束状态 + 延长无敌结束时间
    private void TickMoveAction(ref bool active, float startTime, float graceTime, string stateTag)
    {
        if (!active) return;
        if (!Animator)
        {
            active = false;
            RestorePreActionState();
            moveActionState.invincibleEndTime = 0;
            return;
        }

        // 动画未结束保持无敌
        if (Time.time - startTime < graceTime || IsInAnimatorTag(stateTag))
        {
            moveActionState.invincibleEndTime = Time.time + moveActionConfig.invincibleDelayAfterAction;
            return;
        }

        // 动画结束清除状态 + 重置状态 + 保留额外无敌时间
        active = false;
        RestorePreActionState();
        moveActionState.invincibleEndTime = Time.time + moveActionConfig.invincibleDelayAfterAction;
        
        // 如果是翻滚动作，通知战斗控制器设置翻滚结束时间
        if (stateTag == moveActionConfig.rollStateTag)
        {
            combat?.SetRollEndTime(Time.time);
        }
    }

    // 重置翻滚/冲刺移动动作战斗状态
    private void RestorePreActionState()
    {
        playerMoveState.moveInput = preActionState.savedMoveInput;
        playerMoveState.hasMove = preActionState.savedHasMove;
        playerMoveState.runToggled = preActionState.savedRunToggled;

        // 同步奔跑状态
        if (combat && combat.combatState.isCrouching)
            actorState.isRunning = false;
        else
            actorState.isRunning = playerMoveState.hasMove && playerMoveState.runToggled;
    }

    private bool IsInAnimatorTag(string tagName)
    {
        if (!Animator || string.IsNullOrEmpty(tagName)) return false;
        AnimatorStateInfo current = Animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsTag(tagName)) return true;
        return Animator.IsInTransition(0) && Animator.GetNextAnimatorStateInfo(0).IsTag(tagName);
    }

    private void ApplyMoveActionAnimationMove()
    {
        if (!moveActionConfig.useAnimationMove || !Animator || !CharacterController) return;
        float value = Animator.GetFloat(moveActionConfig.animationMoveParam);
        if (Mathf.Abs(value) <= moveActionConfig.threshold) return;
        CharacterController.Move(transform.forward * value * moveActionConfig.animationMoveSpeed * Time.deltaTime);
    }

    private void UpdateAnimatorState()
    {
        if (!Animator) return;
        bool useLockMove = ShouldUseLockMove();

        Animator.SetFloat("LockOn", useLockMove ? 1 : 0);
        Animator.SetFloat("Crouch", combat && combat.combatState.isCrouching ? 1 : 0);

        if (ShouldClearMoveAnimator()) { ClearMoveAnimator(); return; }
        Animator.SetFloat("Run", actorState.isRunning ? 1 : 0);
        if (useLockMove) SetLockMoveAnimator(); else SetFreeMoveAnimator();
    }

    private bool ShouldClearMoveAnimator() => actorState.isHit || IsMoveActionLocked() || (combat && combat.IsAttackMoveLocked());

    private void ClearMoveAnimator()
    {
        SetFloatDamp("Movement", 0);
        SetFloatDamp("Horizontal", 0);
        SetFloatDamp("Vertical", 0);
        SetFloatDamp("Run", 0);
    }

    private void SetLockMoveAnimator()
    {
        SetFloatDamp("Movement", 0);
        SetFloatDamp("Horizontal", playerMoveState.moveInput.x);
        SetFloatDamp("Vertical", playerMoveState.moveInput.y);
    }

    private void SetFreeMoveAnimator()
    {
        SetFloatDamp("Horizontal", 0); SetFloatDamp("Vertical", 0);
        SetFloatDamp("Movement", playerMoveState.hasMove ? (actorState.isRunning ? 1.5f : 1f) : 0);
    }

    private void SetFloatDamp(string name, float value) => Animator.SetFloat(name, value, 0.08f, Time.deltaTime);

    private void OnRunPerformed(InputAction.CallbackContext context)
    {
        if (!CanToggleRun()) return;
        playerMoveState.runToggled = !playerMoveState.runToggled;
    }

    private void OnSprintPerformed(InputAction.CallbackContext context) => TrySprint();
    private void OnRollPerformed(InputAction.CallbackContext context) => TryRoll();

    private void OnLockOnPerformed(InputAction.CallbackContext context)
    {
        if (!CanToggleLockOn()) return;

        if (playerMoveState.isLockedOn) { ClearLockTarget(); return; }

        Transform target = FindBestLockTarget();
        if (target) SetLockTarget(target);
    }

    private void OnCrouchPerformed(InputAction.CallbackContext context)
    {
        if (!combat || IsMoveActionLocked()) return;
        combat.ToggleCrouch();
        if (combat.combatState.isCrouching) StopRun();
    }

    private void OnAttackPerformed(InputAction.CallbackContext context) => combat?.TryAttackWithComboWindow();

    private bool CanToggleRun() => !actorState.isHit && !IsMoveActionLocked() && !(combat && (combat.IsAttackMoveLocked() || combat.combatState.isCrouching));
    private bool CanToggleLockOn() => CanMove() && !IsMoveActionLocked();

    private void OnDrawGizmosSelected()
    {
        if (playerRefs.groundCheck) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(playerRefs.groundCheck.position, groundConfig.groundCheckRadius); }
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, lockMoveConfig.lockSearchRadius);
    }
}
