using UnityEngine;

public class EnemyKnockDownState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private EnemyFSM enemyFSM;

    private enum Phase { Start, Loop, End }
    private Phase phase;
    private float loopTimer;
    private float loopDuration = 10f;

    public EnemyKnockDownState(Animator animator, EnemyFSMControl fsm, EnemyFSM enemyFSM)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM = enemyFSM;
    }

    public override void OnEnter()
    {
        animator.CrossFadeInFixedTime("Knock_Down_Start", 0.05f, 0);
        phase = Phase.Start;
    }

    public override void OnUpdate()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        switch (phase)
        {
            case Phase.Start:
                if (state.IsName("Knock_Down_Start") && state.normalizedTime >= 0.95f)
                {
                    animator.CrossFadeInFixedTime("Knock_Down_Loop", 0.1f, 0);
                    phase = Phase.Loop;
                    loopTimer = 0f;
                }
                break;

            case Phase.Loop:
                loopTimer += Time.deltaTime;
                if (loopTimer >= loopDuration)
                {
                    animator.CrossFadeInFixedTime("Knock_Down_End", 0.1f, 0);
                    phase = Phase.End;
                }
                break;

            case Phase.End:
                if (state.IsName("Knock_Down_End") && state.normalizedTime >= 0.95f)
                {
                    fsm.SetState(EnemyStateType.IDLE);
                }
                break;
        }
    }

    public override void OnExit()
    {
        loopTimer = 0f;
    }
}
