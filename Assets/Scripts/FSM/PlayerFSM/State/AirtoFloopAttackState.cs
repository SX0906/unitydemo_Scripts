using UnityEngine;
using GameInput;

public class AirtoFloopAttackState : StateBase
{
    private Animator animator;
    private PlayerControl playerControl;
    private FSMControl fsm;
    private TestFSM testfsm;
    private CharacterController controller;

    private enum Phase { Start, Loop, End }
    private Phase currentPhase;

    // ===== 可调参数 =====
    private float slamSpeed = 25f;         // Loop阶段向下位移速度
    private float damageRadius = 2f;       // 落地范围伤害半径
    private float damageAmount = 30f;      // 落地伤害值

    private const string StartAnim = "Attack_Air_to_Floor_Start";
    private const string LoopAnim  = "Attack_Air_to_Floor_Loop";
    private const string EndAnim   = "Attack_Air_to_Floor_End";

    private bool hasDealtDamage;

    public AirtoFloopAttackState(Animator animator, PlayerControl playerControl,
        FSMControl fsm, TestFSM testfsm, CharacterController controller)
    {
        this.animator = animator;
        this.playerControl = playerControl;
        this.fsm = fsm;
        this.testfsm = testfsm;
        this.controller = controller;
    }

    public override void OnEnter()
    {
        currentPhase = Phase.Start;
        hasDealtDamage = false;
        testfsm.VerticalVelocity = 0f;
        animator.Play(StartAnim, 0, 0f);
    }

    public override void OnUpdate()
    {
        switch (currentPhase)
        {
            case Phase.Start: UpdateStart(); break;
            case Phase.Loop:  UpdateLoop();  break;
            case Phase.End:   UpdateEnd();   break;
        }
    }

    private void UpdateStart()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName(StartAnim) && state.normalizedTime >= 1f && !animator.IsInTransition(0))
        {
            currentPhase = Phase.Loop;
            animator.Play(LoopAnim, 0, 0f);
        }
    }

    private void UpdateLoop()
    {
        // 手动控制向下位移（Loop动画本身没有Y轴下降，水平也不位移）
        Vector3 move = Vector3.down * slamSpeed * Time.deltaTime;
        controller.Move(move);

        // 检测落地 → 进入End
        if (testfsm.IsGrounded)
        {
            testfsm.VerticalVelocity = 0f;
            currentPhase = Phase.End;
            animator.Play(EndAnim, 0, 0f);
            DealAreaDamage();
        }
    }

    private void UpdateEnd()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        // End动画播放完毕 → 回到地面状态（不再走跳跃下落动画）
        if (state.IsName(EndAnim) && state.normalizedTime >= 0.5f && !animator.IsInTransition(0))
        {
            Debug.Log("下落攻击动画动画完成");
            fsm.SetState(testfsm.IsLockOn ? StateType.LockOn : StateType.IDlE);
        }
    }

    private void DealAreaDamage()
    {
        if (hasDealtDamage) return;
        hasDealtDamage = true;

        Vector3 center = testfsm.transform.position;
        Collider[] hits = Physics.OverlapSphere(center, damageRadius, testfsm.targetLayers);

        ActorVitals playerVitals = testfsm.GetComponent<ActorVitals>();

        foreach (Collider hit in hits)
        {
            EnemyFSM enemy = hit.GetComponent<EnemyFSM>();
            if (enemy == null) continue;

            Vector3 dir = enemy.transform.position - testfsm.transform.position;
            dir.y = 0f;
            if (dir.magnitude < 0.01f) dir = testfsm.transform.forward;

            enemy.TakeDamage("F", dir, false, testfsm.transform, damageAmount);

            // 击杀/击中 → 怒气
            EnemyVitals enemyVitals = enemy.GetComponent<EnemyVitals>();
            if (enemyVitals != null && enemyVitals.IsDead)
                playerVitals?.OnKill();

            if (playerVitals != null)
                playerVitals.GainRage(5f);
        }
    }

    public override void OnExit()
    {
        testfsm.VerticalVelocity = 0f;
        currentPhase = Phase.Start;
        hasDealtDamage = false;
    }
}