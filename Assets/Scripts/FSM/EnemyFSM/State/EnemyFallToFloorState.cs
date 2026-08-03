using UnityEngine;

public class EnemyFallToFloorState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private EnemyFSM enemyFSM;
    private CharacterController controller;

    private float gravity = -20f;
    private float fallSpeed;

    private enum Phase { Start, Loop, End }
    private Phase phase;
    private EnemyFSM_test enemyFSM_test;


    public EnemyFallToFloorState(Animator animator, EnemyFSMControl fsm,
        EnemyFSM enemyFSM, CharacterController controller)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM = enemyFSM;
        this.controller = controller;
    }

    public EnemyFallToFloorState(Animator animator, EnemyFSMControl fsm, EnemyFSM_test enemyFSM_test, CharacterController controller)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM_test = enemyFSM_test;
        this.controller = controller;
    }


    public override void OnEnter()
    {
        fallSpeed = 0f;
        animator.CrossFadeInFixedTime("Hit_Air_To_Floor_Start", 0.05f, 0);
        phase = Phase.Start;
    }

    public override void OnUpdate()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        switch (phase)
        {
            case Phase.Start:
                if (state.IsName("Hit_Air_To_Floor_Start") && state.normalizedTime >= 0.95f)
                {
                    animator.CrossFadeInFixedTime("Hit_Air_To_Floor_Loop", 0.1f, 0);
                    phase = Phase.Loop;
                }
                break;

            case Phase.Loop:
                fallSpeed += gravity*Time.deltaTime;
                fallSpeed = Mathf.Max(fallSpeed,-30f);

                Vector3 move = new Vector3(0,fallSpeed*Time.deltaTime,0);

                if (controller != null && controller.enabled)
                    controller.Move(move);
                else
                    enemyFSM.transform.position += move;
                    
                // 落地时切换到 End
                if (enemyFSM != null && enemyFSM.IsGrounded)
                {
                    animator.CrossFadeInFixedTime("Hit_Air_To_Floor_End", 0.05f, 0);
                    phase = Phase.End;
                }
                break;

            case Phase.End:
                if (state.IsName("Hit_Air_To_Floor_End") && state.normalizedTime >= 0.95f)
                {
                    fsm.SetState(EnemyStateType.GETUP);
                }
                break;
        }
    }

    public override void OnExit() { }
}
