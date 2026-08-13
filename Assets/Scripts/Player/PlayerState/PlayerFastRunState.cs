using UnityEngine;

/// <summary>
/// Dodge 完成后保持移动输入时进入的疾跑状态。
/// </summary>
public sealed class PlayerFastRunState : PlayerStateBase
{
    public PlayerFastRunState(PlayerContext context) : base(context) { }

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
        return Context.InputSource.MoveInput == Vector2.zero ? new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.StoppedMoving) : null;
    }

    public override void Tick()
    {
        Context.Motor.FastRunMove(Context.Motor.DesiredMoveDirection);
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        return Context.Motor.IsGrounded ? null : new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
    }
}
