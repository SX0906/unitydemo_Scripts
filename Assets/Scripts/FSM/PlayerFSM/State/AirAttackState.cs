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

    // 攻击吸附参数
    private float snapDistance;
    private float snapAngle;
    private float snapRotateSpeed;
    private LayerMask enemyLayers;
    private Transform snapTarget;
    private PlayerVitals playerVitals;
    private const float AirAttackStaminaCost = 5f;

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
    }

    public override void OnUpdate()
    {
        if (snapTarget != null)
        {
            SmoothRotateToSnapTarget();
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        if (!currentAnimStarted)
        {
            if (state.IsTag(AirAttackTag) && state.normalizedTime > 0f)
                currentAnimStarted = true;
            else
                return;
        }

        if (playerControl.Player.Attack.WasPressedThisFrame())
        {
            if (playerVitals == null || playerVitals.UseStamina(AirAttackStaminaCost))
            {
                animator.SetTrigger(AirAttackTrigger);
                currentAnimStarted = false;
            }
            else
            {
                // 体力不足 → 进入下落动画
                testfsm.JumpSoftEnter = true;
                fsm.SetState(StateType.JUMP);
            }
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
