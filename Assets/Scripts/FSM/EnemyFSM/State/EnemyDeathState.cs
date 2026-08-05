using UnityEngine;

public class EnemyDeathState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private EnemyFSM enemyFSM;
    private CharacterController controller;

    private bool hasLanded;
    private bool animFrozen;
    private float holdTimer;
    private float verticalVelocity;
    private const float holdDuration = 3f;
    private const float maxFallSpeed = 20f;
    private const string DeathAnim = "Hit_Death";

    public EnemyDeathState(Animator animator, EnemyFSMControl fsm,
        EnemyFSM enemyFSM, CharacterController controller)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM = enemyFSM;
        this.controller = controller;
    }

    public override void OnEnter()
    {
        hasLanded = enemyFSM.IsGrounded;
        animFrozen = false;
        holdTimer = 0f;
        verticalVelocity = 0f;
        animator.Play(DeathAnim);
    }

    public override void OnUpdate()
    {
        // 空中下落
        if (!hasLanded && !enemyFSM.IsGrounded)
        {
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            float step = Mathf.Max(verticalVelocity, -maxFallSpeed) * Time.deltaTime;
            controller.Move(new Vector3(0f, step, 0f));
        }
        else if (!hasLanded && enemyFSM.IsGrounded)
        {
            hasLanded = true;
        }

        // 等落地 + 动画播完 → 冻帧
        if (!animFrozen && hasLanded)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(DeathAnim) && state.normalizedTime >= 1f && !animator.IsInTransition(0))
            {
                animator.speed = 0f;
                animFrozen = true;
            }
            return;
        }

        if (!animFrozen) return;

        // 保持3秒后消失
        holdTimer += Time.unscaledDeltaTime;
        if (holdTimer >= holdDuration)
            enemyFSM.gameObject.SetActive(false);
    }

    public override void OnExit() { }
}
