using UnityEngine;

public class EnemyAttackState_test : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private EnemyFSMBT_test.IEnemyFsmAccess fsmAccess;
    private Transform transform;
    private Transform attacker;
    private ICombatant selfCombatant;
    private EnemySkillData currentSkill;
    private int comboIndex;
    private int currentStepIndex;
    private string currentAnimStateName;

    public EnemyAttackState_test(Animator animator, EnemyFSMControl fsm, EnemyFSMBT_test.IEnemyFsmAccess fsmAccess, Transform transform, ICombatant selfCombatant)
    {
        this.animator = animator; this.fsm = fsm; this.fsmAccess = fsmAccess;
        this.transform = transform; this.selfCombatant = selfCombatant;
    }

    public void SetAttackerAndSkill(Transform attackerTransform, EnemySkillData skill)
    {
        attacker = attackerTransform; currentSkill = skill;
    }

    public override void OnEnter()
    {
        if (currentSkill == null || currentSkill.ComboCount == 0) { fsm.SetState(EnemyStateType.IDLE); return; }
        comboIndex = 0; currentStepIndex = 0; currentAnimStateName = currentSkill.FirstStateName;
        FaceAttacker();
        fsmAccess.SetWeaponDamage(currentSkill.GetDamage(0));
        animator.CrossFadeInFixedTime(currentAnimStateName, 0.02f);
    }

    public override void OnUpdate()
    {
        if (currentSkill == null) { fsm.SetState(EnemyStateType.IDLE); return; }
        FaceAttacker();
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(currentSkill.animatorLayer);
        bool isLastState = stateInfo.IsName(currentSkill.LastStateName);
        if (isLastState && stateInfo.normalizedTime >= currentSkill.finishNormalizedTime)
        {
            var skillMgr = (fsmAccess as MonoBehaviour)?.GetComponent<EnemySkillManager_test>();
            skillMgr?.FinishCast(); fsm.SetState(EnemyStateType.IDLE); return;
        }
        if (stateInfo.IsTag(currentSkill.stateTag) && !isLastState && stateInfo.normalizedTime >= 0.95f && !IsInSkillStates(stateInfo))
        {
            var skillMgr = (fsmAccess as MonoBehaviour)?.GetComponent<EnemySkillManager_test>();
            skillMgr?.FinishCast(); fsm.SetState(EnemyStateType.IDLE);
        }
    }

    public override void OnExit()
    {
        if (currentSkill != null) { var skillMgr = (fsmAccess as MonoBehaviour)?.GetComponent<EnemySkillManager_test>(); skillMgr?.FinishCast(); }
        attacker = null; currentSkill = null; comboIndex = 0; currentStepIndex = 0; currentAnimStateName = null;
    }

    public void OnAttackComboCheck()
    {
        if (currentSkill == null) return;
        comboIndex++;
        bool hasNextCombo = comboIndex < currentSkill.ComboCount;
        bool inRange = attacker != null && Vector3.Distance(transform.position, attacker.position) <= currentSkill.useDistance * 1.3f;
        if (hasNextCombo && inRange)
        {
            currentAnimStateName = currentSkill.GetStateName(comboIndex); currentStepIndex = comboIndex;
            fsmAccess.SetWeaponDamage(currentSkill.GetDamage(comboIndex));
            animator.CrossFadeInFixedTime(currentAnimStateName, 0.02f);
        }
        else
        {
            var skillMgr = (fsmAccess as MonoBehaviour)?.GetComponent<EnemySkillManager_test>();
            skillMgr?.FinishCast(); fsm.SetState(EnemyStateType.IDLE);
        }
    }

    public void OnAreaAttack()
    {
        if (currentSkill == null || currentStepIndex < 0 || currentStepIndex >= currentSkill.ComboCount) return;
        SkillComboStep step = currentSkill.comboSteps[currentStepIndex];
        if (!step.isAreaAttack) return;
        float damage = step.areaDamage > 0f ? step.areaDamage : currentSkill.GetDamage(currentStepIndex);
        if (selfCombatant?.Vitals is EnemyVitals ev && ev.RagePercent >= 1f) damage *= 1.05f;
        Collider[] hits = Physics.OverlapSphere(transform.position, step.areaRadius, fsmAccess.PlayerLayer);
        foreach (Collider hit in hits)
        {
            ICombatTarget target = hit.GetComponentInParent<ICombatTarget>();
            if (target != null)
                target.TakeHit(new HitContext("F", (target.Transform.position - transform.position).normalized, false, transform, damage, false));
        }
    }

    private bool IsInSkillStates(AnimatorStateInfo state)
    {
        if (currentSkill == null) return false;
        for (int i = 0; i < currentSkill.ComboCount; i++)
            if (state.IsName(currentSkill.GetStateName(i))) return true;
        return false;
    }

    private void FaceAttacker()
    {
        if (attacker == null) return;
        Vector3 dir = attacker.position - transform.position; dir.y = 0;
        if (dir != Vector3.zero) { Quaternion targetRot = Quaternion.LookRotation(dir); transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime); }
    }
}
