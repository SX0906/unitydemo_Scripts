using UnityEngine;
using GameInput;

public class TestFSM : MonoBehaviour
{
    private Animator animator;
    private PlayerControl playerControl;
    private FSMControl fsm;
    private CharacterController controller;
    private WeaponHitDetector weaponHitDetector;
    private bool _isRunning;
    private bool _isLockOn;
    public int currentComboSet { get; set; } = 1;
    public bool JumpSoftEnter { get; set; }
    public float AirAttackEnterY { get; set; }

    // === 公开属性（供 State 类访问） ===
    public bool IsLockOn => _isLockOn;
    public bool IsRunning => _isRunning;
    public float CurrentMoveSpeedMultiplier { get; set; } = 1f;
    public bool IsJumping => fsm != null && fsm.stateType == StateType.JUMP;
    public Transform LockOnTarget => _lockOnTarget;
    public float VerticalVelocity { get; set; }

    [Header("相机控制")]
    public PlayerCameraController cameraController;
    public Transform lookRoot;
    public Transform _lockOnTarget;
    public LayerMask targetLayers;
    public float lockOnSearchRadius = 10f;
    public float lockOnActivationRange = 6f;
    public float lockOnMaxRange = 10f;

    [Header("攻击吸附")]
    public float attackSnapDistance = 2.5f;
    [Range(0f, 180f)] public float attackSnapAngle = 100f;
    public float attackSnapRotateSpeed = 720f;

    [Header("地面检测")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundCheckDistance = 0.15f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerControl = new PlayerControl();
        controller = GetComponent<CharacterController>();
        weaponHitDetector = GetComponentInChildren<WeaponHitDetector>();

        fsm = new FSMControl();
        fsm.AddState(StateType.IDlE, new IdleState(animator, fsm));
        fsm.AddState(StateType.MOVE, new MoveState(animator, playerControl, fsm, this));
        Collider weaponCol = GetComponentInChildren<WeaponHitDetector>()?.GetComponent<Collider>();
        fsm.AddState(StateType.ATTACK_01, new AttackState(animator, playerControl, fsm, this, "LAtk-1", weaponCol,
            attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.ATTACK_02, new AttackState(animator, playerControl, fsm, this, "LAtk-2", weaponCol,
            attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.LockOn, new LockOnState(animator, fsm, this));
        fsm.AddState(StateType.JUMP, new JumpState(animator, playerControl, fsm, this, controller));
        fsm.AddState(StateType.ATTACK_UP, new AttackUpState(animator, playerControl, fsm, this, controller, weaponCol,
            attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.AIR_ATTACK, new AirAttackState(animator, playerControl, fsm, this, controller, weaponCol,
            attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.DODGE, new DodgeState(animator, playerControl, fsm, this));
        fsm.SetState(StateType.IDlE);
    }

    private void OnEnable()
    {
        playerControl.Player.Enable();
    }

    private void OnDisable()
    {
        playerControl.Player.Disable();
    }

    private void Update()
    {
        Vector2 moveInput = playerControl.Player.Move.ReadValue<Vector2>();

        CheckLockOnRange();

        if (playerControl.Player.ComboSet1.WasPressedThisFrame())
        {
            currentComboSet = 1;
            Debug.Log("切换到连招1");
        }
        if (playerControl.Player.ComboSet2.WasPressedThisFrame())
        {
            currentComboSet = 2;
            Debug.Log("切换到连招2");
        }
        if (fsm.stateType != StateType.JUMP && fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIR_ATTACK)
        {
            if (playerControl.Player.Attack.WasPressedThisFrame())
            {
                if (fsm.stateType != StateType.ATTACK_01 && fsm.stateType != StateType.ATTACK_02)
                {
                    switch (currentComboSet)
                    {
                        case 1:
                            fsm.SetState(StateType.ATTACK_01);
                            break;
                        case 2:
                            fsm.SetState(StateType.ATTACK_02);
                            break;
                        default:
                            fsm.SetState(StateType.ATTACK_01);
                            break;
                    }
                }
            }
        }
        if (playerControl.Player.Attack.WasPressedThisFrame()
            && (fsm.stateType == StateType.JUMP || fsm.stateType == StateType.ATTACK_UP))
        {
            fsm.SetState(StateType.AIR_ATTACK);
        }
        // 闪避（体力不足时无法使用）
        if (playerControl.Player.Dodge.WasPressedThisFrame())
        {
            if (fsm.stateType != StateType.ATTACK_01 && fsm.stateType != StateType.ATTACK_02
                && fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIR_ATTACK)
            {
                fsm.SetState(StateType.DODGE);
            }
        }
        if (playerControl.Player.Run.WasPressedThisFrame())
            _isRunning = !_isRunning;
        animator.SetFloat("Run", _isRunning ? 1f : 0f);
        if (playerControl.Player.LockOn.WasPressedThisFrame())
        {
            if (_isLockOn)
            {
                _isLockOn = false;
                _lockOnTarget = null;
                animator.SetFloat("LockOn", 0f);
                if (cameraController != null)
                {
                    cameraController.isLockOn = false;
                    cameraController.lockOnTarget = null;
                }
                if (fsm.stateType == StateType.LockOn)
                    fsm.SetState(StateType.IDlE);
            }
            else
            {
                Transform target = FindNearestEnemy();
                if (target != null)
                {
                    float dist = Vector3.Distance(transform.position, target.position);
                    if (dist > lockOnActivationRange)
                    {
                        Debug.Log($"锁定失败：目标距离 {dist:F1}m，超过锁定范围 {lockOnActivationRange}m");
                    }
                    else
                    {
                        _isLockOn = true;
                        _lockOnTarget = target;
                        animator.SetFloat("LockOn", 1f);
                        if (cameraController != null)
                        {
                            cameraController.isLockOn = true;
                            cameraController.lockOnTarget = target;
                        }
                        fsm.SetState(StateType.LockOn);
                    }
                }
            }
        }
        if (playerControl.Player.Jump.WasPressedThisFrame())
        {
            if (fsm.stateType == StateType.IDlE ||
                fsm.stateType == StateType.MOVE ||
                fsm.stateType == StateType.LockOn)
            {
                if (IsGrounded)
                    fsm.SetState(StateType.JUMP);
            }
        }
        // 升龙（体力不足时无法使用）
        if (playerControl.Player.RAtk.WasPressedThisFrame())
        {
            if (fsm.stateType != StateType.ATTACK_UP)
            {
                fsm.SetState(StateType.ATTACK_UP);
            }
        }
        if (fsm.stateType != StateType.ATTACK_01 && fsm.stateType != StateType.ATTACK_02
            && fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIR_ATTACK
            && fsm.stateType != StateType.DODGE)
        {
            if (moveInput == Vector2.zero)
            {
                if (fsm.stateType == StateType.MOVE)
                    fsm.SetState(StateType.IDlE);
            }
            else
            {
                if (fsm.stateType != StateType.MOVE)
                    fsm.SetState(StateType.MOVE);
            }
        }

        fsm.OnTick();
    }

    private void CheckLockOnRange()
    {
        if (!_isLockOn || _lockOnTarget == null) return;

        float dist = Vector3.Distance(transform.position, _lockOnTarget.position);
        if (dist > lockOnMaxRange)
        {
            Debug.Log($"自动退出锁定：目标距离 {dist:F1}m，超过最大范围 {lockOnMaxRange}m");
            _isLockOn = false;
            _lockOnTarget = null;
            animator.SetFloat("LockOn", 0f);
            if (cameraController != null)
            {
                cameraController.isLockOn = false;
                cameraController.lockOnTarget = null;
            }
            if (fsm.stateType == StateType.LockOn)
                fsm.SetState(StateType.IDlE);
        }
    }

    private Transform FindNearestEnemy()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, lockOnSearchRadius, targetLayers);
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (var col in cols)
        {
            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = col.transform;
            }
        }
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

    public Vector2 GetMoveInput()
    {
        return playerControl.Player.Move.ReadValue<Vector2>();
    }

    // === MoveState 使用的公开方法 ===

    public void ClearMoveAnimation()
    {
        if (animator != null)
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("speed", 0f);
        }
    }

    public void ApplyLockOnMove(Vector2 input)
    {
        if (animator != null)
        {
            animator.SetFloat("MoveX", input.x);
            animator.SetFloat("MoveY", input.y);
            float speed = _isRunning ? 2f : 1f;
            animator.SetFloat("speed", speed);
        }
    }

    public void ApplyFreeMove(Vector2 input)
    {
        if (animator != null)
        {
            animator.SetFloat("MoveX", input.x);
            animator.SetFloat("MoveY", input.y);
            float speed = _isRunning ? 2f : 1f;
            animator.SetFloat("speed", speed);
        }
    }

    // === JumpEndBehaviour 使用的公开方法 ===

    public void OnJumpLandingFinished()
    {
        fsm?.SetState(_isLockOn ? StateType.LockOn : StateType.IDlE);
    }

    // === AnimationEvent 转发（调用方为根物体 Animator） ===

    public void OnHitWindowOpen(string dirTag)
    {
        weaponHitDetector?.OnHitWindowOpen(dirTag);
    }

    public void OnHitWindowClose()
    {
        weaponHitDetector?.OnHitWindowClose();
    }
}