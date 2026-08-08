using UnityEngine;

/// <summary>
/// 玩家地面移动状态。
/// </summary>
public class PlayerGroundMoveState : PlayerStateBase
{
    public PlayerGroundMoveState(PlayerContext context) : base(context) { }

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
        if (Context.InputSource.MoveInput == Vector2.zero)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.StoppedMoving);
        }
        return null;
    }

    public override void Tick()
    {
        Context.Motor.Move();
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
