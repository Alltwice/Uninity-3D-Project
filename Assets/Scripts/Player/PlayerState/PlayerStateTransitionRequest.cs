using System;

/// <summary>
/// 状态提出的候选转换，只描述目标和原因，不执行任何副作用。
/// </summary>
public sealed class PlayerStateTransitionRequest
{
    public PlayerStateTransitionRequest(
        Type targetStateType,
        PlayerStateTransitionReason reason,
        bool allowReentry = false)
    {
        TargetStateType = targetStateType;
        Reason = reason;
        AllowReentry = allowReentry;
    }

    public Type TargetStateType { get; }
    public PlayerStateTransitionReason Reason { get; }
    public bool AllowReentry { get; }
}
