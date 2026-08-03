using UnityEngine;

public class EnemyGetUpState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private EnemyFSM enemyFSM;


    public EnemyGetUpState(Animator animator, EnemyFSMControl fsm, EnemyFSM enemyFSM)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM = enemyFSM;
    }

    public override void OnEnter()
    {
        animator.CrossFadeInFixedTime("Get_Up", 0.1f, 0);
    }

    public override void OnUpdate()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Get_Up") && state.normalizedTime >= 0.95f)
        {
            fsm.SetState(EnemyStateType.IDLE);
        }
    }

    public override void OnExit() { }
}
