using UnityEngine;

public class PlayerSkillBarUI : MonoBehaviour
{
    [Header("技能槽UI数组")]
    public PlayerSkillSlotUI[] skillSlots;
    
    [Header("按键配置（对应技能槽）")]
    public char[] skillKeys = { '1', '2', '3', '4', '5' };
    
    [Header("引用")]
    public PlayerSkillPlayer skillPlayer;
    
    private void Start()
    {
        if (skillPlayer == null)
        {
            skillPlayer = GetComponentInParent<PlayerSkillPlayer>();
        }
        
        if (skillPlayer != null)
        {
            InitializeSkillBar();
        }
    }
    
    private void InitializeSkillBar()
    {
        if (skillSlots == null || skillSlots.Length == 0)
            return;
        
        int skillCount = skillPlayer.skills != null ? skillPlayer.skills.Length : 0;
        int slotCount = skillSlots.Length;
        
        for (int i = 0; i < slotCount; i++)
        {
            if (skillSlots[i] == null)
                continue;
            
            PlayerSkillData skill = (i < skillCount) ? skillPlayer.skills[i] : null;
            char key = (i < skillKeys.Length) ? skillKeys[i] : '\0';
            
            skillSlots[i].Initialize(skillPlayer, i, skill, key);
        }
    }
}
