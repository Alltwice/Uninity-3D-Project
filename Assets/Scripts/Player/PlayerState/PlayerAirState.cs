using System;
using UnityEngine;
/// <summary>
/// 玩家空中状态
/// </summary>
public class PlayerAirState : PlayerStateBase
{
    public PlayerAirState(PlayerContext context) : base(context){}
    public override void Enter()
    {

    }

    public override void Exit()
    {
    }

    public override void Tick()
    {
        Context.Motor.AirMove();
    }

    protected override Type EvaluateNextStateType()
    {
        if (!Context.Motor.IsGrounded)
        {
            return null;
        }

        if (Context.Motor.IsHardLandingImpact)
        {
            return typeof(PlayerHardLandingState);
        }

        if (Context.ActionBuffer != null
            && Context.ActionBuffer.Consume(PlayerBufferedAction.Jump)
            && Context.Jump.TryJump())
        {
            Context.AnimationController.RequestJumpUp();
            return typeof(PlayerAirState);
        }

        if (Context.InputSource.MoveInput != Vector2.zero)
        {
            return typeof(PlayerGroundMoveState);
        }

        return typeof(PlayerIdleState);
    }

    public override bool CanExit()
    {
        return true;
    }
}
