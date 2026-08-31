using System;

/// <summary>
/// 玩家状态基类。转换评估只读取事实，副作用由 Enter、Tick、Exit 执行
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
        switch (Context.TargetGroundMode)
        {
            case PlayerLocomotionMode.Walk: return typeof(PlayerWalkState);
            case PlayerLocomotionMode.Run: return typeof(PlayerRunState);
            case PlayerLocomotionMode.FastRun: return typeof(PlayerFastRunState);
            default: return typeof(PlayerIdleState);
        }
    }
}
