using UnityEngine;

/// <summary>
/// 接地且没有已接受移动意图时的状态。
/// </summary>
public class PlayerIdleState : PlayerStateBase
{
    public PlayerIdleState(PlayerContext context) : base(context) { }

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
            return new PlayerStateTransitionRequest(typeof(PlayerGroundMoveState), PlayerStateTransitionReason.DodgeStarted);
        }

        if (Context.InputSource.MoveInput != Vector2.zero)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerGroundMoveState), PlayerStateTransitionReason.StartedMoving);
        }

        return null;
    }

    public override void Tick()
    {
        Context.Motor.IdleMove();
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        if (Context.Motor.IsGrounded)
        {
            return null;
        }

        return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
    }
}
