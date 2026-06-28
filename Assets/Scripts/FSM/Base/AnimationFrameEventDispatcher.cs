using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 动画帧事件分发器。
/// 挂在角色上（与 Animator 同物体），
/// 读取 AttackAnimationConfig 列表，
/// 在动画播放到指定 normalizedTime 时自动触发对应逻辑。
/// 替代传统的 AnimationEvent。
/// </summary>
public class AnimationFrameEventDispatcher : MonoBehaviour
{
    [Header("帧事件配置")]
    [Tooltip("将所有攻击动画的配置资产拖入此列表")]
    public List<AttackAnimationConfig> configs = new();

    [Header("依赖引用")]
    [Tooltip("玩家武器碰撞体检测器（可选，EnableHitbox/DisableHitbox 需要）")]
    public WeaponHitDetector playerWeaponDetector;
    [Tooltip("敌人武器碰撞体检测器（可选）")]
    public EnemyWeaponHitDetector enemyWeaponDetector;

    // --- 内部状态 ---
    private Animator _animator;
    private Dictionary<string, AttackAnimationConfig> _configMap;
    private Dictionary<string, HashSet<int>> _firedEvents; // stateName -> 已触发的 eventIndex 集合
    private int _hitStopCount; // 帧冻结引用计数，防止多个 HitStop 叠加后提前恢复

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        BuildConfigMap();
    }

    private void BuildConfigMap()
    {
        _configMap = new Dictionary<string, AttackAnimationConfig>();
        _firedEvents = new Dictionary<string, HashSet<int>>();

        foreach (var cfg in configs)
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.animationStateName)) continue;

            string key = cfg.animationStateName;
            if (!_configMap.ContainsKey(key))
            {
                _configMap[key] = cfg;
                _firedEvents[key] = new HashSet<int>();
            }
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

            bool isInTargetState = stateInfo.IsName(stateName);
            if (!isInTargetState && _animator.IsInTransition(config.animatorLayer))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(config.animatorLayer);
                isInTargetState = next.IsName(stateName);
                if (isInTargetState)
                    stateInfo = next;
            }

            if (!isInTargetState)
            {
                if (_firedEvents.TryGetValue(stateName, out var fired))
                    fired.Clear();
                continue;
            }

            float normTime = stateInfo.normalizedTime % 1f;
            if (normTime < 0f) normTime += 1f;

            if (!_firedEvents.TryGetValue(stateName, out var firedSet))
                continue;
            if (config.frameEvents == null) continue;

            for (int i = 0; i < config.frameEvents.Length; i++)
            {
                if (firedSet.Contains(i)) continue;

                FrameEvent fe = config.frameEvents[i];
                if (normTime >= fe.normalizedTime)
                {
                    firedSet.Add(i);
                    ExecuteFrameEvent(fe, config);
                }
            }
        }
    }

    /// <summary>执行单个帧事件</summary>
    private void ExecuteFrameEvent(FrameEvent fe, AttackAnimationConfig config)
    {
        switch (fe.eventType)
        {
            case FrameEventType.EnableHitbox:
                if (playerWeaponDetector != null)
                    playerWeaponDetector.OnHitWindowOpen(fe.dirTag);
                if (enemyWeaponDetector != null)
                    enemyWeaponDetector.OnEnemyHitWindowOpen();
                break;

            case FrameEventType.DisableHitbox:
                if (playerWeaponDetector != null)
                    playerWeaponDetector.OnHitWindowClose();
                if (enemyWeaponDetector != null)
                    enemyWeaponDetector.OnEnemyHitWindowClose();
                break;

            case FrameEventType.AreaAttack:
                SendMessage("OnAreaAttack", SendMessageOptions.DontRequireReceiver);
                break;

            case FrameEventType.ComboCheck:
                SendMessage("OnAttackComboCheck", SendMessageOptions.DontRequireReceiver);
                break;

            case FrameEventType.PlaySound:
                if (!string.IsNullOrEmpty(fe.assetName))
                {
                    var audio = GetComponent<CombatAudioPlayer>();
                    if (audio != null)
                        audio.SendMessage("PlaySoundByName", fe.assetName,
                            SendMessageOptions.DontRequireReceiver);
                }
                break;

            case FrameEventType.SpawnVFX:
                Debug.Log($"[FrameEventDispatcher] SpawnVFX: {fe.assetName}");
                break;

            case FrameEventType.ApplyImpulse:
                {
                    Vector3 worldImpulse = transform.TransformDirection(fe.impulseForce);
                    var cc = GetComponent<CharacterController>();
                    if (cc != null && cc.enabled)
                        cc.Move(worldImpulse * Time.deltaTime);
                }
                break;

            case FrameEventType.HitStop:
                if (fe.hitStopFrames > 0)
                    StartCoroutine(HitStopFramesRoutine(fe.hitStopFrames));
                break;

            case FrameEventType.CustomEvent:
                fe.onCustomEvent?.Invoke();
                break;
        }
    }

    /// <summary>
    /// 顿帧协程：冻结动画指定帧数后恢复。
    /// 不改 Time.timeScale，只暂停 Animator 播放。
    /// 冻结期间 normalizedTime 不推进，后续帧事件不会漏。
    /// </summary>
    private IEnumerator HitStopFramesRoutine(int frameCount)
    {
        if (_animator != null)
            _animator.speed = 0f;
        _hitStopCount++;

        for (int i = 0; i < frameCount; i++)
            yield return null;

        if (_animator != null)
        {
            _hitStopCount--;
            if (_hitStopCount <= 0)
            {
                _hitStopCount = 0;
            _animator.speed = 1f;
            }
        }
    }

    // === 公开方法：外部可强制重置触发记录 ===

    public void ResetFiredEvents(string stateName)
    {
        if (_firedEvents.TryGetValue(stateName, out var fired))
            fired.Clear();
    }

    public void ResetAllFiredEvents()
    {
        foreach (var kvp in _firedEvents)
            kvp.Value.Clear();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        foreach (var cfg in configs)
        {
            if (cfg != null && cfg.frameEvents != null && cfg.frameEvents.Length > 1)
                System.Array.Sort(cfg.frameEvents, (a, b) => a.normalizedTime.CompareTo(b.normalizedTime));
        }
    }
#endif
}
