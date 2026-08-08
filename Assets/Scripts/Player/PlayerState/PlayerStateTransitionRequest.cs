using System;

/// <summary>
/// 状态提出的候选转换，只描述目标和原因，不执行任何副作用
/// </summary>
public class PlayerStateTransitionRequest
{
    public PlayerStateTransitionRequest(Type targetStateType, PlayerStateTransitionReason reason, bool allowReentry = false)
    {
        TargetStateType = targetStateType;
        Reason = reason;
        AllowReentry = allowReentry;
    }
    public Type TargetStateType { get; }
    public PlayerStateTransitionReason Reason { get; }
    //是否允许重新进入同一状态，处理类似与落地后直接起跳的场景（air->air）
    public bool AllowReentry { get; }
}
