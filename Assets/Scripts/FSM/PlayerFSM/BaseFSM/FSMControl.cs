using System.Collections.Generic;
using UnityEngine;

public enum StateType
{
    IDlE,
    MOVE,
    ATTACK_01,
    ATTACK_02,
    LockOn,
    JUMP,
    ATTACK_UP,
    AIR_ATTACK,
    DODGE,
    HIT
}

public class FSMControl 
{
    //当前在运行的状态
    private StateBase currentstate;

    public StateType stateType;//各个状态的标识符

    //保存所有状态的容器
    private Dictionary<StateType,StateBase> allSaveState; 

    public FSMControl()
    {
        allSaveState = new Dictionary<StateType,StateBase>();//初始化容器   
    }

    public void OnTick()//
    {
        currentstate?.OnUpdate();
    }

    public void AddState(StateType stateType,StateBase state)
    {
        if (allSaveState.ContainsKey(stateType)) return;//判断当前状态容器已经包含这个状态，若已经包含则不添加

        allSaveState.Add(stateType, state);//添加一个新的状态
    }

    public void SetState(StateType stateType)//外部调用该函数，需要传一个新的状态
    {
        if(currentstate == allSaveState[stateType]) return;

        currentstate?.OnExit();//判断当前状态是否为空，不为空则执行
        currentstate = allSaveState[stateType];//将新的状态替换掉旧的状态
        this.stateType = stateType;//更新当前状态的标识符
        currentstate.OnEnter();
    }

    public StateBase GetCurrentState() => currentstate;//获取当前状态

    public T GetState<T>(StateType type) where T : StateBase => allSaveState.TryGetValue(type, out var s) ? s as T : null;
}
