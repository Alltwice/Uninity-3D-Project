using UnityEngine;

/// <summary>
/// 接地常规跑动状态。
/// </summary>
public sealed class PlayerRunState : PlayerStateBase
{
    public PlayerRunState(PlayerContext context) : base(context) { }

    public override void Enter(PlayerStateTransition transition)
    {
        Context.Motor.SetDesiredMoveDirection(Context.Motor.GetWorldMoveDirection(Context.InputSource.MoveInput));
    }

    public override PlayerStateTransitionRequest EvaluateInputTransition()
    {
        if (!Context.Motor.IsGrounded)
        {
            return null;
        }
        if (Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Jump) && Context.Jump.CanJump)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Jumped);
        }
        if (Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Dodge) && Context.Dodge.CanDodge)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerDodgeState), PlayerStateTransitionReason.DodgeStarted);
        }
        if (Context.InputSource.MoveInput == Vector2.zero)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.StoppedMoving);
        }
        return Context.InputSource.IsWalkMode ? new PlayerStateTransitionRequest(typeof(PlayerWalkState), PlayerStateTransitionReason.Decelerated) : null;
    }

    public override void Tick()
    {
        Context.Motor.RunMove(Context.Motor.GetWorldMoveDirection(Context.InputSource.MoveInput));
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        return Context.Motor.IsGrounded ? null : new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
    }
}
