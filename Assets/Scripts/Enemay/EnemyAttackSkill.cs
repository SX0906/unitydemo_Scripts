using UnityEngine;

[CreateAssetMenu(menuName = "Game/Enemy/Attack Skill", fileName = "NewEnemyAttackSkill")]
public class EnemyAttackSkill : ScriptableObject
{
    [Header("技能 ID")]
    public string atkskillID = "enemy_skill_001";

    [Header("技能第一段状态名称")]
    public string atkskillName = "AtkSkill_01";

    [Header("连击配置")]
    public EnemyComboData[] comboData;

    [Header("技能使用距离")]
    public float atkskillUseDistance = 3f;

    [Header("技能冷却时间")]
    public float atkskillCoolTime = 5f;

    [Header("技能是否完成")]
    public bool atkskilldone = true;

    [Header("Animator 层级")]
    public int animatorLayer = 0;

    [Header("技能状态 Tag")]
    public string attackStateTag = "Skill";

    [Header("最后一段播放到多少时间后结束")]
    [Range(0f, 1f)]
    public float finishNormalizedTime = 0.98f;

    [Header("Debug")]
    public bool debugLog = true;

    [Header("技能霸体 / 格挡属性")]
    public bool isSuperArmor = false;
    public bool canBeBlock = false;

    [Header("AOE 范围伤害设置")]
    public bool isAOESkill;
    public float aoeRadius = 3f;
    public Vector3 aoeCenterOffset = new Vector3(0, 0, 1.5f);
    public LayerMask aoeTargetMask;
    
    [Header("AOE 延迟伤害设置")]
    public int aoeHitCount = 1;
    public float aoeHitInterval = 0.2f;
    public bool aoeIgnoreRepeatHit = true;
    
    [Header("突进矩形伤害配置")]
    public bool isDashSkill = false;
    public float dashWidth = 1.5f;
    public float dashLength = 4f;
    public float dashHeight = 2f;
    public Vector3 dashOffset = Vector3.zero;
    public LayerMask dashTargetMask;
    public int dashHitCount = 1;
    public float dashHitInterval = 0.2f;
    public bool dashIgnoreRepeatHit = true;

    [Header("Power 技能多次判定设置")]
    public bool isPowerSkill;
    public int powerHitCount = 3;
    public float powerHitInterval = 0.15f;

    public string GetStateName(int index)
    {
        if (comboData != null && index >= 0 && index < comboData.Length)
            return comboData[index].stateName;
        return index == 0 ? atkskillName : string.Empty;
    }

    public float GetDamage(int comboIndex)
    {
        if (comboData != null && comboIndex >= 0 && comboIndex < comboData.Length)
            return comboData[comboIndex].damage;
        return 10f;
    }

    public bool IsDashCombo(int comboIndex)
    {
        if (comboData != null && comboIndex >= 0 && comboIndex < comboData.Length)
            return comboData[comboIndex].isDash;
        return false;
    }

    public DashData GetDashData(int comboIndex)
    {
        if (comboData != null && comboIndex >= 0 && comboIndex < comboData.Length)
            return comboData[comboIndex].dashData;
        return null;
    }

    public int ComboCount
    {
        get
        {
            if (comboData != null && comboData.Length > 0)
                return comboData.Length;
            return string.IsNullOrEmpty(atkskillName) ? 0 : 1;
        }
    }

    public string FirstStateName => GetStateName(0);

    public string LastStateName
    {
        get
        {
            int count = ComboCount;
            return count <= 0 ? string.Empty : GetStateName(count - 1);
        }
    }
}

[System.Serializable]
public class EnemyComboData
{
    [Header("状态名称")]
    public string stateName;

    [Header("伤害")]
    public float damage = 10f;

    [Header("是否为突进")]
    public bool isDash;

    [Header("突进配置（仅当isDash=true时有效）")]
    public DashData dashData = new DashData();
}