using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerSkillPlayer : MonoBehaviour
{
    [Header("技能数据列表")]
    public PlayerSkillData[] skills;

    [Header("武器Hitbox")]
    public WeaponHitbox weaponHitbox;

    [Header("设置")]
    public bool disableCombatControllerWhileSkill = true;
    public bool finishWhenExitSkillState = true;
    public float enterSkillStateGraceTime = 0.15f;
    public float targetKeepTime = 3f;
    public bool smoothFaceTargetOnFinish = true;
    public float faceTargetSmoothSpeed = 3f;
    [Header("转向敌人设置")]
    public float maxFaceTargetDistance = 5f;  // 只有在8米范围内才自动转向

    private ActorBase actor;
    private Animator animator;
    private CharacterController characterController;
    private PlayerCombatController combatController;
    private PlayerMoveController moveController;
    private PlayerHealthController healthController;
    private PlayerSkillRootMotionRelay rootMotionRelay;

    private PlayerSkillData currentSkill;
    private Transform currentSkillTarget;
    private int currentComboIndex;
    private int previousComboIndex = -1;
    private int currentSkillIndex = -1;

    private bool isUsingSkill;
    private bool combatControllerWasEnabled;
    private bool hasEnteredAnySkillState;
    private bool hasEnteredLastSkillState;
    private bool previousApplyRootMotion;
    private bool isPostSkillFacing;
    private bool isHitStopping;

    private float postSkillFaceEndTime;
    private float skillStartTime;

    private readonly Dictionary<int, float> skillCooldownEndTimes = new Dictionary<int, float>();
    private readonly HashSet<object> aoeHitTargets = new HashSet<object>();
    private Coroutine aoeDamageCoroutine;
    private Coroutine multiHitCoroutine;
    private Coroutine hitStopCoroutine;
    private Coroutine dashDamageCoroutine;

    public bool IsUsingSkill => isUsingSkill;
    public bool IsSkillPlaying() => isUsingSkill;
    public PlayerSkillData CurrentSkill => currentSkill;
    public Transform CurrentSkillTarget => currentSkillTarget;
    public bool IsSuperArmorActive => isUsingSkill && currentSkill != null && currentSkill.isSuperArmor;
    public bool CanBlockActive => isUsingSkill && currentSkill != null && currentSkill.canBeBlock;

    private void Awake()
    {
        actor = GetComponent<ActorBase>();
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        combatController = GetComponent<PlayerCombatController>();
        moveController = GetComponent<PlayerMoveController>();
        healthController = GetComponent<PlayerHealthController>();

        SetupRootMotionRelay();

        if (weaponHitbox != null)
            weaponHitbox.OnHitEnemy += OnHitEnemy;
    }

    private void SetupRootMotionRelay()
    {
        if (animator == null)
            return;

        rootMotionRelay = animator.GetComponent<PlayerSkillRootMotionRelay>();

        if (rootMotionRelay == null)
            rootMotionRelay = animator.gameObject.AddComponent<PlayerSkillRootMotionRelay>();

        rootMotionRelay.Initialize(this, animator);
    }

    private void Update()
    {
        if (isPostSkillFacing)
            UpdatePostSkillFacing();

        if (!isUsingSkill)
            return;

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

    public bool IsSkillOnCooldown(int skillIndex)
    {
        if (skillIndex < 0 || skillIndex >= skills.Length) return true;
        if (!skillCooldownEndTimes.TryGetValue(skillIndex, out float endTime)) return false;
        return Time.time < endTime;
    }

    public float GetSkillCooldownRemaining(int skillIndex)
    {
        if (skillIndex < 0 || skillIndex >= skills.Length) return 0f;
        if (skillCooldownEndTimes.TryGetValue(skillIndex, out float endTime))
            return Mathf.Max(0f, endTime - Time.time);
        return 0f;
    }

    public bool CanPlaySkill(int skillIndex)
    {
        if (skillIndex < 0 || skillIndex >= skills.Length) return false;
        if (skills[skillIndex] == null) return false;
        if (isUsingSkill) return false;
        if (IsSkillOnCooldown(skillIndex)) return false;

        if (actor != null && actor.actorState.isHit) return false;
        if (healthController != null && healthController.IsGuarding()) return false;

        return true;
    }

    public void TryPlaySkill(int skillIndex)
    {
        if (!CanPlaySkill(skillIndex)) return;

        PlayerSkillData skill = skills[skillIndex];
        if (skill == null) return;

        if (skill.interruptNormalAttack)
        {
            combatController?.CancelAttack();
        }

        StartSkill(skillIndex);
    }

    private bool StartSkill(int skillIndex)
    {
        PlayerSkillData skill = skills[skillIndex];
        if (skill == null) return false;

        currentSkill = skill;
        currentSkillIndex = skillIndex;
        currentSkillTarget = FindNearestEnemy();
        currentComboIndex = 0;
        previousComboIndex = -1;

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

    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float minDist = float.MaxValue;

        foreach (GameObject enemy in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy.transform;
            }
        }

        return nearest;
    }

    private void PrepareMovement()
    {
        if (animator != null)
        {
            previousApplyRootMotion = animator.applyRootMotion;
            animator.applyRootMotion = currentSkill.useRootMotion;
        }
    }

    private void RestoreMovement()
    {
        if (animator != null)
            animator.applyRootMotion = previousApplyRootMotion;
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
        // 立即转向敌人
        FaceNearestEnemy();
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

    private void UpdateComboIndex(AnimatorStateInfo state)
    {
        if (currentSkill == null) return;

        for (int i = 0; i < currentSkill.ComboCount; i++)
        {
            string stateName = currentSkill.GetStateName(i);
            if (!string.IsNullOrEmpty(stateName) && state.IsName(stateName))
            {
                if (i != previousComboIndex)
                {
                    // 进入新的连段，快速平滑转向敌人
                    FaceNearestEnemy();
                    previousComboIndex = i;
                }
                currentComboIndex = i;
                break;
            }
        }
    }

    private void FaceNearestEnemy()
    {
        if (currentSkillTarget == null)
            return;

        // 检查敌人是否在范围内
        float distance = Vector3.Distance(transform.position, currentSkillTarget.position);
        if (distance > maxFaceTargetDistance)
            return;

        Vector3 dir = currentSkillTarget.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        // 快速平滑转向敌人
        Quaternion targetRotation = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f);
    }

    public void ApplyRootMotion(Animator sourceAnimator)
    {
        if (!isUsingSkill || sourceAnimator == null)
            return;
        
        // 获取当前连段的缩放系数
        float scale = currentSkill.GetRootMotionScale(currentComboIndex);
        
        // 应用缩放到位移和旋转
        Vector3 deltaPosition = sourceAnimator.deltaPosition * scale;
        Quaternion deltaRotation = Quaternion.Slerp(Quaternion.identity, sourceAnimator.deltaRotation, scale);
        
        MoveRoot(deltaPosition);
        RotateRoot(deltaRotation);
    }

    private void MoveRoot(Vector3 delta)
    {
        if (delta.sqrMagnitude <= 0.0000001f)
            return;

        if (characterController != null && characterController.enabled)
            characterController.Move(delta);
        else
            transform.position += delta;
    }

    private void RotateRoot(Quaternion deltaRotation)
    {
        if (deltaRotation == Quaternion.identity)
            return;

        transform.rotation *= deltaRotation;
    }

    public void FinishSkill()
    {
        if (!isUsingSkill)
            return;

        if (currentSkill != null)
            SetSkillCooldown(currentSkillIndex, currentSkill);

        RestoreMovement();

        Transform finalTarget = currentSkillTarget;

        currentSkill = null;
        currentSkillIndex = -1;
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
        
        StopMultiHitSequence();

        if (finalTarget != null && smoothFaceTargetOnFinish)
        {
            // 检查敌人是否在范围内
            float distance = Vector3.Distance(transform.position, finalTarget.position);
            if (distance <= maxFaceTargetDistance)
            {
                currentSkillTarget = finalTarget;
                isPostSkillFacing = true;
                postSkillFaceEndTime = Time.time + targetKeepTime;
            }
        }
    }

    public void CancelSkill(bool startCooldown = false)
    {
        if (!isUsingSkill)
            return;

        if (startCooldown && currentSkill != null && !currentSkill.isSuperArmor)
            SetSkillCooldown(currentSkillIndex, currentSkill);

        RestoreMovement();

        currentSkill = null;
        currentSkillIndex = -1;
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
        
        StopMultiHitSequence();

        isPostSkillFacing = false;
    }

    private void SetSkillCooldown(int skillIndex, PlayerSkillData skill)
    {
        if (skill != null)
            skillCooldownEndTimes[skillIndex] = Time.time + skill.coolDown;
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
            faceTargetSmoothSpeed * Time.deltaTime
        );
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
            if (currentSkill.isMultiHitSkill)
            {
                StartAOEDamageSequence(hitStateName);
            }
            else
            {
                AOEDamageDetect(hitStateName);
            }
        }
        else if (currentSkill.isMultiHitSkill)
        {
            StartMultiHitSequence(hitStateName);
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

        if (currentSkill.isMultiHitSkill)
        {
            StopMultiHitSequence();
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

            IHitReceiver hitReceiver = col.GetComponentInParent<IHitReceiver>();

            if (hitReceiver == null)
                continue;

            if (currentSkill.aoeIgnoreRepeatHit && aoeHitTargets.Contains(hitReceiver))
                continue;

            if (currentSkill.aoeIgnoreRepeatHit)
                aoeHitTargets.Add(hitReceiver);
            
            // 先尝试处决
            if (ExecutionManager.TryStartExecution(gameObject, col.gameObject))
            {
                // 处决成功，跳过这个目标
                continue;
            }
            
            hitReceiver.ReceiveHit(hitStateName, damage);
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

            IHitReceiver hitReceiver = col.GetComponentInParent<IHitReceiver>();

            if (hitReceiver == null)
                continue;

            if (dashData.ignoreRepeatHit && aoeHitTargets.Contains(hitReceiver))
                continue;

            if (dashData.ignoreRepeatHit)
                aoeHitTargets.Add(hitReceiver);
            
            // 先尝试处决
            if (ExecutionManager.TryStartExecution(gameObject, col.gameObject))
            {
                // 处决成功，跳过这个目标
                continue;
            }
            
            hitReceiver.ReceiveHit(hitStateName, damage);
            StartHitStop();
        }
    }

    private void StartMultiHitSequence(string hitStateName)
    {
        if (multiHitCoroutine != null)
            StopCoroutine(multiHitCoroutine);

        if (currentSkill.multiHitCount <= 1)
        {
            if (weaponHitbox != null)
                weaponHitbox.BeginHitbox(hitStateName, GetCurrentDamage());
            return;
        }

        multiHitCoroutine = StartCoroutine(MultiHitSequenceCoroutine(hitStateName));
    }

    private IEnumerator MultiHitSequenceCoroutine(string hitStateName)
    {
        for (int i = 0; i < currentSkill.multiHitCount; i++)
        {
            if (!isUsingSkill || currentSkill == null)
                yield break;

            if (weaponHitbox != null)
                weaponHitbox.BeginHitbox(hitStateName, GetCurrentDamage());

            yield return new WaitForSeconds(currentSkill.multiHitInterval);

            if (weaponHitbox != null)
                weaponHitbox.EndHitbox();
        }

        multiHitCoroutine = null;
    }

    private void StopMultiHitSequence()
    {
        if (multiHitCoroutine != null)
        {
            StopCoroutine(multiHitCoroutine);
            multiHitCoroutine = null;
        }

        if (weaponHitbox != null)
            weaponHitbox.EndHitbox();
    }

    private float GetCurrentDamage()
    {
        if (currentSkill == null) return 10f;
        return currentSkill.GetDamage(currentComboIndex);
    }

    private void OnHitEnemy(GameObject enemy)
    {
        StartHitStop();
    }

    private void StartHitStop()
    {
        if (currentSkill != null)
            StartHitStop(currentSkill.hitStopDuration, currentSkill.hitStopTimeScale);
    }

    private void StartHitStop(float duration, float timeScale)
    {
        if (duration <= 0 || isHitStopping)
            return;

        if (hitStopCoroutine != null)
            StopCoroutine(hitStopCoroutine);

        hitStopCoroutine = StartCoroutine(HitStopCoroutine(duration, timeScale));
    }

    private IEnumerator HitStopCoroutine(float duration, float timeScale)
    {
        isHitStopping = true;
        Time.timeScale = timeScale;

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1f;
        isHitStopping = false;
        hitStopCoroutine = null;
    }
}
