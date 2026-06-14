using UnityEngine;

public class EnemyAttackState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private EnemyFSM enemyFSM;
    private Transform transform;
    private Transform attacker;

    // === 技能驱动 ===
    private EnemySkillData currentSkill;
    private int comboIndex;
    private string currentAnimStateName;

    public EnemyAttackState(Animator animator, EnemyFSMControl fsm,
        EnemyFSM enemyFSM, Transform transform)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM = enemyFSM;
        this.transform = transform;
    }

    /// <summary>进入攻击前由外部（BT Action）设置目标和技能</summary>
    public void SetAttackerAndSkill(Transform attackerTransform, EnemySkillData skill)
    {
        attacker = attackerTransform;
        currentSkill = skill;
    }

    public override void OnEnter()
    {
        if (currentSkill == null || currentSkill.ComboCount == 0)
        {
            // 没有有效技能直接退回
            fsm.SetState(EnemyStateType.IDLE);
            return;
        }

        comboIndex = 0;
        currentAnimStateName = currentSkill.FirstStateName;
        FaceAttacker();
        enemyFSM.SetEnemyWeaponDamage(currentSkill.GetDamage(0));
        animator.CrossFadeInFixedTime(currentAnimStateName, 0.02f);
    }

    public override void OnUpdate()
    {
        if (currentSkill == null)
        {
            fsm.SetState(EnemyStateType.IDLE);
            return;
        }

        FaceAttacker();

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(currentSkill.animatorLayer);
        bool isLastState = stateInfo.IsName(currentSkill.LastStateName);

        // 最后一段播完 → 技能结束
        if (isLastState && stateInfo.normalizedTime >= currentSkill.finishNormalizedTime)
        {
            var skillMgr = enemyFSM.GetComponent<EnemySkillManager>();
            Debug.Log("正常退出");
            skillMgr?.FinishCast();
            fsm.SetState(EnemyStateType.IDLE);
            return;
        }

        // 安全兜底：动画意外跑到非技能状态
        if (stateInfo.IsTag(currentSkill.stateTag)&&!isLastState && stateInfo.normalizedTime >= 0.95f && !IsInSkillStates(stateInfo))
        {
            var skillMgr = enemyFSM.GetComponent<EnemySkillManager>();
            Debug.Log("出现意外退出");
            skillMgr?.FinishCast();
            fsm.SetState(EnemyStateType.IDLE);
        }
    }

    public override void OnExit()
    {
        enemyFSM.OnEnemyHitWindowClose();
        // 异常中断（如受击）也确保冷却写入
        if (currentSkill != null)
        {
            var skillMgr = enemyFSM.GetComponent<EnemySkillManager>();
            skillMgr?.FinishCast();
        }
        attacker = null;
        currentSkill = null;
        comboIndex = 0;
        currentAnimStateName = null;
    }

    /// <summary>
    /// 动画事件：每段挥刀命中帧触发。
    /// 有下一段 + 距离够 → 衔接连击；否则 → 提前结束动画退回 IDLE。
    /// </summary>
    public void OnAttackComboCheck()
    {
        if (currentSkill == null) return;

        comboIndex++;
        bool hasNextCombo = comboIndex < currentSkill.ComboCount;
        bool inRange = attacker != null
            && Vector3.Distance(transform.position, attacker.position) <= currentSkill.useDistance * 1.3f;

        if (hasNextCombo && inRange)
        {
            // 衔接下一段
            currentAnimStateName = currentSkill.GetStateName(comboIndex);
            enemyFSM.SetEnemyWeaponDamage(currentSkill.GetDamage(comboIndex));
            animator.CrossFadeInFixedTime(currentAnimStateName, 0.02f);
        }
        else
        {
            // ★ 没有可接的段 → 立刻结束技能，不等收招动画播完
            var skillMgr = enemyFSM.GetComponent<EnemySkillManager>();
            skillMgr?.FinishCast();
            fsm.SetState(EnemyStateType.IDLE);
        }
    }

    private bool IsInSkillStates(AnimatorStateInfo state)
    {
        if (currentSkill == null) return false;
        for (int i = 0; i < currentSkill.ComboCount; i++)
        {
            if (state.IsName(currentSkill.GetStateName(i)))
                return true;
        }
        return false;
    }

    private void FaceAttacker()
    {
        if (attacker == null) return;
        Vector3 dir = attacker.position - transform.position;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }
    }
}