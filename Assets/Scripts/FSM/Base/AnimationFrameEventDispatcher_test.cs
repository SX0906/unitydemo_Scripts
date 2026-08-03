using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IWeaponHitDetectorView
{
    void OnHitWindowOpen(string dirTag);
    void OnHitWindowClose();
}

public class AnimationFrameEventDispatcher_test : MonoBehaviour
{
    public List<AttackAnimationConfig> configs = new();
    public IWeaponHitDetectorView playerWeaponDetector;
    public IWeaponHitDetectorView enemyWeaponDetector;
    public System.Action OnAreaAttack;
    public System.Action OnAttackComboCheck;

    private Animator _animator;
    private Dictionary<string, AttackAnimationConfig> _configMap;
    private Dictionary<string, HashSet<int>> _firedEvents;

    private void Awake() { _animator = GetComponent<Animator>(); BuildConfigMap(); }

    private void BuildConfigMap()
    {
        _configMap = new Dictionary<string, AttackAnimationConfig>();
        _firedEvents = new Dictionary<string, HashSet<int>>();
        foreach (var cfg in configs)
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.animationStateName)) continue;
            _configMap[cfg.animationStateName] = cfg;
            _firedEvents[cfg.animationStateName] = new HashSet<int>();
        }
    }

    private void Update()
    {
        if (_animator == null || _configMap.Count == 0) return;
        foreach (var kvp in _configMap)
        {
            string stateName = kvp.Key;
            AttackAnimationConfig config = kvp.Value;
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(config.animatorLayer);
            if (!stateInfo.IsName(stateName) && _animator.IsInTransition(config.animatorLayer))
                stateInfo = _animator.GetNextAnimatorStateInfo(config.animatorLayer);
            if (!stateInfo.IsName(stateName)) { if (_firedEvents.TryGetValue(stateName, out var f)) f.Clear(); continue; }
            float nt = stateInfo.normalizedTime % 1f;
            if (!_firedEvents.TryGetValue(stateName, out var fs)) continue;
            if (config.frameEvents == null) continue;
            for (int i = 0; i < config.frameEvents.Length; i++)
            {
                if (fs.Contains(i)) continue;
                if (nt >= config.frameEvents[i].normalizedTime) { fs.Add(i); ExecuteFrameEvent(config.frameEvents[i]); }
            }
        }
    }

    private void ExecuteFrameEvent(FrameEvent fe)
    {
        switch (fe.eventType)
        {
            case FrameEventType.EnableHitbox: playerWeaponDetector?.OnHitWindowOpen(fe.dirTag); enemyWeaponDetector?.OnHitWindowOpen(fe.dirTag); break;
            case FrameEventType.DisableHitbox: playerWeaponDetector?.OnHitWindowClose(); enemyWeaponDetector?.OnHitWindowClose(); break;
            case FrameEventType.AreaAttack: OnAreaAttack?.Invoke(); break;
            case FrameEventType.ComboCheck: OnAttackComboCheck?.Invoke(); break;
            case FrameEventType.PlaySound: if (!string.IsNullOrEmpty(fe.assetName)) { var a = GetComponent<CombatAudioPlayer>(); if (a) a.SendMessage("PlaySoundByName", fe.assetName, SendMessageOptions.DontRequireReceiver); } break;
            case FrameEventType.ApplyImpulse: { var w = transform.TransformDirection(fe.impulseForce); var cc = GetComponent<CharacterController>(); if (cc) cc.Move(w * Time.deltaTime); } break;
            case FrameEventType.CustomEvent: fe.onCustomEvent?.Invoke(); break;
        }
    }

    public void ResetFiredEvents(string stateName) { if (_firedEvents.TryGetValue(stateName, out var fired)) fired?.Clear(); }
}
