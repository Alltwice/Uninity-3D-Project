using UnityEngine;

/// <summary>
/// 接地且没有移动意图的状态。
/// </summary>
public sealed class PlayerIdleState : PlayerStateBase
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
            return new PlayerStateTransitionRequest(typeof(PlayerDodgeState), PlayerStateTransitionReason.DodgeStarted);
        }
        return Context.InputSource.MoveInput == Vector2.zero ? null : new PlayerStateTransitionRequest(ResolveGroundStateType(), PlayerStateTransitionReason.StartedMoving);
    }

    public override void Tick()
    {
        Context.Motor.IdleMove();
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        return Context.Motor.IsGrounded ? null : new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
    }
}
