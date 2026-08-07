using System;
using UnityEngine;

/// <summary>
/// 玩家空中状态。
/// </summary>
public class PlayerAirState : PlayerStateBase
{
    public PlayerAirState(PlayerContext context) : base(context)
    {
    }

    public override void Enter(PlayerStateTransition transition)
    {
        if (transition.Reason != PlayerStateTransitionReason.Jumped)
        {
            return;
        }

        Context.ActionBuffer.Consume(PlayerBufferedAction.Jump);
        Context.Jump.ExecuteJump();
        Context.AnimationController.RequestJumpUp();
    }

    public override void Tick()
    {
        Context.Motor.AirMove();
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        if (!Context.Motor.IsGrounded)
        {
            return null;
        }

        if (Context.Motor.IsHardLandingImpact)
        {
            return new PlayerStateTransitionRequest(
                typeof(PlayerHardLandingState),
                PlayerStateTransitionReason.HardLanded);
        }

        if (Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Jump) && Context.Jump.CanJump)
        {
            return new PlayerStateTransitionRequest(
                typeof(PlayerAirState),
                PlayerStateTransitionReason.Jumped,
                true);
        }

        Type targetStateType = Context.InputSource.MoveInput != Vector2.zero
            ? typeof(PlayerGroundMoveState)
            : typeof(PlayerIdleState);

        return new PlayerStateTransitionRequest(
            targetStateType,
            PlayerStateTransitionReason.Landed);
    }
}
