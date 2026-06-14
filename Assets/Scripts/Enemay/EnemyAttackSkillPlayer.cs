using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class EnemySkillPostConfig
{
    public float targetKeepTime = 3f;
    public bool smoothFaceTargetOnFinish = true;
    public float faceTargetSmoothSpeed = 3f;
}

[System.Serializable]
public class EnemySkillHitStopConfig
{
    [Header("受击顿帧")]
    public float hitStopDuration = 0.12f;
    public float hitStopTimeScale = 0.2f;
}

public class EnemyAttackSkillPlayer : MonoBehaviour
{
    [Header("所有技能")]
    public List<EnemyAttackSkill> allSkills = new();
    public EnemyAttackSkill defaultSkill;

    [Header("当前可用的技能池")]
    public List<EnemyAttackSkill> availableSkillPool = new();

    [Header("准备释放但不在距离的技能池")]
    public List<EnemyAttackSkill> approachSkillPool = new();

    public bool useCombatControllerTarget = true;
    public bool disableCombatControllerWhileSkill = true;
    public bool finishWhenExitSkillState = true;
    public float enterSkillStateGraceTime = 0.15f;

    [Header("技能后期")]
    public EnemySkillPostConfig postSkillConfig = new();
    [Header("受击顿帧")]
    public EnemySkillHitStopConfig hitStopConfig = new();
    [Header("武器 Hitbox")]
    public WeaponHitbox weaponHitbox;

    private ActorBase actor;
    private Animator animator;
    private NavMeshAgent navAgent;
    private CharacterController characterController;
    private EnemyCombatController combatController;
    private EnemySkillRootMotionRelay rootMotionRelay;

    private EnemyAttackSkill currentSkill;
    private Transform currentSkillTarget;
    private int currentComboIndex;

    private bool isUsingSkill;
    private bool combatControllerWasEnabled;
    private bool hasEnteredAnySkillState;
    private bool hasEnteredLastSkillState;
    private bool previousApplyRootMotion;
    private bool previousAgentUpdatePosition;
    private bool previousAgentUpdateRotation;
    private bool isPostSkillFacing;

    private float postSkillFaceEndTime;
    private float skillStartTime;

    private readonly Dictionary<string, float> skillCooldownEndTimes = new();
    private readonly HashSet<object> aoeHitTargets = new();
    private Coroutine aoeDamageCoroutine;
    private Coroutine powerHitCoroutine;

    private Coroutine hitStopCoroutine;
    private Coroutine dashDamageCoroutine;
    private bool isHitStopping;

    public bool IsUsingSkill => isUsingSkill;
    public bool UseSkillRootMotion => isUsingSkill;
    public EnemyAttackSkill CurrentSkill => currentSkill;
    public Transform CurrentSkillTarget => currentSkillTarget;
    public bool IsSuperArmorActive => isUsingSkill && currentSkill != null && currentSkill.isSuperArmor;
    public bool CanBlockActive => isUsingSkill && currentSkill != null && currentSkill.canBeBlock;

    private void Awake()
    {
        actor = GetComponent<ActorBase>();
        animator = GetComponentInChildren<Animator>();
        navAgent = GetComponent<NavMeshAgent>();
        characterController = GetComponent<CharacterController>();
        combatController = GetComponent<EnemyCombatController>();

        SetupRootMotionRelay();

        if (weaponHitbox != null)
            weaponHitbox.OnHitEnemy += OnHitEnemy;
    }

    private void Start()
    {
        RefreshSkillPool();
    }

    private void Update()
    {
        if (isPostSkillFacing)
            UpdatePostSkillFacing();

        if (!isUsingSkill)
        {
            RefreshSkillPool();
            return;
        }

        if (actor != null && actor.actorState.isHit)
        {
            if (currentSkill != null && currentSkill.isSuperArmor)
            {
                TickSkill();
                return;
            }

            CancelSkill(true);
            return;
        }

        TickSkill();
    }

    private void OnDisable()
    {
        if (isUsingSkill)
            CancelSkill(false);

        isPostSkillFacing = false;
    }

    private void OnDestroy()
    {
        if (weaponHitbox != null)
            weaponHitbox.OnHitEnemy -= OnHitEnemy;
        
        StopAllCoroutines();
    }

    private void SetupRootMotionRelay()
    {
        if (animator == null)
            return;

        rootMotionRelay = animator.GetComponent<EnemySkillRootMotionRelay>();

        if (rootMotionRelay == null)
            rootMotionRelay = animator.gameObject.AddComponent<EnemySkillRootMotionRelay>();

        rootMotionRelay.Initialize(this, animator);
    }

    public bool TryUseDefaultSkill()
    {
        return TryUseSkill(defaultSkill);
    }

    public bool TryUseRandomAvailableSkill()
    {
        RefreshSkillPool();

        return availableSkillPool.Count > 0 &&
               TryUseSkill(availableSkillPool[Random.Range(0, availableSkillPool.Count)]);
    }

    public bool TryUseSkill(EnemyAttackSkill skill)
    {
        return CanUseSkill(skill) && StartSkill(skill);
    }

    public bool HasAvailableSkillNow()
    {
        RefreshSkillPool();
        return availableSkillPool.Count > 0;
    }

    public bool HasApproachSkillNow()
    {
        RefreshSkillPool();
        return approachSkillPool.Count > 0;
    }

    public bool CanUseSkill(EnemyAttackSkill skill)
    {
        return CanUseSkillIgnoringDistance(skill) && IsTargetInSkillDistance(skill);
    }

    public bool CanUseSkillIgnoringDistance(EnemyAttackSkill skill)
    {
        return skill != null &&
               animator != null &&
               !isUsingSkill &&
               (actor == null || !actor.actorState.isHit) &&
               skill.atkskilldone &&
               skill.ComboCount > 0 &&
               !IsSkillCoolingDown(skill) &&
               GetTarget() != null;
    }

    public bool IsTargetInSkillDistance(EnemyAttackSkill skill)
    {
        Transform target = GetTarget();

        return target != null &&
               skill != null &&
               (target.position - transform.position).sqrMagnitude <=
               skill.atkskillUseDistance * skill.atkskillUseDistance;
    }

    public bool TryGetApproachSkill(out EnemyAttackSkill approachSkill)
    {
        RefreshSkillPool();

        approachSkill = null;

        if (approachSkillPool.Count <= 0)
            return false;

        float bestDistance = -1f;

        foreach (var skill in approachSkillPool)
        {
            if (skill == null)
                continue;

            float useDistance = Mathf.Max(0.05f, skill.atkskillUseDistance);

            if (useDistance > bestDistance)
            {
                bestDistance = useDistance;
                approachSkill = skill;
            }
        }

        return approachSkill != null;
    }

    private void RefreshSkillPool()
    {
        availableSkillPool.Clear();
        approachSkillPool.Clear();

        foreach (var skill in allSkills)
        {
            if (CanUseSkill(skill))
            {
                availableSkillPool.Add(skill);
            }
            else if (CanUseSkillIgnoringDistance(skill))
            {
                approachSkillPool.Add(skill);
            }
        }
    }

    private Transform GetTarget()
    {
        return useCombatControllerTarget && combatController != null
            ? combatController.Target
            : null;
    }

    private bool StartSkill(EnemyAttackSkill skill)
    {
        currentSkill = skill;
        currentSkillTarget = GetTarget();
        currentComboIndex = 0;

        isUsingSkill = true;
        hasEnteredAnySkillState = false;
        hasEnteredLastSkillState = false;
        isPostSkillFacing = false;
        skillStartTime = Time.time;

        aoeHitTargets.Clear();

        if (actor != null)
        {
            actor.actorState.isAttacking = true;
            actor.actorState.isGuarding = currentSkill.canBeBlock;
        }

        if (disableCombatControllerWhileSkill && combatController != null)
        {
            combatControllerWasEnabled = combatController.enabled;
            combatController.enabled = false;
        }

        PrepareMovement();
        PlaySkillFirstState();

        return true;
    }

    private void PrepareMovement()
    {
        if (animator != null)
        {
            previousApplyRootMotion = animator.applyRootMotion;
            animator.applyRootMotion = true;
        }

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            previousAgentUpdatePosition = navAgent.updatePosition;
            previousAgentUpdateRotation = navAgent.updateRotation;

            navAgent.isStopped = true;
            navAgent.ResetPath();
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
            navAgent.nextPosition = transform.position;
        }
    }

    private void RestoreMovement()
    {
        if (animator != null)
            animator.applyRootMotion = previousApplyRootMotion;

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.Warp(transform.position);
            navAgent.updatePosition = previousAgentUpdatePosition;
            navAgent.updateRotation = previousAgentUpdateRotation;
            navAgent.isStopped = false;
            navAgent.nextPosition = transform.position;
        }
    }

    private void PlaySkillFirstState()
    {
        if (animator == null || currentSkill == null || string.IsNullOrEmpty(currentSkill.FirstStateName))
        {
            FinishSkill();
            return;
        }

        animator.Play(currentSkill.FirstStateName, currentSkill.animatorLayer, 0f);
        animator.Update(0f);
    }

    private void TickSkill()
    {
        if (currentSkill == null || animator == null)
        {
            FinishSkill();
            return;
        }

        int layer = currentSkill.animatorLayer;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);

        bool inTransition = animator.IsInTransition(layer);
        bool isCurrentSkillState = IsStateInCurrentSkill(state);
        bool isLastSkillState = IsLastSkillState(state);

        if (isCurrentSkillState)
        {
            hasEnteredAnySkillState = true;
            UpdateComboIndex(state);
        }

        if (isLastSkillState)
            hasEnteredLastSkillState = true;

        if (!inTransition &&
            hasEnteredLastSkillState &&
            isLastSkillState &&
            state.normalizedTime >= currentSkill.finishNormalizedTime)
        {
            FinishSkill();
            return;
        }

        if (finishWhenExitSkillState)
        {
            if (hasEnteredAnySkillState && !inTransition && !isCurrentSkillState)
            {
                FinishSkill();
                return;
            }

            if (!hasEnteredAnySkillState &&
                Time.time - skillStartTime >= enterSkillStateGraceTime &&
                !inTransition &&
                !isCurrentSkillState)
            {
                CancelSkill(false);
            }
        }
    }

    private bool IsStateInCurrentSkill(AnimatorStateInfo state)
    {
        if (currentSkill == null)
            return false;

        for (int i = 0; i < currentSkill.ComboCount; i++)
        {
            string stateName = currentSkill.GetStateName(i);

            if (!string.IsNullOrEmpty(stateName) && state.IsName(stateName))
                return true;
        }

        return false;
    }

    private bool IsLastSkillState(AnimatorStateInfo state)
    {
        return currentSkill != null &&
               !string.IsNullOrEmpty(currentSkill.LastStateName) &&
               state.IsName(currentSkill.LastStateName);
    }

    public void ApplyRootMotion(Animator sourceAnimator)
    {
        if (!isUsingSkill || sourceAnimator == null)
            return;

        MoveRoot(sourceAnimator.deltaPosition);
        RotateRoot(sourceAnimator.deltaRotation);
    }

    private void MoveRoot(Vector3 delta)
    {
        if (delta.sqrMagnitude <= 0.0000001f)
            return;

        if (characterController != null && characterController.enabled)
            characterController.Move(delta);
        else
            transform.position += delta;

        SyncNavAgent();
    }

    private void RotateRoot(Quaternion deltaRotation)
    {
        if (deltaRotation == Quaternion.identity)
            return;

        transform.rotation *= deltaRotation;
        SyncNavAgent();
    }

    private void SyncNavAgent()
    {
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            navAgent.nextPosition = transform.position;
    }

    public void FinishSkill()
    {
        if (!isUsingSkill)
            return;

        if (currentSkill != null)
            SetSkillCooldown(currentSkill);

        RestoreMovement();

        Transform finalTarget = currentSkillTarget;

        currentSkill = null;
        currentSkillTarget = null;
        isUsingSkill = false;
        hasEnteredAnySkillState = false;
        hasEnteredLastSkillState = false;

        aoeHitTargets.Clear();

        if (actor != null)
        {
            actor.actorState.isAttacking = false;
            actor.actorState.isGuarding = false;
        }

        if (disableCombatControllerWhileSkill && combatController != null)
            combatController.enabled = combatControllerWasEnabled;

        if (finalTarget != null && combatController != null)
        {
            combatController.ForceKeepTarget(postSkillConfig.targetKeepTime);

            if (postSkillConfig.smoothFaceTargetOnFinish)
            {
                currentSkillTarget = finalTarget;
                isPostSkillFacing = true;
                postSkillFaceEndTime = Time.time + postSkillConfig.targetKeepTime;
            }
        }

        RefreshSkillPool();
    }

    public void CancelSkill(bool startCooldown = false)
    {
        if (!isUsingSkill)
            return;

        if (startCooldown && currentSkill != null && !currentSkill.isSuperArmor)
            SetSkillCooldown(currentSkill);

        RestoreMovement();

        currentSkill = null;
        currentSkillTarget = null;
        isUsingSkill = false;
        hasEnteredAnySkillState = false;
        hasEnteredLastSkillState = false;

        aoeHitTargets.Clear();

        if (actor != null)
        {
            actor.actorState.isAttacking = false;
            actor.actorState.isGuarding = false;
        }

        if (disableCombatControllerWhileSkill && combatController != null)
            combatController.enabled = combatControllerWasEnabled;

        if (weaponHitbox != null)
            weaponHitbox.EndHitbox();

        isPostSkillFacing = false;

        RefreshSkillPool();
    }

    private void UpdatePostSkillFacing()
    {
        if (Time.time >= postSkillFaceEndTime || currentSkillTarget == null)
        {
            isPostSkillFacing = false;
            currentSkillTarget = null;
            return;
        }

        Vector3 dir = currentSkillTarget.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
        {
            isPostSkillFacing = false;
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir.normalized),
            postSkillConfig.faceTargetSmoothSpeed * Time.deltaTime
        );
    }

    private void SetSkillCooldown(EnemyAttackSkill skill)
    {
        if (skill != null)
            skillCooldownEndTimes[skill.atkskillID] = Time.time + skill.atkskillCoolTime;
    }

    public bool IsSkillCoolingDown(EnemyAttackSkill skill)
    {
        if (skill == null)
            return true;

        skillCooldownEndTimes.TryGetValue(skill.atkskillID, out float endTime);
        return Time.time < endTime;
    }

    public float GetSkillCooldownRemain(EnemyAttackSkill skill)
    {
        if (skill == null)
            return 0f;

        skillCooldownEndTimes.TryGetValue(skill.atkskillID, out float endTime);
        return Mathf.Max(0f, endTime - Time.time);
    }

    public float GetCurrentDamage()
    {
        if (currentSkill == null) return 10f;
        return currentSkill.GetDamage(currentComboIndex);
    }

    private void UpdateComboIndex(AnimatorStateInfo state)
    {
        if (currentSkill == null) return;

        for (int i = 0; i < currentSkill.ComboCount; i++)
        {
            string stateName = currentSkill.GetStateName(i);
            if (!string.IsNullOrEmpty(stateName) && state.IsName(stateName))
            {
                currentComboIndex = i;
                break;
            }
        }
    }

    public void OnAnimationAttackEvent(string hitStateName)
    {
        if (currentSkill == null)
            return;

        // 检查当前连段是否为突进
        bool isCurrentDash = currentSkill.IsDashCombo(currentComboIndex);
        DashData currentDashData = currentSkill.GetDashData(currentComboIndex);

        if (isCurrentDash && currentDashData != null)
        {
            if (currentDashData.hitCount > 1)
            {
                StartDashDamageSequence(hitStateName, currentDashData);
            }
            else
            {
                DashDamageDetect(hitStateName, currentDashData);
            }
        }
        else if (currentSkill.isAOESkill)
        {
            if (currentSkill.isPowerSkill)
            {
                StartAOEDamageSequence(hitStateName);
            }
            else
            {
                AOEDamageDetect(hitStateName);
            }
        }
        else if (currentSkill.isPowerSkill)
        {
            StartPowerHitSequence(hitStateName);
        }
        else if (weaponHitbox != null)
        {
            weaponHitbox.BeginHitbox(hitStateName, GetCurrentDamage());
        }
    }

    public void OnAnimationAttackEndEvent()
    {
        if (currentSkill == null || currentSkill.isAOESkill || currentSkill.isDashSkill)
            return;

        if (currentSkill.isPowerSkill)
        {
            StopPowerHitSequence();
            return;
        }

        if (weaponHitbox != null)
            weaponHitbox.EndHitbox();
    }

    private void AOEDamageDetect(string hitStateName)
    {
        if (currentSkill == null)
            return;

        Vector3 center = transform.TransformPoint(currentSkill.aoeCenterOffset);
        float damage = GetCurrentDamage();

        Collider[] hitCols = Physics.OverlapSphere(
            center,
            currentSkill.aoeRadius,
            currentSkill.aoeTargetMask
        );

        foreach (var col in hitCols)
        {
            if (col.transform.IsChildOf(transform))
                continue;

            IHitReceiver receiver = col.GetComponentInParent<IHitReceiver>();

            if (receiver == null)
                continue;

            if (currentSkill.aoeIgnoreRepeatHit && aoeHitTargets.Contains(receiver))
                continue;

            if (currentSkill.aoeIgnoreRepeatHit)
                aoeHitTargets.Add(receiver);
            
            // 先尝试处决
            if (ExecutionManager.TryStartExecution(gameObject, col.gameObject))
            {
                // 处决成功，跳过这个目标
                continue;
            }
            
            receiver.ReceiveHit(hitStateName, damage);
            StartHitStop();
        }
    }

    private void StartAOEDamageSequence(string hitStateName)
    {
        if (aoeDamageCoroutine != null)
            StopCoroutine(aoeDamageCoroutine);

        if (currentSkill.aoeHitCount <= 1)
        {
            AOEDamageDetect(hitStateName);
            return;
        }

        aoeDamageCoroutine = StartCoroutine(AOEDamageSequenceCoroutine(hitStateName));
    }

    private IEnumerator AOEDamageSequenceCoroutine(string hitStateName)
    {
        for (int i = 0; i < currentSkill.aoeHitCount; i++)
        {
            if (!isUsingSkill || currentSkill == null)
                yield break;

            AOEDamageDetect(hitStateName);

            if (i < currentSkill.aoeHitCount - 1)
                yield return new WaitForSeconds(currentSkill.aoeHitInterval);
        }

        aoeDamageCoroutine = null;
    }

    private void StartDashDamageSequence(string hitStateName, DashData dashData)
    {
        if (dashDamageCoroutine != null)
            StopCoroutine(dashDamageCoroutine);

        if (dashData.hitCount <= 1)
        {
            DashDamageDetect(hitStateName, dashData);
            return;
        }

        dashDamageCoroutine = StartCoroutine(DashDamageSequenceCoroutine(hitStateName, dashData));
    }

    private IEnumerator DashDamageSequenceCoroutine(string hitStateName, DashData dashData)
    {
        for (int i = 0; i < dashData.hitCount; i++)
        {
            if (!isUsingSkill || currentSkill == null)
                yield break;

            DashDamageDetect(hitStateName, dashData);

            if (i < dashData.hitCount - 1)
                yield return new WaitForSeconds(dashData.hitInterval);
        }

        dashDamageCoroutine = null;
    }

    private void DashDamageDetect(string hitStateName, DashData dashData)
    {
        if (dashData == null)
            return;

        float damage = GetCurrentDamage();

        // 计算矩形的位置和旋转
        Vector3 boxCenter = transform.position + transform.forward * (dashData.length / 2f + dashData.offset.z) + 
                            transform.right * dashData.offset.x + 
                            transform.up * dashData.offset.y;
        Vector3 boxSize = new Vector3(dashData.width, dashData.height, dashData.length);

        Collider[] hitCols = Physics.OverlapBox(
            boxCenter,
            boxSize / 2f,
            transform.rotation,
            dashData.targetMask
        );

        foreach (var col in hitCols)
        {
            if (col.transform.IsChildOf(transform))
                continue;

            IHitReceiver receiver = col.GetComponentInParent<IHitReceiver>();

            if (receiver == null)
                continue;

            if (dashData.ignoreRepeatHit && aoeHitTargets.Contains(receiver))
                continue;

            if (dashData.ignoreRepeatHit)
                aoeHitTargets.Add(receiver);
            
            // 先尝试处决
            if (ExecutionManager.TryStartExecution(gameObject, col.gameObject))
            {
                // 处决成功，跳过这个目标
                continue;
            }
            
            receiver.ReceiveHit(hitStateName, damage);
            StartHitStop();
        }
    }

    private void StartPowerHitSequence(string hitStateName)
    {
        if (powerHitCoroutine != null)
            StopCoroutine(powerHitCoroutine);

        if (currentSkill.powerHitCount <= 1)
        {
            if (weaponHitbox != null)
                weaponHitbox.BeginHitbox(hitStateName, GetCurrentDamage());
            return;
        }

        powerHitCoroutine = StartCoroutine(PowerHitSequenceCoroutine(hitStateName));
    }

    private IEnumerator PowerHitSequenceCoroutine(string hitStateName)
    {
        for (int i = 0; i < currentSkill.powerHitCount; i++)
        {
            if (!isUsingSkill || currentSkill == null)
                yield break;

            if (weaponHitbox != null)
                weaponHitbox.BeginHitbox(hitStateName, GetCurrentDamage());

            yield return new WaitForSeconds(currentSkill.powerHitInterval);

            if (weaponHitbox != null)
                weaponHitbox.EndHitbox();
        }

        powerHitCoroutine = null;
    }

    private void StopPowerHitSequence()
    {
        if (powerHitCoroutine != null)
        {
            StopCoroutine(powerHitCoroutine);
            powerHitCoroutine = null;
        }

        if (weaponHitbox != null)
            weaponHitbox.EndHitbox();
    }

    private void OnHitEnemy(GameObject enemy)
    {
        StartHitStop();
    }

    private void StartHitStop()
    {
        if (hitStopConfig.hitStopDuration <= 0 || isHitStopping)
            return;

        if (hitStopCoroutine != null)
            StopCoroutine(hitStopCoroutine);

        hitStopCoroutine = StartCoroutine(HitStopCoroutine());
    }

    private IEnumerator HitStopCoroutine()
    {
        isHitStopping = true;
        Time.timeScale = hitStopConfig.hitStopTimeScale;

        yield return new WaitForSecondsRealtime(hitStopConfig.hitStopDuration);

        Time.timeScale = 1f;
        isHitStopping = false;
        hitStopCoroutine = null;
    }
}

public class EnemySkillRootMotionRelay : MonoBehaviour
{
    private EnemyAttackSkillPlayer skillPlayer;
    private Animator animator;

    public void Initialize(EnemyAttackSkillPlayer owner, Animator sourceAnimator)
    {
        skillPlayer = owner;
        animator = sourceAnimator;
    }

    private void OnAnimatorMove()
    {
        if (skillPlayer != null && animator != null)
            skillPlayer.ApplyRootMotion(animator);
    }
}