using System;
using UnityEngine;

/// <summary>
/// 玩家状态基类。转换评估只读取事实，副作用由 Enter、Tick、Exit 执行。
/// </summary>
public abstract class PlayerStateBase
{
    protected readonly PlayerContext Context;

    protected PlayerStateBase(PlayerContext context)
    {
        Context = context;
    }

    public virtual PlayerStateTransitionRequest EvaluateInputTransition()
    {
        return null;
    }

    public virtual void Enter(PlayerStateTransition transition)
    {
    }

    public virtual void Tick(float deltaTime, ref PlayerGameplayIntent intent)
    {
    }

    public virtual PlayerStateTransitionRequest EvaluateResultTransition()
    {
        return null;
    }

    public virtual void Exit(PlayerStateTransition transition)
    {
    }

    public abstract PlayerLocomotionMode LocomotionMode { get; }
    public virtual float PresentationProgress => 0f;

    protected Type ResolveGroundStateType()
    {
        if (Context.InputSource.MoveInput == Vector2.zero)
        {
            return typeof(PlayerIdleState);
        }

        return Context.InputSource.IsWalkMode ? typeof(PlayerWalkState) : typeof(PlayerRunState);
    }
}
