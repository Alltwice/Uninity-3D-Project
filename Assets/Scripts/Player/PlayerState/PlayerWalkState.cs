using UnityEngine;

/// <summary>
/// 接地慢速移动状态。
/// </summary>
public sealed class PlayerWalkState : PlayerStateBase
{
    public PlayerWalkState(PlayerContext context) : base(context) { }

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
        return Context.InputSource.IsWalkMode ? null : new PlayerStateTransitionRequest(typeof(PlayerRunState), PlayerStateTransitionReason.Accelerated);
    }

    public override void Tick()
    {
        Context.Motor.WalkMove(Context.Motor.DesiredMoveDirection);
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        return Context.Motor.IsGrounded ? null : new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
    }
}
