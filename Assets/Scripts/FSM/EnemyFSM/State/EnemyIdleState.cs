using UnityEngine;

public class EnemyIdleState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSM enemyFSM;
    private CharacterController controller;
    private EnemyFSMControl fsm;
    private EnemyFSM_test enemyFSM_test;


    public EnemyIdleState(Animator animator, EnemyFSMControl fsm,
        EnemyFSM enemyFSM, CharacterController controller)
    {
        this.animator = animator;
        this.enemyFSM = enemyFSM;
        this.controller = controller;
    }

    public EnemyIdleState(Animator animator, EnemyFSMControl fsm, EnemyFSM_test enemyFSM_test, CharacterController controller)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM_test = enemyFSM_test;
        this.controller = controller;
    }


    public override void OnEnter()
    {
        animator.CrossFade("BaseMotion", 0.05f);
        animator.SetFloat("speed", 0f);
    }

    public override void OnUpdate()
    {
    }

    public override void OnExit() { }
}
