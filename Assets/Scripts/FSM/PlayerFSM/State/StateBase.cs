using UnityEngine;

public abstract class StateBase
{
    /// <summary>
    /// 第一次进入状态
    /// </summary>
    public abstract void OnEnter();
    /// <summary>
    /// 持续处于当前状态
    /// </summary>
    public abstract void OnUpdate();
    /// <summary>
    /// 退出当前状态
    /// </summary>
    public abstract void OnExit();
   
}
