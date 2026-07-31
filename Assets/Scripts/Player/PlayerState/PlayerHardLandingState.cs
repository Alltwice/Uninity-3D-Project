using System;
using UnityEngine;

public class PlayerHardLandingState : PlayerStateBase
{
    public PlayerHardLandingState(PlayerContext context) : base(context)
    {
    }

    public override void Enter()
    {
        Context.AnimationController.RequestHardLanding();
    }

    public override void Exit()
    {
        Context.AnimationController.ReleaseHardLanding();
    }

    public override void Tick()
    {
        Context.Motor.IdleMove();
    }

    protected override Type EvaluateNextStateType()
    {
        if (!Context.Motor.IsGrounded)
        {
            return typeof(PlayerAirState);
        }

        return Context.InputSource.MoveInput != Vector2.zero ? typeof(PlayerGroundMoveState) : typeof(PlayerIdleState);
    }

    public override bool CanExit()
    {
        return !Context.Motor.IsGrounded || Context.AnimationController.IsHardLandingComplete;
    }
}
