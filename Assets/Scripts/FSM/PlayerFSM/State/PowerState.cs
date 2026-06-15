using UnityEngine;
using GameInput;
using System.Collections.Generic;

public class PowerState : StateBase
{
    private enum Phase { Start, Loop, End }

    private Animator animator;
    private PlayerControl playerControl;
    private FSMControl fsm;
    private TestFSM testfsm;
    private PlayerVitals playerVitals;
    private CombatAudioPlayer audioPlayer;

    private Phase currentPhase;
    private float loopTimer;
    private float damageTickTimer;
    private HashSet<EnemyFSM> hitEnemiesThisTick;

    private const float LoopDuration = 3f;
    private const float DamageInterval = 0.5f;
    private const float PowerRadius = 1.5f;
    private const float PowerReach = 1.5f;

    [Header("伤害")]
    public float powerDamage = 25f;

    public PowerState(Animator animator, PlayerControl playerControl,
        FSMControl fsm, TestFSM testfsm, PlayerVitals playerVitals)
    {
        this.animator = animator;
        this.playerControl = playerControl;
        this.fsm = fsm;
        this.testfsm = testfsm;
        this.playerVitals = playerVitals;
        this.audioPlayer = testfsm.GetComponent<CombatAudioPlayer>();
    }

    public override void OnEnter()
    {
        // 消耗全部怒气
        playerVitals?.ConsumeRage(playerVitals.maxRage);

        animator.Play("Power_Start");
        currentPhase = Phase.Start;
        loopTimer = 0f;
        damageTickTimer = 0f;
        hitEnemiesThisTick = new HashSet<EnemyFSM>();
    }

    public override void OnUpdate()
    {
        switch (currentPhase)
        {
            case Phase.Start:
                UpdateStartPhase();
                break;
            case Phase.Loop:
                UpdateLoopPhase();
                break;
            case Phase.End:
                UpdateEndPhase();
                break;
        }
    }

    private void UpdateStartPhase()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Power_Start") && state.normalizedTime >= 1f && !animator.IsInTransition(0))
        {
            animator.Play("Power_Loop");
            currentPhase = Phase.Loop;
            loopTimer = 0f;
            damageTickTimer = 0f;
        }
    }

    private void UpdateLoopPhase()
    {
        loopTimer += Time.deltaTime;
        damageTickTimer += Time.deltaTime;

        // 每0.5秒造成一次范围伤害
        if (damageTickTimer >= DamageInterval)
        {
            damageTickTimer -= DamageInterval;
            DealAreaDamage();
        }

        // 3秒后进入收尾阶段
        if (loopTimer >= LoopDuration)
        {
            animator.Play("Power_End");
            currentPhase = Phase.End;
        }
    }

    private void UpdateEndPhase()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Power_End") && state.normalizedTime >= 1f && !animator.IsInTransition(0))
        {
            fsm.SetState(testfsm.IsLockOn ? StateType.LockOn : StateType.IDlE);
        }
    }

    private void DealAreaDamage()
    {
        Transform playerTransform = testfsm.transform;
        Vector3 sphereCenter = playerTransform.position + playerTransform.forward * PowerReach + Vector3.up * 0.5f;

        Collider[] hits = Physics.OverlapSphere(sphereCenter, PowerRadius, testfsm.targetLayers);

        foreach (Collider col in hits)
        {
            EnemyFSM enemy = col.GetComponentInParent<EnemyFSM>();
            if (enemy == null) continue;

            Vector3 dir = enemy.transform.position - playerTransform.position;
            dir.y = 0;
            if (dir.magnitude < 0.01f) dir = playerTransform.forward;
            dir.Normalize();

            // Power攻击：不可格挡、不可闪避，直接造成伤害
            enemy.TakeDamage("F", dir, false, playerTransform, powerDamage, true);
        }
    }

    public override void OnExit()
    {
        hitEnemiesThisTick?.Clear();
        hitEnemiesThisTick = null;
    }
}
