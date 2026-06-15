using System.Collections.Generic;
using UnityEngine;

public enum EnemyStateType
{
    IDLE,
    MOVE,
    HIT,
    AIR_HIT,
    BLOCK,
    DODGE,
    ATTACK,
    GETUP,
    BLOCKBREAK,
    FALLTOFLOOR,
    KNOCKDOWN,
    LOCK_MOVE,
    DEATH
}

public class EnemyFSMControl
{
    private EnemyStateBase currentState;
    public EnemyStateType stateType;
    private Dictionary<EnemyStateType, EnemyStateBase> allStates;

    public EnemyFSMControl()
    {
        allStates = new Dictionary<EnemyStateType, EnemyStateBase>();
    }

    public void OnTick()
    {
        currentState?.OnUpdate();
    }

    public void AddState(EnemyStateType stateType, EnemyStateBase state)
    {
        if (allStates.ContainsKey(stateType)) return;
        allStates.Add(stateType, state);
    }

    public void SetState(EnemyStateType stateType)
    {
        if (currentState == allStates[stateType])
            return;
        currentState?.OnExit();
        currentState = allStates[stateType];
        this.stateType = stateType;
        currentState.OnEnter();
    }

    public T GetState<T>(EnemyStateType type) where T : EnemyStateBase
    {
        allStates.TryGetValue(type, out var state);
        return state as T;
    }
}
