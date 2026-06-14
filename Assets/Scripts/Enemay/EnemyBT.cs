using System;
using System.Collections.Generic;

public enum EnemyBTState
{
    Success,
    Failure,
    Running
}

public abstract class EnemyBTNode
{
    public abstract EnemyBTState Tick();
}

public class EnemyBTConditionNode : EnemyBTNode
{
    private readonly Func<bool> conditionFunc;

    public EnemyBTConditionNode(Func<bool> conditionFunc)
    {
        this.conditionFunc = conditionFunc;
    }

    public override EnemyBTState Tick()
    {
        if (conditionFunc != null && conditionFunc.Invoke())
            return EnemyBTState.Success;
        else
            return EnemyBTState.Failure;
    }
}

public class EnemyBTActionNode : EnemyBTNode
{
    private readonly Func<EnemyBTState> actionFunc;

    public EnemyBTActionNode(Func<EnemyBTState> actionFunc)
    {
        this.actionFunc = actionFunc;
    }

    public override EnemyBTState Tick()
    {
        if (actionFunc != null)
            return actionFunc.Invoke();
        else
            return EnemyBTState.Failure;
    }
}

public class EnemyBTSequenceNode : EnemyBTNode
{
    private readonly List<EnemyBTNode> nodeList = new List<EnemyBTNode>();

    public EnemyBTSequenceNode(params EnemyBTNode[] nodes)
    {
        if (nodes != null)
            nodeList.AddRange(nodes);
    }

    public override EnemyBTState Tick()
    {
        foreach (EnemyBTNode node in nodeList)
        {
            EnemyBTState result = node.Tick();
            if (result != EnemyBTState.Success)
                return result;
        }
        return EnemyBTState.Success;
    }
}

public class EnemyBTSelectorNode : EnemyBTNode
{
    private readonly List<EnemyBTNode> nodeList = new List<EnemyBTNode>();

    public EnemyBTSelectorNode(params EnemyBTNode[] nodes)
    {
        if (nodes != null)
            nodeList.AddRange(nodes);
    }

    public override EnemyBTState Tick()
    {
        foreach (EnemyBTNode node in nodeList)
        {
            EnemyBTState result = node.Tick();
            if (result != EnemyBTState.Failure)
                return result;
        }
        return EnemyBTState.Failure;
    }
}
