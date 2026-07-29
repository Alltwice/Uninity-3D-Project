using System;
using UnityEngine;
/// <summary>
/// 玩家在地面移动逻辑
/// </summary>
public class PlayerGroundMoveState : PlayerStateBase
{
    public PlayerGroundMoveState(PlayerContext context) : base(context){}
    public override void Enter()
    {
        Context.AnimationController.RequestLocomotion();
    }
    public override void Exit()
    {

    }

    public override void Tick()
    {
        Context.Motor.Move();
    }

    protected override Type EvaluateNextStateType()
    {
        if (Context.InputSource == null)
        {
            return null;
        }

        if (!Context.Motor.IsGrounded)
        {
            return typeof(PlayerAirState);
        }

        if (Context.ActionBuffer != null
            && Context.ActionBuffer.Consume(PlayerBufferedAction.Jump)
            && Context.Jump.TryJump())
        {
            Context.AnimationController.RequestJump();
            return typeof(PlayerAirState);
        }

        if (Context.InputSource.MoveInput == Vector2.zero)
        {
            return typeof(PlayerIdleState);
        }

        return null;
    }

    public override bool CanExit()
    {
        return true;
    }
}
