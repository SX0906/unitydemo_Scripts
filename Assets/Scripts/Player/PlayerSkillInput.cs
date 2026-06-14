using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class PlayerSkillKeyBinding
{
    public Key key;
    public string displayName;
}

[RequireComponent(typeof(PlayerSkillPlayer))]
public class PlayerSkillInput : MonoBehaviour
{
    [Header("技能按键绑定")]
    public PlayerSkillKeyBinding[] skillKeys = new PlayerSkillKeyBinding[5]
    {
        new PlayerSkillKeyBinding { key = Key.Digit1, displayName = "技能1" },
        new PlayerSkillKeyBinding { key = Key.Digit2, displayName = "技能2" },
        new PlayerSkillKeyBinding { key = Key.Digit3, displayName = "技能3" },
        new PlayerSkillKeyBinding { key = Key.Digit4, displayName = "技能4" },
        new PlayerSkillKeyBinding { key = Key.Digit5, displayName = "技能5" }
    };

    [Header("允许在普通攻击中释放技能")]
    public bool allowSkillDuringAttack = true;

    private PlayerSkillPlayer skillPlayer;
    private PlayerCombatController combatController;

    private void Awake()
    {
        skillPlayer = GetComponent<PlayerSkillPlayer>();
        combatController = GetComponent<PlayerCombatController>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        for (int i = 0; i < Mathf.Min(skillKeys.Length, skillPlayer.skills.Length); i++)
        {
            if (Keyboard.current[skillKeys[i].key].wasPressedThisFrame)
            {
                TryPlaySkill(i);
            }
        }
    }

    private void TryPlaySkill(int skillIndex)
    {
        if (skillPlayer == null || skillPlayer.skills == null)
            return;

        if (skillIndex < 0 || skillIndex >= skillPlayer.skills.Length)
            return;

        PlayerSkillData skill = skillPlayer.skills[skillIndex];
        if (skill == null)
            return;

        if (allowSkillDuringAttack && skill.interruptNormalAttack)
        {
            if (combatController != null && combatController.IsCurrentlyAttacking())
            {
                combatController.CancelAttack();
            }
        }

        skillPlayer.TryPlaySkill(skillIndex);
    }
}
