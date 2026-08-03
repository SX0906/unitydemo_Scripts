using UnityEngine;

public class EnemyBlockBreakState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private EnemyFSM enemyFSM;
    private CombatAudioPlayer audioPlayer;
    private Transform transform;

    private float hitStopDuration = 1.5f;
    private float animSlowSpeed = 0.2f;
    private float timer;
    private bool hitStopActive;
    private EnemyFSM_test enemyFSM_test;


    public EnemyBlockBreakState(Animator animator, EnemyFSMControl fsm,
        EnemyFSM enemyFSM, Transform transform, CombatAudioPlayer audioPlayer)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM = enemyFSM;
        this.transform = transform;
        this.audioPlayer = audioPlayer;
    }

    public EnemyBlockBreakState(Animator animator, EnemyFSMControl fsm, EnemyFSM_test enemyFSM_test, Transform transform, CombatAudioPlayer audioPlayer)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM_test = enemyFSM_test;
        this.transform = transform;
        this.audioPlayer = audioPlayer;
    }


    public override void OnEnter()
    {
        animator.CrossFadeInFixedTime("Block_Break", 0.05f, 0);
        audioPlayer?.PlayHitSound();

        timer = hitStopDuration;
        hitStopActive = true;
        animator.speed = animSlowSpeed;
    }

    public override void OnUpdate()
    {
        if (hitStopActive)
        {
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
            {
                animator.speed = 1f;
                hitStopActive = false;
                fsm.SetState(EnemyStateType.IDLE);
                return;
            }
        }
    }

    public override void OnExit()
    {
        if (hitStopActive)
        {
            animator.speed = 1f;
            hitStopActive = false;
        }
        enemyFSM.GetComponent<EnemyVitals>()?.ResetPosture();
    }
}
