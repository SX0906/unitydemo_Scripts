using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 动画帧事件类型枚举。
/// </summary>
public enum FrameEventType
{
    /// <summary>开启武器碰撞体检测窗口</summary>
    EnableHitbox,
    /// <summary>关闭武器碰撞体检测窗口</summary>
    DisableHitbox,
    /// <summary>触发范围攻击检测</summary>
    AreaAttack,
    /// <summary>触发连击衔接检查</summary>
    ComboCheck,
    /// <summary>播放音效</summary>
    PlaySound,
    /// <summary>生成特效</summary>
    SpawnVFX,
    /// <summary>施加位移（如冲刺）</summary>
    ApplyImpulse,
    /// <summary>动画冻结指定帧数</summary>
    HitStop,
    /// <summary>自定义逻辑（通过 UnityEvent）</summary>
    CustomEvent,
}

/// <summary>
/// 单个帧事件：在动画 normalizedTime 达到时触发。
/// </summary>
[System.Serializable]
public class FrameEvent
{
    [Header("触发时机")]
    [Range(0f, 1f)]
    [Tooltip("动画归一化时间（0=开头，1=结尾），即进度百分比")]
    public float normalizedTime = 0.3f;

    [Header("事件类型")]
    public FrameEventType eventType;

    [Header("参数")]
    [Tooltip("EnableHitbox/DisableHitbox 时的方向标识（如 L, R, F, B）")]
    public string dirTag = "L";

    [Tooltip("伤害值（用于 AreaAttack 等，0 则使用技能配置的伤害）")]
    public float damageOverride;

    [Tooltip("范围攻击半径")]
    public float areaRadius = 3f;

    [Tooltip("Sound / VFX 的名称标识")]
    public string assetName;

    [Tooltip("冲击力向量（本地空间）")]
    public Vector3 impulseForce = Vector3.forward;

    [Header("顿帧")]
    [Tooltip("冻结的帧数（例如 6 = 定格 6 帧后继续播放）")]
    [Min(0)]
    public int hitStopFrames = 6;

    [Header("自定义回调")]
    [Tooltip("CustomEvent 类型时触发")]
    public UnityEvent onCustomEvent;
}

/// <summary>
/// 攻击动画帧事件配置 ScriptableObject。
/// 定义一个动画状态内各帧触发哪些逻辑，
/// 替代手摆 AnimationEvent。
/// </summary>
[CreateAssetMenu(menuName = "Combat/Attack Animation Config", fileName = "AttackAnimConfig")]
public class AttackAnimationConfig : ScriptableObject
{
    [Header("动画标识")]
    [Tooltip("Animator 状态名（不含 Layer 前缀）")]
    public string animationStateName;

    [Tooltip("Animator 层索引")]
    public int animatorLayer;

    [Header("伤害配置")]
    [Tooltip("基础伤害")]
    public float baseDamage = 20f;

    [Header("帧事件列表")]
    [Tooltip("按 normalizedTime 升序排列")]
    public FrameEvent[] frameEvents;

    // === 辅助属性 ===

    /// <summary>是否有任何 EnableHitbox 事件（用于外部查询）</summary>
    public bool HasHitboxEvents
    {
        get
        {
            if (frameEvents == null) return false;
            foreach (var e in frameEvents)
                if (e.eventType == FrameEventType.EnableHitbox)
                    return true;
            return false;
        }
    }

    /// <summary>获取第一个 EnableHitbox 事件的 dirTag</summary>
    public string FirstHitboxDirTag
    {
        get
        {
            if (frameEvents == null) return "L";
            foreach (var e in frameEvents)
                if (e.eventType == FrameEventType.EnableHitbox)
                    return e.dirTag;
            return "L";
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 自动按 normalizedTime 升序排列
        if (frameEvents != null && frameEvents.Length > 1)
            System.Array.Sort(frameEvents, (a, b) => a.normalizedTime.CompareTo(b.normalizedTime));
    }
#endif
}