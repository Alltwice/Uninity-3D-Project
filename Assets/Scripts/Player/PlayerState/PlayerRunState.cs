using UnityEngine;

/// <summary>
/// 接地常规跑动状态。
/// </summary>
public sealed class PlayerRunState : PlayerStateBase
{
    public PlayerRunState(PlayerContext context) : base(context) { }
    public override PlayerLocomotionMode LocomotionMode => PlayerLocomotionMode.Run;

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
        if (!Context.IsWalkMode)
        {
            return null;
        }
        return new PlayerStateTransitionRequest(typeof(PlayerWalkState), PlayerStateTransitionReason.Decelerated);
    }

    public override void Tick(float deltaTime, ref PlayerGameplayIntent intent)
    {
        intent.LocomotionMode = PlayerLocomotionMode.Run;
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        return Context.IsGrounded ? null : new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
    }
}
