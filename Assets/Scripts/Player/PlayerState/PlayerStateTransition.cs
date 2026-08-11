using System;

public enum PlayerStateTransitionReason
{
    Initialized,
    StartedMoving,
    StoppedMoving,
    Accelerated,
    Decelerated,
    DodgeStarted,
    DodgeCompleted,
    Jumped,
    Fell,
    Landed,
    HardLanded,
    HardLandingRecovered
}

/// <summary>
/// 状态控制器已经接受并执行的转换事实
/// </summary>
public readonly struct PlayerStateTransition
{
    public PlayerStateTransition(Type previousStateType, Type currentStateType, PlayerStateTransitionReason reason)
    {
        PreviousStateType = previousStateType;
        CurrentStateType = currentStateType;
        Reason = reason;
    }

    public Type PreviousStateType { get; }
    public Type CurrentStateType { get; }
    public PlayerStateTransitionReason Reason { get; }
}
