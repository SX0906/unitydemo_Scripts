using UnityEngine;
using GameInput;
using System.Collections;
using UnityEngine.Playables;

public class PowerState : StateBase
{
    private enum Phase { Start, Loop, End }

    private Animator animator;
    private FSMControl fsm;
    private TestFSM testfsm;
    private PlayerVitals playerVitals;

    private Phase currentPhase;
    private Coroutine endDamageCoroutine;
    private bool damageSequenceComplete;

    // Timeline 模式下，End 阶段的开始时间（Start + Loop 时长）
    private float endPhaseStartTime;

    // Timeline 驱动
    private PlayableDirector powerDirector;

    // 伤害参数
    private const float PowerRadius = 1.8f;
    private const float FastInterval = 0.12f;
    private const float FinalInterval = 0.2f;
    private const float FirstDamageDelay = 0.02f;  // 1x 倍速下 End 阶段首次伤害延迟
    [Header("伤害")]
    public float powerDamage = 25f;

    [Header("伤害节奏（Timeline 为 0.65x 倍速下的参数）")]
    [Tooltip("1x 倍速下首次伤害的延迟（0.02s），运行时自动除以 timeScale")]
    public float powerTimeScale = 0.65f;

    public PowerState(Animator animator, PlayerControl playerControl,
        FSMControl fsm, TestFSM testfsm, PlayerVitals playerVitals)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.testfsm = testfsm;
        this.playerVitals = playerVitals;
        this.powerDirector = testfsm.powerDirector;
    }

    public override void OnEnter()
    {
        // 消耗全部怒气
        playerVitals?.ConsumeRage(playerVitals.maxRage);

        // 禁用正常相机控制器，防止运镜期间鼠标旋转干扰
        if (testfsm.cameraController != null)
            testfsm.cameraController.enabled = false;

        Debug.Log($"[PowerState] OnEnter — powerDirector={powerDirector != null}");

        // 启动 Timeline（Timeline 内的 Animation Track 会自动播放 Start → Loop → End）
        if (powerDirector != null)
        {
            powerDirector.time = 0;
            powerDirector.Play();
        }
        else
        {
            // 兜底：没有 Timeline 时走旧逻辑
            animator.Play("Power_Start");
        }

        currentPhase = Phase.Start;
        damageSequenceComplete = false;
        endDamageCoroutine = null;

        // 从动画 Clip 计算 End 阶段开始时间（Start + Loop 的总时长）
        endPhaseStartTime = GetClipLength("Power_Start") + GetClipLength("Power_Loop");
    }

    public override void OnUpdate()
    {
        if (powerDirector != null)
            UpdateTimelineMode();
        else
            UpdateLegacyMode();
    }

    // ================================================================
    // Timeline 模式：Timeline 自动驱动动画阶段，这里只检测状态
    // ================================================================
    private void UpdateTimelineMode()
    {
        if (powerDirector == null) return;
        // 用 Timeline 当前时间检测是否进入 End 阶段（不再依赖 AnimatorStateInfo）
        if (powerDirector.time >= endPhaseStartTime && endDamageCoroutine == null && !damageSequenceComplete)
        {
            currentPhase = Phase.End;
            // Timeline 模式下由 Signal Track 控制伤害时机，不再启动协程
            damageSequenceComplete = true;
        }

        // Timeline 播放完毕 → 退出 Power 状态
        if (powerDirector.state != PlayState.Playing)
        {
            fsm.SetState(testfsm.IsLockOn ? StateType.LockOn : StateType.IDlE);
        }
    }

    // ================================================================
    // 旧模式：没有 Timeline 时走回原来的 animator.Play 逻辑
    // ================================================================
    private void UpdateLegacyMode()
    {
        switch (currentPhase)
        {
            case Phase.Start:
                {
                    AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(0);
                    if (s.IsName("Power_Start") && s.normalizedTime >= 1f && !animator.IsInTransition(0))
                    {
                        animator.Play("Power_Loop");
                        currentPhase = Phase.Loop;
                    }
                }
                break;
            case Phase.Loop:
                {
                    AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(0);
                    if (s.IsName("Power_Loop") && s.normalizedTime >= 1f && !animator.IsInTransition(0))
                    {
                        animator.Play("Power_End");
                        currentPhase = Phase.End;
                        endDamageCoroutine = testfsm.StartCoroutine(EndDamageSequence());
                    }
                }
                break;
            case Phase.End:
                {
                    if (!damageSequenceComplete) return;
                    AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(0);
                    if (s.IsName("Power_End") && s.normalizedTime >= 1f && !animator.IsInTransition(0))
                    {
                        fsm.SetState(testfsm.IsLockOn ? StateType.LockOn : StateType.IDlE);
                    }
                }
                break;
        }
    }

    /// <summary>
    /// End 伤害序列：前5次每隔0.12秒造成一次伤害，
    /// 然后间隔0.2秒造成最后一次伤害，共计6次。
    /// </summary>
    private IEnumerator EndDamageSequence()
    {
        // 首次伤害延迟（1x: 0.02s → 0.65x: 0.02/0.65）
        yield return new WaitForSeconds(FirstDamageDelay / powerTimeScale);

        for (int i = 0; i < 5; i++)
        {
            DealAreaDamage();
            yield return new WaitForSeconds(FastInterval / powerTimeScale);
        }
        yield return new WaitForSeconds(FinalInterval / powerTimeScale);
        DealAreaDamage();

        damageSequenceComplete = true;
    }

    /// <summary>以玩家为中心半径1.8米的范围伤害</summary>
    private void DealAreaDamage()
    {
        Transform t = testfsm.transform;
        // 伤害球：前方1.8米，半径1.8米
        Vector3 center = t.position + t.forward * 1.8f + Vector3.up * 0.5f;

        Collider[] hits = Physics.OverlapSphere(center, PowerRadius, testfsm.targetLayers);

        foreach (Collider col in hits)
        {
            EnemyFSM enemy = col.GetComponentInParent<EnemyFSM>();
            if (enemy == null) continue;

            Vector3 dir = enemy.transform.position - t.position;
            dir.y = 0;
            if (dir.magnitude < 0.01f) dir = t.forward;
            dir.Normalize();

            // Power攻击：不可格挡、不可闪避，直接造成伤害
            enemy.TakeDamage("F", dir, false, t, powerDamage, true);
        }
    }

    /// <summary>从 Animator Controller 的动画 Clip 列表获取指定 Clip 的长度</summary>
    private float GetClipLength(string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return 0f;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName) return clip.length;
        }
        return 0f;
    }

    public override void OnExit()
    {
        if (endDamageCoroutine != null)
        {
            testfsm.StopCoroutine(endDamageCoroutine);
            endDamageCoroutine = null;
        }

        if (testfsm.cameraController != null)
            testfsm.cameraController.enabled = true;

        if (powerDirector != null)
            powerDirector.Stop();
    }
}
