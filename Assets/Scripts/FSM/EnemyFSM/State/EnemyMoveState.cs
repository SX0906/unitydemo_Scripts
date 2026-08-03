using UnityEngine;

public class EnemyMoveState : EnemyStateBase
{
    private Animator animator;
    private Transform transform;
    private CharacterController controller;
    private EnemyFSM enemyFSM;

    private float moveSpeed = 1.2f;
    private float gravity = -20f;
    private float verticalVelocity;
    private EnemyFSMControl fsm;
    private EnemyFSM_test enemyFSM_test;

    public EnemyMoveState(Animator animator, EnemyFSMControl fsm,
        Transform transform, CharacterController controller, EnemyFSM enemyFSM)
    {
        this.animator = animator;
        this.transform = transform;
        this.controller = controller;
        this.enemyFSM = enemyFSM;
    }

    public EnemyMoveState(Animator animator, EnemyFSMControl fsm, Transform transform, CharacterController controller, EnemyFSM_test enemyFSM_test)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.transform = transform;
        this.controller = controller;
        this.enemyFSM_test = enemyFSM_test;
    }

    public override void OnEnter()
    {
        animator.SetFloat("speed", 2f);
    }

    public override void OnUpdate()
    {
        if (!enemyFSM.IsGrounded)
            return;

        // 优先使用实时玩家位置
        Vector3 targetPos = enemyFSM.targetPlayer != null
            ? enemyFSM.targetPlayer.position
            : enemyFSM.lastKnownPlayerPos;

        Vector3 horizontalOffset = targetPos - transform.position;
        horizontalOffset.y = 0;
        float distanceToTarget = horizontalOffset.magnitude;

        // 始终面向目标
        if (horizontalOffset != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(horizontalOffset);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        if (enemyFSM.IsGrounded && verticalVelocity < 0)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        // 距离玩家1.8米内停止移动，仅应用重力
        Vector3 velocity;
        if (distanceToTarget <= 1.8f)
        {
            velocity = Vector3.zero;
        }
        else
        {
            Vector3 moveDir = horizontalOffset.normalized;
            velocity = moveDir * moveSpeed;
        }
        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    public override void OnExit()
    {
        animator.SetFloat("speed", 0f);
    }
}
