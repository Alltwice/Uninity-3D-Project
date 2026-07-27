using System;
using UnityEngine;
/// <summary>
/// 玩家待机状态
/// </summary>
public class PlayerIdleState : PlayerStateBase
{
    //base，默认调用父类构造函数
    public PlayerIdleState(PlayerContext context) : base(context){}

    public override void Enter()
    {
    }
    public override void Exit()
    {
    }
    public override void Tick()
    {
        Context.Motor.IdleMove(); 
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
            Context.AnimationDriver.PlayJumpAnimation();
            return typeof(PlayerAirState);
        }

        if (Context.InputSource.MoveInput != Vector2.zero)
        {
            return typeof(PlayerGroundMoveState);
        }

        return null;
    }
    public override bool CanExit()
    {
        return true;
    }
}
