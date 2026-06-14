using UnityEngine;

public class EnemyIdleState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSM enemyFSM;
    private CharacterController controller;

    public EnemyIdleState(Animator animator, EnemyFSMControl fsm,
        EnemyFSM enemyFSM, CharacterController controller)
    {
        this.animator = animator;
        this.enemyFSM = enemyFSM;
        this.controller = controller;
    }

    public override void OnEnter()
    {
        animator.CrossFade("BaseMotion", 0.05f);
        Debug.Log("进入Idle状态");
        animator.SetFloat("speed", 0f);
    }

    public override void OnUpdate()
    {
    }

    public override void OnExit() { }
}
