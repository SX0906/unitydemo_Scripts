using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 行为树节点执行结果
/// </summary>
public enum BTNodeState
{
    Success,
    Failure,
    Running
}

/// <summary>
/// 行为树节点基类
/// </summary>
public abstract class BTNode
{
    public abstract BTNodeState Evaluate();
}

/// <summary>
/// 顺序节点：依次执行子节点，任一失败则返回失败，全部成功则返回成功
/// </summary>
public class BTSequenceNode : BTNode
{
    private readonly List<BTNode> children = new List<BTNode>();

    public BTSequenceNode(params BTNode[] nodes)
    {
        if (nodes != null)
            children.AddRange(nodes);
    }

    public void AddChild(BTNode node)
    {
        children.Add(node);
    }

    public override BTNodeState Evaluate()
    {
        foreach (BTNode child in children)
        {
            BTNodeState result = child.Evaluate();
            if (result != BTNodeState.Success)
                return result;
        }
        return BTNodeState.Success;
    }
}

/// <summary>
/// 选择节点：依次执行子节点，任一成功则返回成功，全部失败则返回失败
/// </summary>
public class BTSelectorNode : BTNode
{
    private readonly List<BTNode> children = new List<BTNode>();

    public BTSelectorNode(params BTNode[] nodes)
    {
        if (nodes != null)
            children.AddRange(nodes);
    }

    public void AddChild(BTNode node)
    {
        children.Add(node);
    }

    public override BTNodeState Evaluate()
    {
        foreach (BTNode child in children)
        {
            BTNodeState result = child.Evaluate();
            if (result != BTNodeState.Failure)
                return result;
        }
        return BTNodeState.Failure;
    }
}

/// <summary>
/// 取反装饰节点：将子节点的 Success/Failure 结果取反，Running 保持不变
/// </summary>
public class BTInverterNode : BTNode
{
    private readonly BTNode child;

    public BTInverterNode(BTNode child)
    {
        this.child = child;
    }

    public override BTNodeState Evaluate()
    {
        BTNodeState result = child.Evaluate();
        switch (result)
        {
            case BTNodeState.Success: return BTNodeState.Failure;
            case BTNodeState.Failure: return BTNodeState.Success;
            default: return result;
        }
    }
}

/// <summary>
/// 条件节点：通过委托判断条件是否成立
/// </summary>
public class BTConditionNode : BTNode
{
    private readonly Func<bool> condition;

    public BTConditionNode(Func<bool> condition)
    {
        this.condition = condition;
    }

    public override BTNodeState Evaluate()
    {
        return (condition != null && condition.Invoke()) ? BTNodeState.Success : BTNodeState.Failure;
    }
}

/// <summary>
/// 行为节点：通过委托执行行为，返回执行结果
/// </summary>
public class BTActionNode : BTNode
{
    private readonly Func<BTNodeState> action;

    public BTActionNode(Func<BTNodeState> action)
    {
        this.action = action;
    }

    public override BTNodeState Evaluate()
    {
        return action != null ? action.Invoke() : BTNodeState.Failure;
    }
}

/// <summary>
/// 行为树黑板：在BT节点间共享数据
/// </summary>
public class BTBlackboard
{
    /// <summary>行为树期望设置的FSM目标状态</summary>
    public EnemyStateType desiredState;

    /// <summary>是否由BT控制了状态切换（false时BT不干预FSM，如受击期间）</summary>
    public bool btControlEnabled = true;
}

/// <summary>
/// 行为树运行器：持有根节点，每帧调用Evaluate驱动决策
/// </summary>
public class BehaviorTree
{
    private readonly BTNode root;
    public readonly BTBlackboard blackboard;

    public BehaviorTree(BTNode root)
    {
        this.root = root;
        this.blackboard = new BTBlackboard();
    }

    /// <summary>
    /// 每帧调用，从根节点开始评估行为树
    /// </summary>
    public void Tick()
    {
        root?.Evaluate();
    }
}

/// <summary>
/// 敌人行为树：完整的敌人行为树实现（包含所有节点定义）
/// 
/// 架构说明：行为树(BT)作为决策层 → 驱动有限状态机(FSM)状态切换
/// - BT负责根据环境/条件做出逻辑决策（攻击/追击/待机）
/// - FSM负责具体状态的执行逻辑（攻击/移动/待机的实际表现）
/// 
/// 行为树逻辑结构：
///   Root (Selector)
///   ├── Sequence: "攻击" (最高优先级)
///   │   ├── Condition: 是否在地面
///   │   ├── Condition: 是否检测到目标
///   │   └── Action: 距离≤2m → 设置目标状态为ATTACK
///   ├── Sequence: "追击"
///   │   ├── Condition: 是否在地面
///   │   └── Action: 检测到目标 → 设置目标状态为MOVE
///   └── Action: 设置目标状态为IDLE（默认行为）
/// </summary>
public class EnemyFSMBT
{
    public BehaviorTree BuildTree(EnemyFSM enemyFSM)
    {
        EnemySkillManager skillMgr = enemyFSM.GetComponent<EnemySkillManager>();

        // ===== 默认：待机 =====
        BTActionNode actionIdle = new BTActionNode(() =>
        {
            enemyFSM.BT_SetDesiredState(EnemyStateType.IDLE);
            return BTNodeState.Success;
        });

        // ===== ★ 攻击序列：有目标 + 技能可用且距离够 → ATTACK =====
        BTSequenceNode attackSequence = new BTSequenceNode();

        attackSequence.AddChild(new BTConditionNode(() => enemyFSM.IsGrounded));

        attackSequence.AddChild(new BTConditionNode(() =>
        {
            return skillMgr != null
                && enemyFSM.hasTarget
                && enemyFSM.targetPlayer != null
                && skillMgr.HasAvailableSkill(enemyFSM.transform, enemyFSM.targetPlayer);
        }));

        attackSequence.AddChild(new BTActionNode(() =>
        {
            EnemySkillData skill = skillMgr.GetAvailableSkill(
                enemyFSM.transform, enemyFSM.targetPlayer);
            if (skill == null) return BTNodeState.Failure;

            if (!EnemyCombatCoordinator.TryAcquireAttackSlot(enemyFSM))
                return BTNodeState.Failure;

            skillMgr.StartCast(skill, enemyFSM.targetPlayer);
            enemyFSM.GetAttackState()?.SetAttackerAndSkill(
                enemyFSM.targetPlayer, skill);
            enemyFSM.DirectSetState(EnemyStateType.ATTACK);
            return BTNodeState.Success;
        }));

        // ===== ★ 追击序列：有目标 + 有技能但距离不够 → MOVE 靠近 =====
        BTSequenceNode chaseSequence = new BTSequenceNode();

        chaseSequence.AddChild(new BTConditionNode(() => enemyFSM.IsGrounded));

        chaseSequence.AddChild(new BTConditionNode(() =>
        {
            return skillMgr != null
                && enemyFSM.hasTarget
                && enemyFSM.targetPlayer != null
                && skillMgr.HasAnySkillReadyIgnoreDistance(
                    enemyFSM.transform, enemyFSM.targetPlayer);
        }));

        chaseSequence.AddChild(new BTActionNode(() =>
        {
            if (!enemyFSM.hasTarget)
                return BTNodeState.Failure;

            // 攻击名额已满时，其余敌人进入锁定走位围观，不再冲上去打
            enemyFSM.BT_SetDesiredState(
                EnemyCombatCoordinator.AttackSlotsFull
                    ? EnemyStateType.LOCK_MOVE
                    : EnemyStateType.MOVE);
            return BTNodeState.Success;
        }));

        // ===== ★ 锁定移动序列：有目标 + 无任何可用技能 → LOCK_MOVE =====
        BTSequenceNode lockMoveSequence = new BTSequenceNode();

        lockMoveSequence.AddChild(new BTConditionNode(() => enemyFSM.IsGrounded));

        lockMoveSequence.AddChild(new BTActionNode(() =>
        {
            if (enemyFSM.hasTarget)
            {
                enemyFSM.BT_SetDesiredState(EnemyStateType.LOCK_MOVE);
                return BTNodeState.Success;
            }
            return BTNodeState.Failure;
        }));

        // ===== 根选择：攻击 > 追击 > 锁定移动 > 待机 =====
        BTSelectorNode root = new BTSelectorNode(
            attackSequence, chaseSequence, lockMoveSequence, actionIdle);

        return new BehaviorTree(root);
    }
}
