using UnityEngine;

/// <summary>
/// 接地慢速移动状态。
/// </summary>
public sealed class PlayerWalkState : PlayerStateBase
{
    public PlayerWalkState(PlayerContext context) : base(context) { }
    public override PlayerLocomotionMode LocomotionMode => PlayerLocomotionMode.Walk;

    public override PlayerStateTransitionRequest EvaluateInputTransition()
    {
        if (!Context.IsGrounded)
        {
            return null;
        }
        if (Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Jump) && Context.Jump.CanJump(Context.IsGrounded))
        {
            return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Jumped);
        }
        if (Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Dodge) && Context.Dodge.CanDodge)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerDodgeState), PlayerStateTransitionReason.DodgeStarted);
        }
        if (!Context.HasGroundMoveContinuationIntent)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.StoppedMoving);
        }
        return Context.IsWalkMode ? null : new PlayerStateTransitionRequest(typeof(PlayerRunState), PlayerStateTransitionReason.Accelerated);
    }

    public override void Tick(float deltaTime, ref PlayerGameplayIntent intent)
    {
        intent.LocomotionMode = PlayerLocomotionMode.Walk;
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        return Context.IsGrounded ? null : new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
    }
}
