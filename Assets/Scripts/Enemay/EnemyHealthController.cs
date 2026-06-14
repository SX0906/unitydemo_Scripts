using UnityEngine.AI;
using UnityEngine;
using System.Collections;

[System.Serializable]
public class EnemyHitConfig
{
    public float hitStopTime = 0.35f;
    public float crossFadeTime = 0.02f;
    public string hitStatePrefix = "Base Layer.Katana.Hit.";
    public string parryStatePrefix = "Base Layer.Katana.Hit.Parry.";

    [Header("后撤翻滚动画")]
    public string backRollStateName = "Base Layer.Katana.BackRoll";
    public string backRollTag = "Roll";
    public float backRollCrossFadeTime = 0.05f;

    [Header("敌人弹刀顿帧")]
    public float parryHitStopDuration = 0.15f;
    public float parryHitStopTimeScale = 0.1f;

    [Header("弹刀硬直时长")]
    public float parryRecoveryTime = 0.2f;

    [Header("受击转向设置")]
    [Tooltip("受击时转向攻击者的速度(度/秒)")]
    public float hitTurnSpeed = 60f;

    [Header("格挡范围设置")]
    [Tooltip("正面可格挡的角度范围(度)")]
    public float blockAngleRange = 90f;
}

[System.Serializable]
public class EnemyBackRollConfig
{
    [Header("后滚基础速度")]
    public float backRollSpeed = 5f;
    [Header("是否应用 Y 轴位移")]
    public bool applyY = true;
    [Header("使用 CharacterController 移动")]
    public bool useCharacterController = true;
    [Header("AnimationMove 曲线控制")]
    public float animationMoveMultiplier = 1f;
    [Header("垂直方向单独倍率")]
    public float verticalMultiplier = 1f;
}

public class EnemyHealthController : MonoBehaviour, IHitReceiver
{
    [Header("敌人受击/格挡参数")]
    public EnemyHitConfig hitConfig = new();
    [Header("后滚移动设置")]
    public EnemyBackRollConfig backRollConfig = new();

    [Header("自动格挡设置")]
    public bool enableBlock = true;
    public float blockCooldown = 0.1f;
    public float blockWindowDuration = 1f;
    public float blockDistance = 1.5f;
    public int maxBlockCountPerGuard = 2;

    [Header("受击后撤设置")]
    public int hitCountToBackRoll = 3;
    public float backRollCooldown = 2f;

    private ActorBase actor;
    private Animator animator;
    private NavMeshAgent navAgent;
    private CharacterController characterController;
    private EnemyCombatController combat;
    private EnemyAttackSkillPlayer skillPlayer;

    private PlayerCombatController playerCombat;
    private PlayerSkillPlayer playerSkillPlayer;
    private float blockCooldownTimer;
    private int currentBlockRemain;
    private int normalHitCounter;
    private float lastBackRollTime = -999f;
    private bool isBackRolling;
    private bool hasEnteredBackRollAnim;
    private float backRollStartTime;
    private Vector3 backRollDirection;

    private Coroutine parryHitStopCoroutine;
    private bool isParryHitStopping;
    private bool isBlockReady;
    private Coroutine blockWindowCoroutine;

    // 受击转向相关变量
    private bool isTurningToTarget;
    private Transform currentAttacker;

    public bool IsBackRolling => isBackRolling;

    private void Awake()
    {
        actor = GetComponent<ActorBase>();
        animator = GetComponentInChildren<Animator>();
        navAgent = GetComponent<NavMeshAgent>();
        characterController = GetComponent<CharacterController>();
        combat = GetComponent<EnemyCombatController>();
        skillPlayer = GetComponent<EnemyAttackSkillPlayer>();

        blockCooldownTimer = blockCooldown;
        currentBlockRemain = Mathf.Max(0, maxBlockCountPerGuard);
    }

    private void Update()
    {
        if (!actor) return;
        CachePlayerCombat();
        blockCooldownTimer += Time.deltaTime;
        CheckBackRollAnimationFinish();
        UpdateBackRollMovement();
        UpdateTurnToTarget(); // 受击时转向逻辑，格挡时不会触发
    }

    private void CachePlayerCombat()
    {
        if (playerCombat != null || combat == null || combat.Target == null) return;
        playerCombat = combat.Target.GetComponent<PlayerCombatController>();
        playerSkillPlayer = combat.Target.GetComponent<PlayerSkillPlayer>();
    }

    // 受击时平滑转向攻击者
    private void UpdateTurnToTarget()
    {
        if (!isTurningToTarget || currentAttacker == null || isBackRolling) return;

        Vector3 directionToTarget = currentAttacker.position - transform.position;
        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude < 0.01f)
        {
            isTurningToTarget = false;
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            hitConfig.hitTurnSpeed * Time.deltaTime
        );

        // 当几乎面向目标时停止转向
        if (Quaternion.Angle(transform.rotation, targetRotation) < 1f)
        {
            isTurningToTarget = false;
        }
    }

    private void CheckBackRollAnimationFinish()
    {
        if (!isBackRolling || animator == null) return;
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsTag(hitConfig.backRollTag))
        {
            hasEnteredBackRollAnim = true;
            return;
        }
        if (hasEnteredBackRollAnim || Time.time - backRollStartTime > 1.5f)
            EndBackRollState();
    }

    private bool IsPlayerInBlockRange()
    {
        return combat != null && combat.Target != null &&
               Vector3.Distance(transform.position, combat.Target.position) <= blockDistance;
    }

    // 检查攻击者是否在正面格挡角度范围内
    private bool IsAttackerInFrontAngle(Transform attacker)
    {
        if (attacker == null) return false;

        Vector3 directionToAttacker = attacker.position - transform.position;
        directionToAttacker.y = 0f;
        directionToAttacker.Normalize();

        float angle = Vector3.Angle(transform.forward, directionToAttacker);
        return angle <= hitConfig.blockAngleRange / 2f;
    }

    public bool TryStartAutoBlock()
    {
        if (!CanStartBlockWindow()) return false;
        StopCurrentBlockWindow();
        blockWindowCoroutine = StartCoroutine(BlockWindowCoroutine());
        return true;
    }

    private bool CanStartBlockWindow()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        return enableBlock && actor != null &&
               !state.IsTag("Skill") &&
               !isBlockReady &&
               blockCooldownTimer >= blockCooldown &&
               !isBackRolling &&
               currentBlockRemain > 0;
    }

    private IEnumerator BlockWindowCoroutine()
    {
        isBlockReady = true;
        blockCooldownTimer = 0f;
        yield return new WaitForSeconds(blockWindowDuration);
        isBlockReady = false;
        blockWindowCoroutine = null;
    }

    private void StopCurrentBlockWindow()
    {
        if (blockWindowCoroutine != null)
        {
            StopCoroutine(blockWindowCoroutine);
            blockWindowCoroutine = null;
        }
        isBlockReady = false;
    }

    public void ReceiveHit(string hitStateName, float damage)
    {
        if (!actor || !animator || string.IsNullOrEmpty(hitStateName) || isBackRolling) return;

        CancelInvoke(nameof(EndHitState));
        CancelInvoke(nameof(EndParryState));
        StopTurningToTarget();

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        // 检查玩家技能是否可以被格挡
        bool hitCanBeBlocked = true;
        if (playerSkillPlayer != null && playerSkillPlayer.IsUsingSkill && playerSkillPlayer.CurrentSkill != null)
        {
            hitCanBeBlocked = playerSkillPlayer.CurrentSkill.canBeBlock;
        }

        // 只有在正面范围内且玩家技能允许被格挡时才能格挡
        if (hitCanBeBlocked &&
            currentBlockRemain > 0 &&
            !state.IsTag("Skill") &&
            !string.IsNullOrEmpty(GetParryStateName(hitStateName)) &&
            IsAttackerInFrontAngle(combat?.Target))
        {
            PlayParryAnimation(hitStateName);
            return;
        }

        if (skillPlayer != null && skillPlayer.IsSuperArmorActive)
        {
            GetComponentInChildren<CombatAudioPlayer>()?.PlayHitSound();
            return;
        }

        ReceiveNormalHit(hitStateName, damage);
    }

    private void PlayParryAnimation(string hitStateName)
    {
        string parryState = GetParryStateName(hitStateName);
        if (string.IsNullOrEmpty(parryState))
        {
            ReceiveNormalHit(hitStateName, 0f);
            return;
        }

        currentBlockRemain--;
        StopCurrentBlockWindow();

        actor.actorState.isGuarding = true;
        actor.actorState.isHit = false;
        actor.actorState.isAttacking = false;

        if (skillPlayer != null && skillPlayer.IsUsingSkill && !skillPlayer.IsSuperArmorActive)
            skillPlayer.CancelSkill(false);

        StopNavAgent();

        animator.CrossFadeInFixedTime(hitConfig.parryStatePrefix + parryState, hitConfig.crossFadeTime, 0);
        GetComponentInChildren<CombatAudioPlayer>()?.PlayParrySound();
        StartParryHitStop();

        Invoke(nameof(EndParryState), hitConfig.parryRecoveryTime);
    }

    // 弹刀结束自动回到可格挡状态 → 支持连续格挡
    private void EndParryState()
    {
        actor.actorState.isGuarding = false;
        // 格挡结束不启动转向，保持原有朝向
        ResumeNavAgent();

        if (currentBlockRemain > 0)
        {
            TryStartAutoBlock();
        }
    }

    private void ReceiveNormalHit(string hitStateName, float damage)
    {
        StopCurrentBlockWindow();

        if (skillPlayer != null && skillPlayer.IsUsingSkill && !skillPlayer.IsSuperArmorActive)
            skillPlayer.CancelSkill(true);

        actor.actorState.isHit = true;
        actor.actorState.isAttacking = false;
        actor.actorState.isGuarding = false;

        HealthSystem health = GetComponent<HealthSystem>();
        if (health != null)
        {
            health.TakeDamage(damage);
        }

        StopNavAgent();
        normalHitCounter++;

        if (ShouldTriggerBackRoll())
        {
            PlayBackRoll();
            return;
        }

        animator.CrossFadeInFixedTime(hitConfig.hitStatePrefix + hitStateName, hitConfig.crossFadeTime, 0);
        Invoke(nameof(EndHitState), hitConfig.hitStopTime);
        GetComponentInChildren<CombatAudioPlayer>()?.PlayHitSound();

        if (combat?.Target != null)
        {
            StartTurningToTarget(combat.Target);
        }
    }

    private bool ShouldTriggerBackRoll()
    {
        return hitCountToBackRoll > 0 &&
               normalHitCounter >= hitCountToBackRoll &&
               Time.time - lastBackRollTime >= backRollCooldown &&
               !string.IsNullOrEmpty(hitConfig.backRollStateName);
    }

    private void PlayBackRoll()
    {
        normalHitCounter = 0;
        lastBackRollTime = Time.time;
        backRollStartTime = Time.time;
        isBackRolling = true;
        hasEnteredBackRollAnim = false;

        CancelInvoke(nameof(EndHitState));
        CancelInvoke(nameof(EndParryState));
        StopCurrentBlockWindow();
        StopTurningToTarget(); // 后滚时停止转向

        actor.actorState.isHit = true;
        actor.actorState.isAttacking = false;
        actor.actorState.isGuarding = false;

        if (combat != null && combat.Target != null)
        {
            backRollDirection = transform.position - combat.Target.position;
            backRollDirection.y = 0f;
            backRollDirection.Normalize();
        }
        else
        {
            backRollDirection = -transform.forward;
        }

        StopNavAgent();
        animator.CrossFadeInFixedTime(hitConfig.backRollStateName, hitConfig.backRollCrossFadeTime, 0);
    }

    private void EndBackRollState()
    {
        if (!isBackRolling) return;
        isBackRolling = false;
        hasEnteredBackRollAnim = false;
        ResumeNavAgent();
        if (actor)
        {
            actor.actorState.isHit = false;
            actor.actorState.isAttacking = false;
            actor.actorState.isGuarding = false;
        }
        currentBlockRemain = Random.Range(2, 5);
        // 后滚结束不开启格挡
    }

    private void UpdateBackRollMovement()
    {
        if (!isBackRolling) return;
        float animationMove = animator.GetFloat("AnimationMove");
        float moveAmount = animationMove * backRollConfig.backRollSpeed * Time.deltaTime;
        moveAmount *= backRollConfig.animationMoveMultiplier;

        Vector3 delta = backRollDirection * moveAmount;
        delta.y = backRollConfig.applyY ? delta.y * backRollConfig.verticalMultiplier : 0f;

        if (backRollConfig.useCharacterController && characterController != null && characterController.enabled)
            characterController.Move(delta);
        else
            transform.position += delta;

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            navAgent.nextPosition = transform.position;
    }

    private void StopNavAgent()
    {
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
            navAgent.nextPosition = transform.position;
        }
    }

    private void ResumeNavAgent()
    {
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.Warp(transform.position);
            navAgent.isStopped = false;
            navAgent.nextPosition = transform.position;
        }
    }

    private void EndHitState()
    {
        if (actor) actor.actorState.isHit = false;
        StopTurningToTarget(); // 受击结束停止转向
        ResumeNavAgent();
    }

    private string GetParryStateName(string hitStateName)
    {
        return hitStateName switch
        {
            "Hit_D_Up" => "ParryF",
            "Hit_Up_Left" or "Hit_D_Left" or "Hit_H_Left" => "ParryL",
            "Hit_Up_Right" or "Hit_D_Right" or "Hit_H_Right" => "ParryR",
            _ => null
        };
    }

    private void StartParryHitStop()
    {
        if (hitConfig.parryHitStopDuration <= 0 || isParryHitStopping) return;
        if (parryHitStopCoroutine != null) StopCoroutine(parryHitStopCoroutine);
        parryHitStopCoroutine = StartCoroutine(HitStopParryCoroutine());
    }

    private IEnumerator HitStopParryCoroutine()
    {
        isParryHitStopping = true;
        Time.timeScale = hitConfig.parryHitStopTimeScale;
        yield return new WaitForSecondsRealtime(hitConfig.parryHitStopDuration);
        Time.timeScale = 1f;
        isParryHitStopping = false;
        parryHitStopCoroutine = null;
    }

    // 开始转向目标
    private void StartTurningToTarget(Transform target)
    {
        if (target == null) return;
        currentAttacker = target;
        isTurningToTarget = true;
    }

    // 停止转向
    private void StopTurningToTarget()
    {
        isTurningToTarget = false;
        currentAttacker = null;
    }

    private void OnDisable()
    {
        StopCurrentBlockWindow();
        CancelInvoke();
        StopTurningToTarget();
    }

    private void OnDestroy()
    {
        if (parryHitStopCoroutine != null) StopCoroutine(parryHitStopCoroutine);
        StopCurrentBlockWindow();
        CancelInvoke();
        StopTurningToTarget();
    }
}