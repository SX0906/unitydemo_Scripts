using UnityEngine;
using System.Collections;

[System.Serializable]
public class ExecutionConfig
{
    [Header("处决阈值（%）")]
    [Range(0.01f, 1f)]
    public float executionHealthPercent = 0.08f; // 8%

    [Header("敌人发起处决距离")]
    public float enemyExecutionDistance = 2f; // 敌人离玩家2米内即可处决

    [Header("处决间隔距离")]
    public float executionDistance = -0.8f; // 处决时双方的距离

    [Header("处决动画状态名")]
    public string executionStateName = "Execution";
    public string executionHitStateName = "ExecutionHit";

    [Header("被处决动画Tag")]
    public string executionHitTag = "ExecutionHit";

    [Header("动画层级")]
    public int animatorLayer = 0;

    [Header("过渡时间")]
    public float crossFadeTime = 0.1f;
}

public class ExecutionManager : MonoBehaviour
{
    [Header("处决配置")]
    public ExecutionConfig config = new ExecutionConfig();

    [Header("状态")]
    public bool isExecuting;

    public static ExecutionManager Instance { get; private set; }

    private ActorBase attacker;
    private ActorBase victim;
    private Animator attackerAnimator;
    private Animator victimAnimator;

    private Coroutine executionCoroutine;

    public bool IsExecuting => isExecuting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public static bool CanExecute(GameObject target)
    {
        if (Instance == null) return false;
        
        HealthSystem health = target.GetComponent<HealthSystem>();
        if (health == null) return false;
        
        // 确保目标还活着
        if (health.IsDead) return false;

        return health.GetHealthPercent() <= Instance.config.executionHealthPercent;
    }

    public static bool TryStartExecution(GameObject attackerObj, GameObject victimObj)
    {
        if (Instance == null) return false;
        if (!CanExecute(victimObj)) return false;
        if (Instance.isExecuting) return false;

        Instance.StartExecutionInternal(attackerObj, victimObj);
        return true;
    }

    private void StartExecutionInternal(GameObject attackerObj, GameObject victimObj)
    {
        attacker = attackerObj.GetComponent<ActorBase>();
        victim = victimObj.GetComponent<ActorBase>();
        attackerAnimator = attackerObj.GetComponentInChildren<Animator>();
        victimAnimator = victimObj.GetComponentInChildren<Animator>();

        if (attacker == null || victim == null || attackerAnimator == null || victimAnimator == null)
        {
            Debug.LogWarning("处决缺少必要组件！");
            return;
        }

        isExecuting = true;

        // 禁用攻击者的行为
        DisableAttacker(attacker);

        // 禁用受害者的行为 - 打断任何动作
        DisableVictim(victim);

        // 开始处决双方的位置
        PositionForExecution();

        // 播放动画
        PlayExecutionAnimations();

        // 开始时间停止
        StartExecutionCoroutine();
    }

    private void DisableAttacker(ActorBase attackerActor)
    {
        if (attackerActor == null) return;

        // 禁用攻击方状态
        attackerActor.actorState.isAttacking = false;
        attackerActor.actorState.isHit = false;
        attackerActor.actorState.isGuarding = false;

        // 禁用攻击者相关脚本
        var attackerCombatEnemy = attackerActor.GetComponent<EnemyCombatController>();
        var attackerCombatPlayer = attackerActor.GetComponent<PlayerCombatController>();
        var attackerSkillEnemy = attackerActor.GetComponent<EnemyAttackSkillPlayer>();
        var attackerSkillPlayer = attackerActor.GetComponent<PlayerSkillPlayer>();

        if (attackerCombatEnemy != null)
            attackerCombatEnemy.enabled = false;
        if (attackerCombatPlayer != null)
            attackerCombatPlayer.enabled = false;
        if (attackerSkillEnemy != null)
        {
            attackerSkillEnemy.CancelSkill(false);
            attackerSkillEnemy.enabled = false; // 完全禁用技能系统
        }
        if (attackerSkillPlayer != null)
        {
            attackerSkillPlayer.CancelSkill(false);
            attackerSkillPlayer.enabled = false;
        }
    }

    private void DisableVictim(ActorBase victimActor)
    {
        if (victimActor == null) return;

        // 禁用受害者状态 - 打断所有动作
        victimActor.actorState.isAttacking = false;
        victimActor.actorState.isHit = false;
        victimActor.actorState.isGuarding = false;

        // 禁用受害者相关脚本
        var victimCombatEnemy = victimActor.GetComponent<EnemyCombatController>();
        var victimCombatPlayer = victimActor.GetComponent<PlayerCombatController>();
        var victimSkillEnemy = victimActor.GetComponent<EnemyAttackSkillPlayer>();
        var victimSkillPlayer = victimActor.GetComponent<PlayerSkillPlayer>();
        var victimMoveEnemy = victimActor.GetComponent<EnemyMoveControllerBT>();
        var victimMovePlayer = victimActor.GetComponent<PlayerMoveController>();
        var victimHealthEnemy = victimActor.GetComponent<EnemyHealthController>();
        var victimHealthPlayer = victimActor.GetComponent<PlayerHealthController>();

        // 禁用战斗和技能控制器
        if (victimCombatEnemy != null)
            victimCombatEnemy.enabled = false;
        if (victimCombatPlayer != null)
            victimCombatPlayer.enabled = false;
        if (victimSkillEnemy != null)
            victimSkillEnemy.CancelSkill(false);
        if (victimSkillPlayer != null)
            victimSkillPlayer.CancelSkill(false);

        // 禁用移动控制器
        if (victimMoveEnemy != null)
            victimMoveEnemy.enabled = false;
        if (victimMovePlayer != null)
            victimMovePlayer.enabled = false;

        // 禁用健康控制器（防止被处决过程中被再次攻击）
        if (victimHealthEnemy != null)
            victimHealthEnemy.enabled = false;
        if (victimHealthPlayer != null)
            victimHealthPlayer.enabled = false;

        // 禁用 NavMeshAgent
        var navAgent = victimActor.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        // 禁用 CharacterController
        var charController = victimActor.GetComponent<CharacterController>();
        if (charController != null)
        {
            charController.enabled = false;
        }
    }

    private void PositionForExecution()
    {
        if (attacker == null || victim == null)
            return;

        // 让攻击者面向受害者
        Vector3 direction = victim.transform.position - attacker.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            attacker.transform.rotation = Quaternion.LookRotation(direction);
        }

        // 让受害者面向攻击者
        Vector3 reverseDirection = attacker.transform.position - victim.transform.position;
        reverseDirection.y = 0f;
        if (reverseDirection.sqrMagnitude > 0.01f)
        {
            victim.transform.rotation = Quaternion.LookRotation(reverseDirection);
        }

        // 调整位置，让两者靠近
        Vector3 targetPos = attacker.transform.position + attacker.transform.forward * config.executionDistance;
        victim.transform.position = new Vector3(targetPos.x, victim.transform.position.y, targetPos.z);
    }

    private AnimatorUpdateMode originalAttackerUpdateMode;
    private AnimatorUpdateMode originalVictimUpdateMode;

    private void PlayExecutionAnimations()
    {
        if (attackerAnimator == null || victimAnimator == null)
            return;

        // 保存原始更新模式
        originalAttackerUpdateMode = attackerAnimator.updateMode;
        originalVictimUpdateMode = victimAnimator.updateMode;

        // 设置为不受时间缩放影响
        attackerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        victimAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        // 直接播放攻击者的处决动画，不使用技能系统，直接切换
        attackerAnimator.Play(config.executionStateName, config.animatorLayer, 0f);
        attackerAnimator.Update(0f);

        // 直接播放受害者的被处决动画
        victimAnimator.Play(config.executionHitStateName, config.animatorLayer, 0f);
        victimAnimator.Update(0f);

        // 播放受击音效
        victim?.GetComponentInChildren<CombatAudioPlayer>()?.PlayHitSound();
    }

    private void StartExecutionCoroutine()
    {
        executionCoroutine = StartCoroutine(ExecutionCoroutine());
    }

    private IEnumerator ExecutionCoroutine()
    {
        // 停止游戏时间
        Time.timeScale = 0f;

        // 等待动画完成（使用真实时间）
        while (isExecuting)
        {
            yield return null;

            // 检查动画状态
            AnimatorStateInfo attackerState = attackerAnimator.GetCurrentAnimatorStateInfo(config.animatorLayer);
            AnimatorStateInfo victimState = victimAnimator.GetCurrentAnimatorStateInfo(config.animatorLayer);

            // 检查被处决动画是否播放完成（通过 tag 检测）
            bool victimFinished = victimState.IsTag(config.executionHitTag) && victimState.normalizedTime >= 0.95f;

            // 被处决动画播放完才结束
            if (victimFinished)
            {
                break;
            }
        }

        // 结束处决
        EndExecution();
    }

    private void EndExecution()
    {
        if (!isExecuting) return;

        isExecuting = false;

        // 恢复游戏时间
        Time.timeScale = 1f;

        // 恢复Animator更新模式
        if (attackerAnimator != null)
            attackerAnimator.updateMode = originalAttackerUpdateMode;
        if (victimAnimator != null)
            victimAnimator.updateMode = originalVictimUpdateMode;

        // 让受害者真正死亡 - 只保留模型
        MakeActorDead(victim);

        // 完全恢复攻击者
        FullyResumeActor(attacker);

        // 清除锁定目标（如果玩家锁定了受害者）
        if (attacker != null)
        {
            PlayerMoveController playerMove = attacker.GetComponent<PlayerMoveController>();
            if (playerMove != null && victim != null)
            {
                playerMove.ClearLockTarget();
            }
        }

        attacker = null;
        victim = null;
        attackerAnimator = null;
        victimAnimator = null;
        executionCoroutine = null;
    }

    private void MakeActorDead(ActorBase actor)
    {
        if (actor == null) return;

        // 禁用所有行为脚本
        MonoBehaviour[] allScripts = actor.GetComponents<MonoBehaviour>();
        foreach (var script in allScripts)
        {
            if (script != this && !(script is ExecutionManager))
            {
                script.enabled = false;
            }
        }

        // 禁用碰撞体
        Collider[] colliders = actor.GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }

        // 禁用 CharacterController
        CharacterController charController = actor.GetComponent<CharacterController>();
        if (charController != null)
            charController.enabled = false;

        // 禁用 NavMeshAgent
        UnityEngine.AI.NavMeshAgent navAgent = actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
            navAgent.enabled = false;

        // 设置死亡状态
        actor.actorState.isAttacking = false;
        actor.actorState.isHit = false;
        actor.actorState.isGuarding = false;

        // 让受害者直接死亡（如果还有生命）
        HealthSystem health = actor.GetComponent<HealthSystem>();
        if (health != null && !health.IsDead)
        {
            health.TakeDamage(health.maxHealth);
        }
    }

    private void FullyResumeActor(ActorBase actor)
    {
        if (actor == null) return;

        // 恢复状态
        actor.actorState.isAttacking = false;
        actor.actorState.isHit = false;
        actor.actorState.isGuarding = false;

        // 恢复战斗控制器
        var combatEnemy = actor.GetComponent<EnemyCombatController>();
        var combatPlayer = actor.GetComponent<PlayerCombatController>();

        if (combatEnemy != null)
            combatEnemy.enabled = true;
        if (combatPlayer != null)
            combatPlayer.enabled = true;

        // 恢复移动控制器
        PlayerMoveController playerMove = actor.GetComponent<PlayerMoveController>();
        if (playerMove != null)
            playerMove.enabled = true;

        // 恢复技能控制器
        EnemyAttackSkillPlayer enemySkill = actor.GetComponent<EnemyAttackSkillPlayer>();
        PlayerSkillPlayer playerSkill = actor.GetComponent<PlayerSkillPlayer>();

        if (enemySkill != null)
        {
            enemySkill.enabled = true;
            enemySkill.CancelSkill(false);
        }
        if (playerSkill != null)
        {
            playerSkill.enabled = true;
            playerSkill.CancelSkill(false);
        }

        // 恢复健康控制器
        EnemyHealthController enemyHealth = actor.GetComponent<EnemyHealthController>();
        PlayerHealthController playerHealth = actor.GetComponent<PlayerHealthController>();

        if (enemyHealth != null)
            enemyHealth.enabled = true;
        if (playerHealth != null)
            playerHealth.enabled = true;

        // 恢复 NavMeshAgent
        UnityEngine.AI.NavMeshAgent navAgent = actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.Warp(actor.transform.position);
        }

        // 恢复 CharacterController
        CharacterController charController = actor.GetComponent<CharacterController>();
        if (charController != null)
            charController.enabled = true;
    }

    public static void ForceStopExecution()
    {
        if (Instance != null && Instance.isExecuting)
        {
            Instance.EndExecution();
        }
    }
}
