using UnityEngine;
using GameInput;
using UnityEngine.Playables;

public partial class TestFSM_test : MonoBehaviour, ICombatTarget, ICombatant
{
    private Animator animator;
    private PlayerControl playerControl;
    private FSMControl fsm;
    private CharacterController controller;
    private WeaponHitDetector_test weaponHitDetector;
    private PlayerVitals playerVitals;
    private bool _isRunning;
    private bool _isLockOn;
    private bool isChargingAirToFloor;
    private float airToFloorChargeTime;
    public int currentComboSet { get; set; } = 1;
    public bool JumpSoftEnter { get; set; }
    public float AirAttackEnterY { get; set; }
    public bool backAttackAvailable;
    public float backAttackTimer;
    public float backAttackDuration = 5f;
    public bool IsLockOn => _isLockOn;
    public bool IsRunning => _isRunning;
    public float CurrentMoveSpeedMultiplier { get; set; } = 1f;
    public bool IsJumping => fsm != null && fsm.stateType == StateType.JUMP;
    public Transform LockOnTarget => _lockOnTarget;
    public float VerticalVelocity { get; set; }

    [Header("相机控制")]
    public FSMCamera cameraController;
    public Transform lookRoot;
    public Transform _lockOnTarget;

    [Header("Timeline")]
    public PlayableDirector powerDirector;
    public LayerMask targetLayers;
    public float lockOnSearchRadius = 10f;
    public float lockOnActivationRange = 6f;
    public float lockOnMaxRange = 10f;

    [Header("攻击吸附")]
    public float attackSnapDistance = 2.5f;
    [Range(0f, 180f)] public float attackSnapAngle = 100f;
    public float attackSnapRotateSpeed = 720f;

    [Header("体力消耗")]
    public float dodgeStaminaCost = 15f;
    public float attackUpStaminaCost = 20f;
    public float jumpStaminaCost = 10f;
    public float airAttackStaminaCost = 3f;

    [Header("空中蓄力下砸")]
    public float airToFloorChargeDuration = 1f;
    public float airToFloorStaminaCost = 18f;

    [Header("地面检测")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundCheckDistance = 0.15f;

    [Header("顿帧效果")]
    public int hitStopFrameCount = 8;
    public float dodgeSlowTimeScale = 0.5f;
    public float dodgeSlowDuration = 0.2f;

    [Header("Power防御")]
    [Range(0f, 360f)] public float powerBlockAngle = 160f;

    // ===== ICombatTarget 实现 =====
    Transform ICombatTarget.Transform => transform;
    bool ICombatTarget.IsAlive => playerVitals != null && !playerVitals.IsDead;
    bool ICombatTarget.TakeHit(HitContext hit)
    {
        if (playerVitals == null || playerVitals.IsDead) return false;
        if (playerVitals.isInvincible && fsm.stateType == StateType.DODGE)
        {
            HitStopManager_test.Request(dodgeSlowTimeScale, dodgeSlowDuration);
            backAttackAvailable = true;
            backAttackTimer = backAttackDuration;
            return false;
        }
        if (fsm.stateType == StateType.POWER) return false;
        if (fsm.stateType == StateType.BACKATTACK)
        {
            playerVitals.TakeDamage(hit.Damage);
            if (playerVitals.IsDead) fsm.SetState(StateType.DEATH);
            return true;
        }
        playerVitals.TakeDamage(hit.Damage);
        if (playerVitals.IsDead) { fsm.SetState(StateType.DEATH); return true; }
        HitStopManager_test.FreezeAnimator(animator, hitStopFrameCount);
        if (hit.Attacker != null)
        {
            Animator a = hit.Attacker.GetComponentInChildren<Animator>();
            if (a != null) HitStopManager_test.FreezeAnimator(a, hitStopFrameCount);
        }
        var hitState = fsm.GetState<HitState>(StateType.HIT);
        if (hitState != null)
        {
            if (fsm.stateType == StateType.HIT) hitState.Rehit(hit.Attacker);
            else { hitState.SetHitInfo(hit.Attacker); fsm.SetState(StateType.HIT); }
        }
        return true;
    }

    // ===== ICombatant 实现 =====
    Transform ICombatant.Transform => transform;
    ActorVitals ICombatant.Vitals => playerVitals;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerControl = new PlayerControl();
        controller = GetComponent<CharacterController>();
        weaponHitDetector = GetComponentInChildren<WeaponHitDetector_test>();
        playerVitals = GetComponent<PlayerVitals>();
        fsm = new FSMControl();
        HitStopManager_test.EnsureHost(this);

        fsm.AddState(StateType.IDlE, new IdleState(animator, fsm));
        fsm.AddState(StateType.MOVE, new MoveState(animator, playerControl, fsm, this));
        Collider weaponCol = GetComponentInChildren<WeaponHitDetector_test>()?.GetComponent<Collider>();
        fsm.AddState(StateType.ATTACK_01, new AttackState(animator, playerControl, fsm, this, "LAtk-1", weaponCol, attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.ATTACK_02, new AttackState(animator, playerControl, fsm, this, "LAtk-2", weaponCol, attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.LockOn, new LockOnState(animator, fsm, this));
        fsm.AddState(StateType.JUMP, new JumpState(animator, playerControl, fsm, this, controller));
        fsm.AddState(StateType.ATTACK_UP, new AttackUpState(animator, playerControl, fsm, this, controller, weaponCol, attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.AIR_ATTACK, new AirAttackState(animator, playerControl, fsm, this, controller, weaponCol, attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.DODGE, new DodgeState(animator, playerControl, fsm, this, playerVitals));
        fsm.AddState(StateType.HIT, new HitState(fsm, this));
        fsm.AddState(StateType.AIRTOFLOORATTACK, new AirtoFloopAttackState(animator, playerControl, fsm, this, controller));
        fsm.AddState(StateType.POWER, new PowerState_test(animator, playerControl, fsm, playerVitals, powerDirector, targetLayers, transform, (enabled) => { if (cameraController) cameraController.enabled = enabled; }, () => IsLockOn, this));
        fsm.AddState(StateType.BACKATTACK, new BackAttackState(animator, playerControl, fsm, this, weaponCol, attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.DEATH, new DeathState(animator, fsm, this));
        fsm.SetState(StateType.IDlE);

        // 绑定 UI
        var ui = GetComponentInChildren<PlayerVitalsUI_test>();
        if (ui != null) ui.Bind(playerVitals);
    }

    private void OnEnable() { playerControl.Player.Enable(); }
    private void OnDisable() { playerControl.Player.Disable(); }

    private void Update()
    {
        if (fsm.stateType == StateType.HIT || fsm.stateType == StateType.DEATH) { fsm.OnTick(); return; }
        if (fsm.stateType == StateType.POWER) { fsm.OnTick(); return; }

        Vector2 moveInput = playerControl.Player.Move.ReadValue<Vector2>();
        if (backAttackAvailable) { backAttackTimer -= Time.deltaTime; if (backAttackTimer <= 0f) { backAttackAvailable = false; backAttackTimer = 0f; } }
        CheckLockOnRange();
        if (playerControl.Player.ComboSet1.WasPressedThisFrame()) currentComboSet = 1;
        if (playerControl.Player.ComboSet2.WasPressedThisFrame()) currentComboSet = 2;

        if (playerControl.Player.Power.WasPressedThisFrame())
        {
            if (playerVitals != null && playerVitals.currentRage >= playerVitals.maxRage)
            {
                if (fsm.stateType != StateType.ATTACK_01 && fsm.stateType != StateType.ATTACK_02 && fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIR_ATTACK && fsm.stateType != StateType.AIRTOFLOORATTACK && fsm.stateType != StateType.JUMP)
                { fsm.SetState(StateType.POWER); return; }
            }
        }

        if (fsm.stateType != StateType.JUMP && fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIR_ATTACK && fsm.stateType != StateType.AIRTOFLOORATTACK && fsm.stateType != StateType.BACKATTACK)
        {
            if (playerControl.Player.Attack.WasPressedThisFrame())
            {
                if (backAttackAvailable && backAttackTimer > 0f) { fsm.SetState(StateType.BACKATTACK); return; }
                if (fsm.stateType != StateType.ATTACK_01 && fsm.stateType != StateType.ATTACK_02 && fsm.stateType != StateType.BACKATTACK)
                {
                    switch (currentComboSet) { case 1: fsm.SetState(StateType.ATTACK_01); break; case 2: fsm.SetState(StateType.ATTACK_02); break; default: fsm.SetState(StateType.ATTACK_01); break; }
                }
            }
        }

        if (!IsGrounded || fsm.stateType == StateType.JUMP || fsm.stateType == StateType.ATTACK_UP || fsm.stateType == StateType.AIR_ATTACK)
        {
            if (playerControl.Player.Attack.WasPressedThisFrame())
            {
                if (backAttackAvailable && backAttackTimer > 0f) { fsm.SetState(StateType.BACKATTACK); return; }
                isChargingAirToFloor = true; airToFloorChargeTime = 0f;
            }
            if (isChargingAirToFloor)
            {
                if (playerControl.Player.Attack.IsPressed())
                {
                    airToFloorChargeTime += Time.deltaTime;
                    if (airToFloorChargeTime >= airToFloorChargeDuration)
                    {
                        isChargingAirToFloor = false;
                        if (playerVitals == null || playerVitals.UseStamina(airToFloorStaminaCost)) fsm.SetState(StateType.AIRTOFLOORATTACK);
                    }
                }
                else
                {
                    isChargingAirToFloor = false;
                    if (playerVitals == null || playerVitals.UseStamina(airAttackStaminaCost)) fsm.SetState(StateType.AIR_ATTACK);
                    else { JumpSoftEnter = true; fsm.SetState(StateType.JUMP); }
                }
            }
        }
        else { isChargingAirToFloor = false; }

        if (playerControl.Player.Dodge.WasPressedThisFrame())
        {
            if (fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIR_ATTACK && fsm.stateType != StateType.AIRTOFLOORATTACK)
            { if (playerVitals == null || playerVitals.UseStamina(dodgeStaminaCost)) fsm.SetState(StateType.DODGE); }
        }

        if (playerControl.Player.Run.WasPressedThisFrame()) _isRunning = !_isRunning;
        animator.SetFloat("Run", _isRunning ? 1f : 0f);

        if (playerControl.Player.LockOn.WasPressedThisFrame())
        {
            if (_isLockOn)
            {
                _isLockOn = false; _lockOnTarget = null; animator.SetFloat("LockOn", 0f);
                if (cameraController != null) { cameraController.isLockOn = false; cameraController.lockOnTarget = null; }
                if (fsm.stateType == StateType.LockOn) fsm.SetState(StateType.IDlE);
            }
            else
            {
                Transform target = FindNearestEnemy();
                if (target != null)
                {
                    float dist = Vector3.Distance(transform.position, target.position);
                    if (dist > lockOnActivationRange) { /* 超出范围 */ }
                    else
                    {
                        _isLockOn = true; _lockOnTarget = target; animator.SetFloat("LockOn", 1f);
                        if (cameraController != null) { cameraController.isLockOn = true; cameraController.lockOnTarget = target; }
                        fsm.SetState(StateType.LockOn);
                    }
                }
            }
        }

        if (playerControl.Player.Jump.WasPressedThisFrame())
        {
            if ((fsm.stateType == StateType.IDlE || fsm.stateType == StateType.MOVE || fsm.stateType == StateType.LockOn || fsm.stateType == StateType.ATTACK_01 || fsm.stateType == StateType.ATTACK_02) && IsGrounded)
            { if (playerVitals == null || playerVitals.UseStamina(jumpStaminaCost)) fsm.SetState(StateType.JUMP); }
        }

        if (playerControl.Player.RAtk.WasPressedThisFrame())
        {
            if (fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIRTOFLOORATTACK)
            { if (playerVitals == null || playerVitals.UseStamina(attackUpStaminaCost)) fsm.SetState(StateType.ATTACK_UP); }
        }

        if (fsm.stateType != StateType.ATTACK_01 && fsm.stateType != StateType.ATTACK_02 && fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIR_ATTACK && fsm.stateType != StateType.DODGE && fsm.stateType != StateType.JUMP && fsm.stateType != StateType.AIRTOFLOORATTACK && fsm.stateType != StateType.BACKATTACK)
        {
            if (moveInput == Vector2.zero) { if (fsm.stateType == StateType.MOVE) fsm.SetState(StateType.IDlE); }
            else { if (fsm.stateType != StateType.MOVE) fsm.SetState(StateType.MOVE); }
        }
        fsm.OnTick();
    }

    private void CheckLockOnRange()
    {
        if (!_isLockOn || _lockOnTarget == null) return;
        float dist = Vector3.Distance(transform.position, _lockOnTarget.position);
        if (dist > lockOnMaxRange)
        {
            _isLockOn = false; _lockOnTarget = null; animator.SetFloat("LockOn", 0f);
            if (cameraController != null) { cameraController.isLockOn = false; cameraController.lockOnTarget = null; }
            if (fsm.stateType == StateType.LockOn) fsm.SetState(StateType.IDlE);
        }
    }

    private Transform FindNearestEnemy()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, lockOnSearchRadius, targetLayers);
        Transform best = null; float bestDist = float.MaxValue;
        foreach (var col in cols) { float d = Vector3.Distance(transform.position, col.transform.position); if (d < bestDist) { bestDist = d; best = col.transform; } }
        return best;
    }

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

    public Vector2 GetMoveInput() => playerControl.Player.Move.ReadValue<Vector2>();
    public void ClearMoveAnimation() { if (animator != null) { animator.SetFloat("MoveX", 0f); animator.SetFloat("MoveY", 0f); animator.SetFloat("speed", 0f); } }
    public void ApplyLockOnMove(Vector2 input) { if (animator != null) { animator.SetFloat("MoveX", input.x); animator.SetFloat("MoveY", input.y); animator.SetFloat("speed", _isRunning ? 2f : 1f); } }
    public void ApplyFreeMove(Vector2 input) { if (animator != null) { animator.SetFloat("MoveX", input.x); animator.SetFloat("MoveY", input.y); animator.SetFloat("speed", _isRunning ? 2f : 1f); } }
    public void OnJumpLandingFinished() { fsm?.SetState(_isLockOn ? StateType.LockOn : StateType.IDlE); }
    public void OnHitWindowOpen(string dirTag) { weaponHitDetector?.OnHitWindowOpen(dirTag); }
    public void OnHitWindowClose() { weaponHitDetector?.OnHitWindowClose(); }

    public void TakeDamage(float damage, Transform attacker)
    {
        ICombatTarget t = this;
        t.TakeHit(new HitContext("F", Vector3.zero, false, attacker, damage, false));
    }

    public void DealPowerDamage()
    {
        Transform t = transform;
        Vector3 center = t.position + t.forward * 1.8f + Vector3.up * 0.5f;
        Collider[] hits = Physics.OverlapSphere(center, 1.8f, targetLayers);
        foreach (Collider col in hits)
        {
            ICombatTarget target = col.GetComponentInParent<ICombatTarget>();
            if (target == null) continue;
            Vector3 dir = (target.Transform.position - t.position).normalized;
            target.TakeHit(new HitContext("F", dir, false, t, 25f, true));
        }
    }

    public void DealPowerDamageFinal()
    {
        Transform t = transform;
        Vector3 center = t.position + t.forward * 1.8f + Vector3.up * 0.5f;
        Collider[] hits = Physics.OverlapSphere(center, 1.8f, targetLayers);
        foreach (Collider col in hits)
        {
            ICombatTarget target = col.GetComponentInParent<ICombatTarget>();
            if (target == null) continue;
            Vector3 dir = (target.Transform.position - t.position).normalized;
            target.TakeHit(new HitContext("F", dir, false, t, 50f, true));
        }
    }
}
