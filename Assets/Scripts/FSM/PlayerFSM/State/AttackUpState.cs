using UnityEngine;
using GameInput;

public class AttackUpState : StateBase
{
    private Animator animator;
    private PlayerControl playerControl;
    private FSMControl fsm;
    private TestFSM testfsm;
    private CharacterController controller;
    private Collider weaponCollider;
    private bool isAirAttack;
    private bool hasAttackStarted;
    private int enterStuckFrames;

    // 攻击吸附参数
    private float snapDistance;
    private float snapAngle;
    private float snapRotateSpeed;
    private LayerMask enemyLayers;
    private Transform snapTarget;
    private TestFSM_test testFSM_test;
    private Collider weaponCol;
    private float attackSnapDistance;
    private float attackSnapAngle;
    private float attackSnapRotateSpeed;
    private const string GroundStateName = "Attack_Up_Floor_To_Air";
    private const string AirStateName = "Attack_Up_Air_To_Air";

    public AttackUpState(Animator animator, PlayerControl playerControl,
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

    public AttackUpState(Animator animator, PlayerControl playerControl, FSMControl fsm, TestFSM_test testFSM_test, CharacterController controller, Collider weaponCol, float attackSnapDistance, float attackSnapAngle, float attackSnapRotateSpeed)
    {
        this.animator = animator;
        this.playerControl = playerControl;
        this.fsm = fsm;
        this.testFSM_test = testFSM_test;
        this.controller = controller;
        this.weaponCol = weaponCol;
        this.attackSnapDistance = attackSnapDistance;
        this.attackSnapAngle = attackSnapAngle;
        this.attackSnapRotateSpeed = attackSnapRotateSpeed;
    }


    public override void OnEnter()
    {
        isAirAttack = !testfsm.IsGrounded;
        hasAttackStarted = false;
        enterStuckFrames = 0;
        string targetState = isAirAttack ? AirStateName : GroundStateName;
        animator.CrossFadeInFixedTime(targetState, 0.05f, 0, 0.2f);
        snapTarget = FindSnapTarget();
    }

    public override void OnUpdate()
    {
        if (snapTarget != null)
        {
            SmoothRotateToSnapTarget();
        }

        if (!hasAttackStarted)
        {
            enterStuckFrames++;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if ((state.IsName(GroundStateName) || state.IsName(AirStateName))
                && state.normalizedTime > 0f)
            {
                hasAttackStarted = true;
            }
            else if (enterStuckFrames >= 5)
            {
                string targetState = isAirAttack ? AirStateName : GroundStateName;
                animator.CrossFadeInFixedTime(targetState, 0.01f, 0, 0.2f);
                hasAttackStarted = true;
            }
            if (!hasAttackStarted) return;
        }

        if (isAirAttack && testfsm.IsGrounded)
        {
            fsm.SetState(testfsm.IsLockOn ? StateType.LockOn : StateType.IDlE);
            return;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        bool isInAttackUp = currentState.IsName(GroundStateName) || currentState.IsName(AirStateName);
        bool isTransitioningToAttackUp = false;
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            isTransitioningToAttackUp = next.IsName(GroundStateName) || next.IsName(AirStateName);
        }

        bool animationFinished = currentState.normalizedTime >= 1f && !animator.IsInTransition(0);
        bool hasLeftAttack = !isInAttackUp && !isTransitioningToAttackUp;

        if (animationFinished || hasLeftAttack)
        {
            if (!testfsm.IsGrounded){
                testfsm.JumpSoftEnter = true;
                fsm.SetState(StateType.JUMP);
            }
            else
                fsm.SetState(testfsm.IsLockOn ? StateType.LockOn : StateType.IDlE);
        }
    }

    public override void OnExit()
    {
        hasAttackStarted = false;
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
