using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerSkill", menuName = "Player Skill/Skill Data", order = 1)]
public class PlayerSkillData : ScriptableObject
{
    [Header("技能基础信息")]
    public string skillName = "PlayerSkill-01";
    public string skillID = "skill_001";
    public Sprite skillIcon;
    public float coolDown = 5f;
    
    [Header("Animator状态配置")]
    public string skillStateName = "SkillAttack";
    public int animatorLayer = 0;
    public string skillStateTag = "Skill";
    [Range(0f, 1f)]
    public float finishNormalizedTime = 0.98f;
    
    [Header("连击配置")]
    public ComboData[] comboData;
    
    [Header("技能特性")]
    public bool isSuperArmor = false;
    public bool canBeBlock = false;
    public bool interruptNormalAttack = false;
    
    [Header("AOE伤害配置")]
    public bool isAOESkill = false;
    public float aoeRadius = 3f;
    public Vector3 aoeCenterOffset = new Vector3(0, 0, 1.5f);
    public LayerMask aoeTargetMask;
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
    
    [Header("多段伤害设置")]
    public bool isMultiHitSkill = false;
    public int multiHitCount = 3;
    public float multiHitInterval = 0.15f;
    
    [Header("受击顿帧配置")]
    public float hitStopDuration = 0.12f;
    public float hitStopTimeScale = 0.2f;
    
    [Header("动画RootMotion")]
    public bool useRootMotion = true;
    
    // 新增：Root Motion 缩放
    [Header("Root Motion 缩放")]
    [Range(0f, 2f)]
    public float rootMotionScale = 1f;
    [Range(0f, 2f)]
    public float[] comboRootMotionScale;
    
    public string GetStateName(int index)
    {
        if (comboData != null && index >= 0 && index < comboData.Length)
            return comboData[index].stateName;
        return index == 0 ? skillStateName : string.Empty;
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
            return string.IsNullOrEmpty(skillStateName) ? 0 : 1;
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
    
    // 新增方法
    public float GetRootMotionScale(int comboIndex)
    {
        if (comboRootMotionScale != null && comboIndex >= 0 && comboIndex < comboRootMotionScale.Length)
            return comboRootMotionScale[comboIndex];
        return rootMotionScale;
    }
}

[System.Serializable]
public class ComboData
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
