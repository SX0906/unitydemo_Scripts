using UnityEngine;
using GameInput;

public class BackAttackState : StateBase
{
    private Animator animator;
    private PlayerControl playerControl;
    private FSMControl fsm;
    private TestFSM testfsm;
    private Collider weaponCollider;
    private float snapDistance;
    private float snapAngle;
    private float snapRotateSpeed;
    private LayerMask enemyLayers;
    private Transform snapTarget;
    private bool hasAttackStarted;

    private const string AttackTag = "Attack";
    private const string BackAttackAnim = "BackAttack";

    public BackAttackState(Animator animator, PlayerControl playerControl,
        FSMControl fsm, TestFSM testfsm, Collider weaponCollider,
        float snapDistance, float snapAngle, float snapRotateSpeed)
    {
        this.animator = animator;
        this.playerControl = playerControl;
        this.fsm = fsm;
        this.testfsm = testfsm;
        this.weaponCollider = weaponCollider;
        this.snapDistance = snapDistance;
        this.snapAngle = snapAngle;
        this.snapRotateSpeed = snapRotateSpeed;
        this.enemyLayers = testfsm.targetLayers;
    }

    public override void OnEnter()
    {
        // 消耗反击机会
        testfsm.backAttackAvailable = false;
        testfsm.backAttackTimer = 0f;

        animator.Play("BackAttack");
        hasAttackStarted = false;
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
            if (IsInAttackTag())
            {
                hasAttackStarted = true;
            }
            else
            {
                return;
            }
        }

        if (!IsInAttackTag())
        {
            fsm.SetState(testfsm.IsLockOn ? StateType.LockOn : StateType.IDlE);
        }
    }

    public override void OnExit()
    {

        snapTarget = null;
    }

    private bool IsInAttackTag()
    {
        if (animator == null) return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsTag(AttackTag)) return true;

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (next.IsTag(AttackTag)) return true;
        }

        return false;
    }

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
