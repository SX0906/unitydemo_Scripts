using UnityEngine;
using GameInput;

public class AirAttackState : StateBase
{
    private Animator animator;
    private PlayerControl playerControl;
    private FSMControl fsm;
    private TestFSM testfsm;
    private CharacterController controller;
    private Collider weaponCollider;
    private const string AirAttackTrigger = "AirAttack";
    private const string AirAttackTag = "AirAttack";
    private bool currentAnimStarted;

    // 连击预输入
    private bool hasBufferedNextAttack;
    private bool comboWindowOpen;
    private float bufferedTime;
    private int currentAttackStateHash;
    private const float ComboBufferDuration = 0.35f;

    // 攻击吸附参数
    private float snapDistance;
    private float snapAngle;
    private float snapRotateSpeed;
    private LayerMask enemyLayers;
    private Transform snapTarget;
    private PlayerVitals playerVitals;
    private const float AirAttackStaminaCost = 3f;

    public AirAttackState(Animator animator, PlayerControl playerControl,
        FSMControl fsm, TestFSM testfsm, CharacterController controller, Collider weaponCollider,
        float snapDistance = 2.5f, float snapAngle = 100f, float snapRotateSpeed = 720f)
    {
        this.animator = animator;
        this.playerControl = playerControl;
        this.fsm = fsm;
        this.testfsm = testfsm;
        this.controller = controller;
        this.weaponCollider = weaponCollider;
        this.snapDistance = snapDistance;
        this.snapAngle = snapAngle;
        this.snapRotateSpeed = snapRotateSpeed;
        this.enemyLayers = testfsm.targetLayers;
    }

    public override void OnEnter()
    {
        playerVitals = testfsm.GetComponent<PlayerVitals>();
        testfsm.AirAttackEnterY = testfsm.transform.position.y;
        currentAnimStarted = false;
        animator.Play("Combo_Attack_Air_01", 0, 0f);
        snapTarget = FindSnapTarget();

        hasBufferedNextAttack = false;
        comboWindowOpen = false;
        bufferedTime = 0f;
        currentAttackStateHash = 0;
    }

    public override void OnUpdate()
    {
        if (snapTarget != null)
        {
            SmoothRotateToSnapTarget();
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        UpdateCurrentAttackStateHash();

        if (playerControl.Player.Attack.WasPressedThisFrame())
        {
            hasBufferedNextAttack = true;
            bufferedTime = Time.time;
        }

        if (TickAirAttackBuffer())
        {
            return;
        }

        if (!currentAnimStarted)
        {
            if (state.IsTag(AirAttackTag) && state.normalizedTime > 0f)
                currentAnimStarted = true;
            else
                return;
        }

        if (state.IsTag(AirAttackTag) && state.normalizedTime >= 1f && !animator.IsInTransition(0))
        {
            testfsm.JumpSoftEnter = true;
            fsm.SetState(StateType.JUMP);
        }

        if (controller.isGrounded)
        {
            fsm.SetState(testfsm.IsLockOn ? StateType.LockOn : StateType.IDlE);
            return;
        }
    }

    public override void OnExit()
    {
        testfsm.VerticalVelocity = 0f;
        animator.ResetTrigger(AirAttackTrigger);
        currentAnimStarted = false;
        snapTarget = null;
        CloseComboWindow();
        currentAttackStateHash = 0;
    }

    public void OnComboWindowOpen()
    {
        comboWindowOpen = true;
    }

    public void OnComboWindowClose()
    {
        CloseComboWindow();
    }

    private void UpdateCurrentAttackStateHash()
    {
        if (animator == null) return;

        int hash = GetCurrentOrNextAirAttackStateHash();
        if (hash == currentAttackStateHash) return;

        if (currentAttackStateHash != 0)
            ClearBufferedInput();

        currentAttackStateHash = hash;
    }

    private int GetCurrentOrNextAirAttackStateHash()
    {
        if (animator == null) return 0;

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (next.IsTag(AirAttackTag)) return next.fullPathHash;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        return current.IsTag(AirAttackTag) ? current.fullPathHash : 0;
    }

    private bool IsInAirAttackTag()
    {
        if (animator == null) return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsTag(AirAttackTag)) return true;

        if (animator.IsInTransition(0))
            return animator.GetNextAnimatorStateInfo(0).IsTag(AirAttackTag);

        return false;
    }

    private bool TickAirAttackBuffer()
    {
        if (!hasBufferedNextAttack) return false;

        if (Time.time - bufferedTime > ComboBufferDuration)
        {
            ClearBufferedInput();
            return false;
        }

        if (!currentAnimStarted || !comboWindowOpen || !IsInAirAttackTag()) return false;

        if (playerVitals != null && !playerVitals.UseStamina(AirAttackStaminaCost))
        {
            CloseComboWindow();
            testfsm.JumpSoftEnter = true;
            fsm.SetState(StateType.JUMP);
            return true;
        }

        animator.SetTrigger(AirAttackTrigger);
        currentAnimStarted = false;
        comboWindowOpen = false;
        ClearBufferedInput();
        return true;
    }

    private void ClearBufferedInput()
    {
        hasBufferedNextAttack = false;
        bufferedTime = 0f;
    }

    private void CloseComboWindow()
    {
        comboWindowOpen = false;
        ClearBufferedInput();
    }

    /// <summary>
    /// 查找攻击吸附目标：扇形射线检测前方敌人，射线碰到敌人Layer即吸附
    /// </summary>
    private Transform FindSnapTarget()
    {
        Transform playerTransform = testfsm.transform;
        Vector3 rayOrigin = playerTransform.position + Vector3.up * 0.8f;
        Vector3 forward = playerTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        float halfAngle = snapAngle * 0.5f;
        int rayCount = 10;

        Transform best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = -halfAngle + (snapAngle / (rayCount - 1)) * i;
            Vector3 rayDir = Quaternion.Euler(0f, angle, 0f) * forward;

            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, snapDistance, enemyLayers))
            {
                Transform target = hit.transform;
                ActorBase targetActor = hit.collider.GetComponentInParent<ActorBase>();
                if (targetActor != null)
                    target = targetActor.transform;

                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    best = target;
                }
            }
        }

        return best;
    }

    private void SmoothRotateToSnapTarget()
    {
        if (snapTarget == null) return;

        Transform playerTransform = testfsm.transform;
        Vector3 direction = snapTarget.position - playerTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        playerTransform.rotation = Quaternion.RotateTowards(
            playerTransform.rotation,
            targetRotation,
            snapRotateSpeed * Time.deltaTime
        );
    }
}