using System;

public enum PlayerStateTransitionReason
{
    Initialized,
    StartedMoving,
    StoppedMoving,
    DodgeStarted,
    DodgeCompleted,
    Jumped,
    Fell,
    Landed,
    HardLanded,
    HardLandingRecovered
}

/// <summary>
/// 状态控制器已经接受并执行的转换结果
/// </summary>
public class PlayerStateTransition
{
    public PlayerStateTransition(Type previousStateType, Type currentStateType, PlayerStateTransitionReason reason, PlayerLocomotionMode previousLocomotionMode)
    {
        PreviousStateType = previousStateType;
        CurrentStateType = currentStateType;
        Reason = reason;
        PreviousLocomotionMode = previousLocomotionMode;
    }

    public Type PreviousStateType { get; }
    public Type CurrentStateType { get; }
    public PlayerStateTransitionReason Reason { get; }
    public PlayerLocomotionMode PreviousLocomotionMode { get; }
}
