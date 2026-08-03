using UnityEngine;

public class DeathState : StateBase
{
    private Animator animator;
    private FSMControl fsm;
    private TestFSM testfsm;

    private bool animFinished;
    private float holdTimer;
    private const float holdDuration = 3f;
    private const string DeathAnim = "Hit_Death";

    public DeathState(Animator animator, FSMControl fsm, TestFSM testfsm)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.testfsm = testfsm;
    }

    public override void OnEnter()
    {
        animFinished = false;
        holdTimer = 0f;
        animator.Play(DeathAnim);
    }

    public override void OnUpdate()
    {
        if (!animFinished)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(DeathAnim) && state.normalizedTime >= 1f && !animator.IsInTransition(0))
            {
                animFinished = true;
                animator.speed = 0f;   // 冻结在最后一帧
            }
            return;
        }

        holdTimer += Time.unscaledDeltaTime;
        if (holdTimer >= holdDuration)
            testfsm.gameObject.SetActive(false);
    }

    public override void OnExit() { }
}