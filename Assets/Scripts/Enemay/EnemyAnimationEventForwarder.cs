using UnityEngine;

public class EnemyAnimationEventForwarder : MonoBehaviour
{
    // 只保留技能脚本（CombatController已删除攻击事件，无需引用）
    public EnemyAttackSkillPlayer skillPlayer;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Reset()
    {
        AutoFindReferences();
    }

    // 仅自动查找技能脚本
    private void AutoFindReferences()
    {
        if (skillPlayer == null)
            skillPlayer = GetComponentInParent<EnemyAttackSkillPlayer>();
    }

    // 仅转发给技能脚本
    public void OnAnimationAttackEvent(string hitStateName)
    {
        skillPlayer?.OnAnimationAttackEvent(hitStateName);
    }

    public void OnAnimationAttackEndEvent()
    {
        skillPlayer?.OnAnimationAttackEndEvent();
    }
}