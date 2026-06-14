using UnityEngine;
using GameInput;
public class IdleState : StateBase
{
    
    private Animator animator;
    private FSMControl fsm;
    private PlayerControl playerControl;
    public IdleState(Animator animator,FSMControl fsm)
    {
        this.animator = animator;
        this.fsm = fsm;
    }

    public override void OnEnter()
    {
        animator.SetFloat("speed", 0f);
        animator.SetFloat("MoveX", 0f);
        animator.SetFloat("MoveY", 0f);
    }
    public override void OnExit(){}
    public override void OnUpdate(){}

}

