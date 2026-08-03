using UnityEngine;
using System.Collections;

public interface IExecutionService
{
    bool CanExecute(GameObject target);
    bool TryStartExecution(GameObject attackerObj, GameObject victimObj);
    bool IsExecuting { get; }
}

public class ExecutionManager_test : MonoBehaviour, IExecutionService
{
    public ExecutionConfig_test config = new ExecutionConfig_test();
    public bool isExecuting;
    public static ExecutionManager_test Instance { get; private set; }

    private ActorBase attacker;
    private ActorBase victim;
    private Animator attackerAnimator;
    private Animator victimAnimator;
    private Coroutine executionCoroutine;

    public bool IsExecuting => isExecuting;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool CanExecute(GameObject target)
    {
        HealthSystem health = target.GetComponent<HealthSystem>();
        if (health == null || health.IsDead) return false;
        return health.GetHealthPercent() <= config.executionHealthPercent;
    }

    public bool TryStartExecution(GameObject attackerObj, GameObject victimObj)
    {
        if (!CanExecute(victimObj) || isExecuting) return false;
        StartExecutionInternal(attackerObj, victimObj);
        return true;
    }

    private void StartExecutionInternal(GameObject attackerObj, GameObject victimObj)
    {
        attacker = attackerObj.GetComponent<ActorBase>();
        victim = victimObj.GetComponent<ActorBase>();
        attackerAnimator = attackerObj.GetComponentInChildren<Animator>();
        victimAnimator = victimObj.GetComponentInChildren<Animator>();
        if (attacker == null || victim == null || attackerAnimator == null || victimAnimator == null) return;
        isExecuting = true;
        attacker.actorState.isAttacking = false;
        attacker.actorState.isHit = false;
        victim.actorState.isAttacking = false;
        victim.actorState.isHit = true;
        PositionForExecution();
        PlayExecutionAnimations();
        executionCoroutine = StartCoroutine(ExecutionSequence());
    }

    private void PositionForExecution()
    {
        if (attacker == null || victim == null) return;
        Vector3 targetPos = attacker.transform.position + attacker.transform.forward * config.executionDistance;
        victim.transform.position = targetPos;
        Vector3 lookDir = attacker.transform.position - victim.transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
            victim.transform.rotation = Quaternion.LookRotation(lookDir);
    }

    private void PlayExecutionAnimations()
    {
        if (attackerAnimator != null)
            attackerAnimator.CrossFadeInFixedTime(config.executionStateName, config.crossFadeTime, 0, 0f);
        if (victimAnimator != null)
            victimAnimator.CrossFadeInFixedTime(config.executionHitStateName, config.crossFadeTime, 0, 0f);
    }

    private IEnumerator ExecutionSequence() { yield return new WaitForSeconds(2f); EndExecution(); }
    public void EndExecution() { isExecuting = false; if (executionCoroutine != null) { StopCoroutine(executionCoroutine); executionCoroutine = null; } }
}

[System.Serializable]
public class ExecutionConfig_test
{
    [Range(0.01f, 1f)] public float executionHealthPercent = 0.08f;
    public float enemyExecutionDistance = 2f;
    public float executionDistance = -0.8f;
    public string executionStateName = "Execution";
    public string executionHitStateName = "ExecutionHit";
    public string executionHitTag = "ExecutionHit";
    public int animatorLayer = 0;
    public float crossFadeTime = 0.1f;
}
