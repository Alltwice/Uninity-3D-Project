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
public struct PlayerStateTransition
{
    public PlayerStateTransition(Type previousStateType, Type currentStateType, PlayerStateTransitionReason reason, PlayerLocomotionMode previousLocomotionMode, PlayerLocomotionMode currentLocomotionMode)
    {
        PreviousStateType = previousStateType;
        CurrentStateType = currentStateType;
        Reason = reason;
        PreviousLocomotionMode = previousLocomotionMode;
        CurrentLocomotionMode = currentLocomotionMode;
    }

    public Type PreviousStateType { get; }
    public Type CurrentStateType { get; }
    public PlayerStateTransitionReason Reason { get; }
    public PlayerLocomotionMode PreviousLocomotionMode { get; }
    public PlayerLocomotionMode CurrentLocomotionMode { get; }
}
