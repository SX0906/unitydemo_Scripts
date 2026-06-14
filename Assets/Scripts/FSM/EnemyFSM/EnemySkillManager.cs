using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人技能管理器——挂在 Enemy 上，管技能池 + 冷却 + 距离判断。
/// 纯数据层，不参与动画/状态切换，由 BT Action 或 AttackState 调用。
/// </summary>
public class EnemySkillManager : MonoBehaviour
{
    [Header("技能池（Inspector 里把 .asset 拖进来）")]
    public List<EnemySkillData> skillPool = new();

    /// <summary>冷却结束时间字典，key = skillID</summary>
    private readonly Dictionary<string, float> cooldownEndTimes = new();

    /// <summary>当前正在释放的技能（null = 没在放）</summary>
    public EnemySkillData CurrentSkill { get; private set; }

    /// <summary>当前攻击目标</summary>
    public Transform CurrentTarget { get; private set; }

    /// <summary>是否正在施放技能中</summary>
    public bool IsCasting => CurrentSkill != null;

    /// <summary>
    /// 从技能池中返回一个"冷却好 + 在距离内 + isAvailable"的技能，随机选。
    /// 返回 null 表示此刻没有能放的技能。
    /// </summary>
    public EnemySkillData GetAvailableSkill(Transform self, Transform target)
    {
        if (target == null || skillPool == null) return null;

        var available = new List<EnemySkillData>();
        foreach (var s in skillPool)
        {
            if (IsSkillReady(s, self, target))
                available.Add(s);
        }
        return available.Count > 0
            ? available[Random.Range(0, available.Count)]
            : null;
    }

    /// <summary>
    /// 指定技能此刻是否可用
    /// </summary>
    public bool IsSkillReady(EnemySkillData skill, Transform self, Transform target)
    {
        if (skill == null || !skill.isAvailable || target == null) return false;

        if (cooldownEndTimes.TryGetValue(skill.skillID, out float end) && Time.time < end)
            return false;

        float dist = Vector3.Distance(self.position, target.position);
        return dist <= skill.useDistance;
    }

    /// <summary>是否有任意技能立即可放</summary>
    public bool HasAvailableSkill(Transform self, Transform target)
    {
        return GetAvailableSkill(self, target) != null;
    }

    /// <summary>
    /// 开始施放——仅记录当前技能和目标，不做动画。
    /// </summary>
    public void StartCast(EnemySkillData skill, Transform target)
    {
        CurrentSkill = skill;
        CurrentTarget = target;
    }

    /// <summary>
    /// 结束施放——写入冷却。由 AttackState.OnExit / OnUpdate 完结时调用。
    /// </summary>
    public void FinishCast()
    {
        if (CurrentSkill != null)
            cooldownEndTimes[CurrentSkill.skillID] = Time.time + CurrentSkill.coolDown;

        CurrentSkill = null;
        CurrentTarget = null;
    }

    /// <summary>查询技能剩余冷却秒数</summary>
    public float GetCooldownRemain(EnemySkillData skill)
    {
        if (skill == null) return 0f;
        if (cooldownEndTimes.TryGetValue(skill.skillID, out float end))
            return Mathf.Max(0f, end - Time.time);
        return 0f;
    }
}