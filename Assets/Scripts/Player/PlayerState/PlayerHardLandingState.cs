using System;
using UnityEngine;

/// <summary>
/// 玩家硬着陆锁定状态。
/// </summary>
public class PlayerHardLandingState : PlayerStateBase
{
    public PlayerHardLandingState(PlayerContext context) : base(context) { }

    public override void Enter(PlayerStateTransition transition)
    {
        Context.AnimationController.RequestHardLanding();
    }

    public override void Tick()
    {
        Context.Motor.IdleMove();
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        if (!Context.Motor.IsGrounded)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
        }
        if (Context.InputSource.MoveInput != Vector2.zero)
        {
            if (!Context.AnimationController.CanInterruptHardLanding)
            {
                return null;
            }

            return new PlayerStateTransitionRequest(typeof(PlayerGroundMoveState), PlayerStateTransitionReason.HardLandingRecovered);
        }
        if (!Context.AnimationController.IsHardLandingComplete)
        {
            return null;
        }
        return new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.HardLandingRecovered);
    }

    public override void Exit(PlayerStateTransition transition)
    {
        Context.AnimationController.ReleaseHardLanding();
    }
}
