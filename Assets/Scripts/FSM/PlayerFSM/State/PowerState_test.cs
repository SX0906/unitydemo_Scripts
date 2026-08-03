using UnityEngine;
using GameInput;
using System.Collections;
using UnityEngine.Playables;

public class PowerState_test : StateBase
{
    private enum Phase { Start, Loop, End }
    private Animator animator;
    private FSMControl fsm;
    private PlayerVitals playerVitals;
    private PlayableDirector powerDirector;
    private Phase currentPhase;
    private Coroutine endDamageCoroutine;
    private bool damageSequenceComplete;
    private float endPhaseStartTime;
    private readonly LayerMask _targetLayers;
    private readonly Transform _ownerTransform;
    private readonly System.Action<bool> _setCameraEnabled;
    private readonly System.Func<bool> _isLockOn;
    private readonly MonoBehaviour _coroutineHost;

    private const float PowerRadius = 1.8f;
    private const float FastInterval = 0.12f;
    private const float FinalInterval = 0.2f;
    private const float FirstDamageDelay = 0.02f;
    public float powerDamage = 25f;
    public float powerTimeScale = 0.65f;

    public PowerState_test(Animator animator, PlayerControl playerControl, FSMControl fsm, PlayerVitals playerVitals, PlayableDirector powerDirector, LayerMask targetLayers, Transform ownerTransform, System.Action<bool> setCameraEnabled, System.Func<bool> isLockOn, MonoBehaviour coroutineHost)
    {
        this.animator = animator; this.fsm = fsm; this.playerVitals = playerVitals;
        this.powerDirector = powerDirector; _targetLayers = targetLayers;
        _ownerTransform = ownerTransform; _setCameraEnabled = setCameraEnabled;
        _isLockOn = isLockOn; _coroutineHost = coroutineHost;
    }

    public override void OnEnter()
    {
        playerVitals?.ConsumeRage(playerVitals.maxRage);
        _setCameraEnabled?.Invoke(false);
        if (powerDirector != null) { powerDirector.time = 0; powerDirector.Play(); }
        else { animator.Play("Power_Start"); }
        currentPhase = Phase.Start; damageSequenceComplete = false; endDamageCoroutine = null;
        endPhaseStartTime = GetClipLength("Power_Start") + GetClipLength("Power_Loop");
    }

    public override void OnUpdate()
    {
        if (powerDirector != null) UpdateTimelineMode(); else UpdateLegacyMode();
    }

    private void UpdateTimelineMode()
    {
        if (powerDirector == null) return;
        if (powerDirector.time >= endPhaseStartTime && endDamageCoroutine == null && !damageSequenceComplete) { currentPhase = Phase.End; damageSequenceComplete = true; }
        if (powerDirector.state != PlayState.Playing) fsm.SetState(_isLockOn() ? StateType.LockOn : StateType.IDlE);
    }

    private void UpdateLegacyMode()
    {
        switch (currentPhase)
        {
            case Phase.Start: { AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(0); if (s.IsName("Power_Start") && s.normalizedTime >= 1f && !animator.IsInTransition(0)) { animator.Play("Power_Loop"); currentPhase = Phase.Loop; } } break;
            case Phase.Loop: { AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(0); if (s.IsName("Power_Loop") && s.normalizedTime >= 1f && !animator.IsInTransition(0)) { animator.Play("Power_End"); currentPhase = Phase.End; endDamageCoroutine = _coroutineHost.StartCoroutine(EndDamageSequence()); } } break;
            case Phase.End: { if (!damageSequenceComplete) return; AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(0); if (s.IsName("Power_End") && s.normalizedTime >= 1f && !animator.IsInTransition(0)) fsm.SetState(_isLockOn() ? StateType.LockOn : StateType.IDlE); } break;
        }
    }

    private IEnumerator EndDamageSequence()
    {
        yield return new WaitForSeconds(FirstDamageDelay / powerTimeScale);
        for (int i = 0; i < 5; i++) { DealAreaDamage(); yield return new WaitForSeconds(FastInterval / powerTimeScale); }
        yield return new WaitForSeconds(FinalInterval / powerTimeScale);
        DealAreaDamage(); damageSequenceComplete = true;
    }

    private void DealAreaDamage()
    {
        Vector3 center = _ownerTransform.position + _ownerTransform.forward * 1.8f + Vector3.up * 0.5f;
        Collider[] hits = Physics.OverlapSphere(center, PowerRadius, _targetLayers);
        foreach (Collider col in hits)
        {
            ICombatTarget target = col.GetComponentInParent<ICombatTarget>();
            if (target == null) continue;
            Vector3 dir = (target.Transform.position - _ownerTransform.position).normalized;
            target.TakeHit(new HitContext("F", dir, false, _ownerTransform, powerDamage, true));
        }
    }

    public override void OnExit()
    {
        if (endDamageCoroutine != null) { _coroutineHost.StopCoroutine(endDamageCoroutine); endDamageCoroutine = null; }
        _setCameraEnabled?.Invoke(true);
        if (powerDirector != null) powerDirector.Stop();
    }

    private float GetClipLength(string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return 0f;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == clipName) return clip.length;
        return 0f;
    }
}
