using System;
using UnityEngine;
/// <summary>
/// 状态基类，存在进入退出，状态锁和引用容器
/// </summary>
public abstract class PlayerStateBase
{
    //获取所需引用，readonly防止运行时修改context引用
    protected readonly PlayerContext Context;
    //在被创建时构造函数自动拿到引用
    protected PlayerStateBase(PlayerContext context)
    {
        Context = context;
    }
    //状态运行时
    public virtual void Enter(){}
    public virtual void Exit(){}
    public virtual void Tick(){}
    public Type GetNextStateType()
    {
        if (!CanExit())
        {
            return null;
        }

        return EvaluateNextStateType();
    }
    protected abstract Type EvaluateNextStateType();
    //状态锁，具体逻辑具体脚本内处理
    public virtual bool CanExit() { return false; }
}
