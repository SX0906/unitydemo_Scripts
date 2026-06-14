using UnityEngine.InputSystem;
using UnityEngine;
using System.Collections;

[System.Serializable]
public class PlayerHitConfig
{
    public float crossFadeTime = 0.02f;
    public string hitStatePrefix = "Base Layer.Katana.Hit.";
    public string parryStatePrefix = "Base Layer.Katana.Hit.Parry.";

    [Tooltip("受击动画播放进度不超过此值时允许触发 Parry")]
    public float parryWindowNormalizedTime = 0.15f;

    [Header("弹刀顿帧")]
    public float parryHitStopDuration = 0.15f;
    public float parryHitStopTimeScale = 0.1f;

    [Header("受击转向设置")]
    [Tooltip("受击时转向攻击者的速度(度/秒)")]
    public float hitTurnSpeed = 120f;

    [Header("格挡范围设置")]
    [Tooltip("正面可格挡的角度范围(度)")]
    public float blockAngleRange = 120f;

    [Header("连续弹刀设置")]
    [Tooltip("弹刀后多久重置弹刀状态（秒）")]
    public float parryResetDelay = 0.1f;

    [Header("受击后格挡窗口")]
    [Tooltip("受击后多久内按格挡仍能触发弹刀（秒）")]
    public float parryInputWindow = 0.25f;
}

public class PlayerHealthController : MonoBehaviour, IHitReceiver
{
    [Header("玩家受击参数")]
    public PlayerHitConfig hitConfig = new PlayerHitConfig();

    [Header("敌方技能格挡适配")]
    [Tooltip("是否检测敌人当前释放的技能是否允许被格挡")]
    public bool checkEnemySkillBlockable = true;

    [Tooltip("找不到敌人技能信息时，是否默认允许格挡")]
    public bool defaultHitCanBeBlocked = true;

    [Tooltip("检测附近敌人技能的范围")]
    public float enemySkillDetectRadius = 4f;

    [Tooltip("敌人 Layer，用于检测正在释放技能的敌人")]
    public LayerMask enemyLayerMask;

    private ActorBase actor;
    private Animator animator;
    private PlayerCombatController combat;
    // 【新增】引用移动控制器（判断无敌）
    private PlayerMoveController moveController;
    private PlayerSkillPlayer skillPlayer;

    private int hitLayerIndex = 0;
    private string currentHitStateName;
    private bool currentHitCanBeBlocked = true;
    private bool isGuarding;
    private bool hasParried;
    private Coroutine parryHitStopCoroutine;
    private bool isParryHitStopping;
    private bool isTurningToTarget;
    private Transform currentAttacker;
    private float lastParryTime;
    private float lastHitTime;

    private void Awake()
    {
        actor = GetComponent<ActorBase>();
        animator = GetComponentInChildren<Animator>();
        combat = GetComponent<PlayerCombatController>();
        // 【新增】获取移动控制器
        moveController = GetComponent<PlayerMoveController>();
        skillPlayer = GetComponent<PlayerSkillPlayer>();
    }

    private void Update()
    {
        if (!actor) return;
        CheckGuardInput();
        UpdateParryWindow();
        UpdateTurnToTarget();
        CheckResetParryState();
    }

    private void CheckGuardInput()
    {
        if (Keyboard.current.gKey.isPressed)
        {
            EnterGuard();
            
            // 在格挡窗口期内按格挡，触发弹刀
            if (actor.actorState.isHit && !hasParried && 
                currentHitCanBeBlocked && 
                IsAttackerInFrontAngle(currentAttacker) &&
                Time.time - lastHitTime <= hitConfig.parryInputWindow)
            {
                ExecuteParry(currentHitStateName);
            }
        }
        else
        {
            ExitGuard();
        }
    }

    private void CheckResetParryState()
    {
        if (hasParried && Time.time >= lastParryTime + hitConfig.parryResetDelay)
        {
            hasParried = false;
        }
    }

    private void UpdateTurnToTarget()
    {
        if (!isTurningToTarget || currentAttacker == null) return;

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

        if (Quaternion.Angle(transform.rotation, targetRotation) < 1f)
        {
            isTurningToTarget = false;
        }
    }

    private bool IsAttackerInFrontAngle(Transform attacker)
    {
        if (attacker == null) return false;

        Vector3 directionToAttacker = attacker.position - transform.position;
        directionToAttacker.y = 0f;
        directionToAttacker.Normalize();

        float angle = Vector3.Angle(transform.forward, directionToAttacker);
        return angle <= hitConfig.blockAngleRange / 2f;
    }

    private void UpdateParryWindow()
    {
        if (!actor.actorState.isHit || string.IsNullOrEmpty(currentHitStateName))
        {
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(hitLayerIndex);
        bool isHitTag = stateInfo.IsTag("Hit");

        if (animator.IsInTransition(hitLayerIndex))
        {
            AnimatorStateInfo nextStateInfo = animator.GetNextAnimatorStateInfo(hitLayerIndex);
            if (nextStateInfo.IsTag("Hit"))
            {
                isHitTag = true;
            }
        }

        if (!isHitTag)
        {
            actor.actorState.isHit = false;
            currentHitStateName = null;
            currentHitCanBeBlocked = true;
            StopTurningToTarget();
        }
    }

    public void ReceiveHit(string hitStateName, float damage)
    {
        // 检查霸体状态
        if (skillPlayer != null && skillPlayer.IsUsingSkill && skillPlayer.IsSuperArmorActive)
        {
            // 霸体状态下：只受伤害，不播放受击动画
            HealthSystem healthSystem = GetComponent<HealthSystem>();
            if (healthSystem != null)
            {
                healthSystem.TakeDamage(damage);
            }
            GetComponent<CombatAudioPlayer>()?.PlayHitSound();
            return;
        }
        
        if (moveController != null && moveController.IsInvincible())
        {
            moveController.TriggerDodgeSlowMotion();
            return;
        }

        if (!actor || !animator || string.IsNullOrEmpty(hitStateName)) return;

        StopTurningToTarget();
        EnemyAttackSkillPlayer attackingEnemy = FindCurrentAttackingEnemy();
        currentAttacker = attackingEnemy?.transform;
        currentHitCanBeBlocked = CanCurrentIncomingHitBeBlocked(hitStateName, attackingEnemy);

        combat?.CancelAttack();

        // 如果已经提前按了格挡，立即触发弹刀
        if (isGuarding && currentHitCanBeBlocked && IsAttackerInFrontAngle(currentAttacker))
        {
            ExecuteParry(hitStateName);
            return;
        }

        // 记录被攻击时间，开启格挡窗口期
        lastHitTime = Time.time;

        actor.actorState.isHit = true;
        actor.actorState.isAttacking = false;
        currentHitStateName = hitStateName;
        hasParried = false;

        string fullPath = hitConfig.hitStatePrefix + hitStateName;
        animator.CrossFadeInFixedTime(fullPath, hitConfig.crossFadeTime);
        GetComponent<CombatAudioPlayer>()?.PlayHitSound();

        HealthSystem health = GetComponent<HealthSystem>();
        if (health != null)
        {
            health.TakeDamage(damage);
        }

        if (currentAttacker != null) StartTurningToTarget(currentAttacker);
    }

    public void OnGuard()
    {
        // 这个方法现在主要由 CheckGuardInput 在 Update 中处理
        // 这里主要用于外部脚本调用
    }

    private void EnterGuard()
    {
        isGuarding = true;
        animator.SetBool("Guard", true);
    }

    private void ExitGuard()
    {
        isGuarding = false;
        animator.SetBool("Guard", false);
    }

    private void ExecuteParry(string hitStateName)
    {
        if (!currentHitCanBeBlocked) return;
        
        hasParried = true;
        lastParryTime = Time.time;
        StopTurningToTarget();

        string parryStateName = GetParryStateName(hitStateName);
        if (string.IsNullOrEmpty(parryStateName)) return;

        actor.actorState.isHit = false;
        string fullParryPath = hitConfig.parryStatePrefix + parryStateName;
        animator.CrossFadeInFixedTime(fullParryPath, 0f);
        GetComponent<CombatAudioPlayer>()?.PlayParrySound();
        StartParryHitStop();
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

    private bool CanCurrentIncomingHitBeBlocked(string hitStateName, EnemyAttackSkillPlayer attackingEnemy)
    {
        if (!checkEnemySkillBlockable) return true;
        if (attackingEnemy == null || attackingEnemy.CurrentSkill == null) return defaultHitCanBeBlocked;
        return attackingEnemy.CurrentSkill.canBeBlock;
    }

    private EnemyAttackSkillPlayer FindCurrentAttackingEnemy()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, enemySkillDetectRadius, enemyLayerMask);
        EnemyAttackSkillPlayer nearestEnemy = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (Collider col in cols)
        {
            EnemyAttackSkillPlayer skillPlayer = col.GetComponentInParent<EnemyAttackSkillPlayer>();
            if (skillPlayer == null || !skillPlayer.IsUsingSkill || skillPlayer.CurrentSkill == null) continue;

            Transform skillTarget = skillPlayer.CurrentSkillTarget;
            if (skillTarget != null && skillTarget != transform) continue;

            float sqrDistance = (skillPlayer.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearestEnemy = skillPlayer;
            }
        }
        return nearestEnemy;
    }

    private string GetParryStateName(string hitStateName)
    {
        return hitStateName switch
        {
            "Hit_D_Up" => "ParryF",
            "Hit_Up_Left" => "ParryL",
            "Hit_Up_Right" => "ParryR",
            "Hit_H_Left" => "ParryL",
            "Hit_H_Right" => "ParryR",
            "Hit_D_Left" => "ParryL",
            "Hit_D_Right" => "ParryR",
            _ => null
        };
    }

    private void StartTurningToTarget(Transform target)
    {
        if (target == null) return;
        currentAttacker = target;
        isTurningToTarget = true;
    }

    private void StopTurningToTarget()
    {
        isTurningToTarget = false;
        currentAttacker = null;
    }

    private void OnDisable()
    {
        StopTurningToTarget();
    }

    public bool IsGuarding()
    {
        return isGuarding;
    }
    
    private void OnDestroy()
    {
        if (parryHitStopCoroutine != null) StopCoroutine(parryHitStopCoroutine);
        StopTurningToTarget();
    }
}
