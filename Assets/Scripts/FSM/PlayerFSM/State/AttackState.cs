using UnityEngine;
using GameInput;

public class AttackState : StateBase
{
    private Animator animator;
    private PlayerControl playerControl;
    private FSMControl fsm;
    private TestFSM testfsm;
    private string attackTrigger;
    private bool hasAttackStarted;
    private const string AttackTag = "Attack";
    private Collider weaponCollider;

    // 攻击吸附参数
    private float snapDistance;
    private float snapAngle;
    private float snapRotateSpeed;
    private LayerMask enemyLayers;
    private Transform snapTarget;

    public AttackState(Animator animator, PlayerControl playerControl, 
        FSMControl fsm, TestFSM testfsm, string attackTrigger = "LAtk", Collider weaponCollider = null,
        float snapDistance = 2.5f, float snapAngle = 100f, float snapRotateSpeed = 720f)
    {
        this.animator = animator;
        this.playerControl = playerControl;
        this.fsm = fsm;
        this.testfsm = testfsm;
        this.attackTrigger = attackTrigger;
        this.weaponCollider = weaponCollider;
        this.snapDistance = snapDistance;
        this.snapAngle = snapAngle;
        this.snapRotateSpeed = snapRotateSpeed;
        this.enemyLayers = testfsm.targetLayers;
    }

    public override void OnEnter()
    {
        animator.SetTrigger(attackTrigger);
        hasAttackStarted = false;
        snapTarget = FindSnapTarget();
    }

    public override void OnUpdate()
    {
        if (snapTarget != null)
        {
            SmoothRotateToSnapTarget();
        }

        if (playerControl.Player.ComboSet1.WasPressedThisFrame())
        {
            if (fsm.stateType != StateType.ATTACK_01)
            {
                testfsm.currentComboSet = 1;
                fsm.SetState(StateType.ATTACK_01);
                return;
            }
        }
        if (playerControl.Player.ComboSet2.WasPressedThisFrame())
        {
            if (fsm.stateType != StateType.ATTACK_02)
            {
                testfsm.currentComboSet = 2;
                fsm.SetState(StateType.ATTACK_02);
                return;
            }
        }

        if (playerControl.Player.Attack.WasPressedThisFrame())
        {
            animator.SetTrigger(attackTrigger);
            Debug.Log("开始连击");
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
            return;
        }
    }

    public override void OnExit()
    {
        animator.ResetTrigger(attackTrigger);
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
    public void OnAttackComboCheck() { }
    public void OnAreaAttack() { }
}
