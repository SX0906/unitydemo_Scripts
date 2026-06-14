using UnityEngine;

/// <summary>
/// 连击段数据：一段动画 + 可在此动画内触发多次伤害判定
/// </summary>
[System.Serializable]
public class SkillComboStep
{
    [Header("动画状态名")]
    public string stateName;

    [Header("单次伤害值")]
    public float damage = 10f;

    [Header("该段动画内触发几次伤害")]
    public int hitCount = 1;

    [Header("多段伤害时的间隔")]
    public float hitInterval = 0.2f;
}

/// <summary>
/// 敌人技能 ScriptableObject
/// </summary>
[CreateAssetMenu(menuName = "FSM/Enemy Skill Data", fileName = "NewEnemySkill")]
public class EnemySkillData : ScriptableObject
{
    [Header("技能标识")]
    public string skillID = "skill_001";

    [Header("技能名称")]
    public string skillName = "普攻一";

    [Header("连击段配置")]
    public SkillComboStep[] comboSteps;

    [Header("释放距离")]
    public float useDistance = 2f;

    [Header("冷却时间")]
    public float coolDown = 3f;

    [Header("是否可用")]
    public bool isAvailable = true;

    [Header("Animator 层级")]
    public int animatorLayer = 0;

    [Header("动画状态 Tag")]
    public string stateTag = "Attack";

    [Header("最后一段归一化结束时间")]
    [Range(0.5f, 1f)]
    public float finishNormalizedTime = 0.8f;

    // === 便捷属性 ===

    public int ComboCount => comboSteps != null ? comboSteps.Length : 0;

    public string FirstStateName => ComboCount > 0 ? comboSteps[0].stateName : string.Empty;
    public string LastStateName => ComboCount > 0 ? comboSteps[ComboCount - 1].stateName : string.Empty;

    public string GetStateName(int index)
    {
        if (comboSteps != null && index >= 0 && index < comboSteps.Length)
            return comboSteps[index].stateName;
        return string.Empty;
    }

    public float GetDamage(int index)
    {
        if (comboSteps != null && index >= 0 && index < comboSteps.Length)
            return comboSteps[index].damage;
        return 10f;
    }

    // ★ 新增：某段动画的伤害判定次数
    public int GetHitCount(int index)
    {
        if (comboSteps != null && index >= 0 && index < comboSteps.Length)
            return Mathf.Max(1, comboSteps[index].hitCount);
        return 1;
    }

    // ★ 新增：多段伤害时的间隔
    public float GetHitInterval(int index)
    {
        if (comboSteps != null && index >= 0 && index < comboSteps.Length)
            return Mathf.Max(0f, comboSteps[index].hitInterval);
        return 0f;
    }
}