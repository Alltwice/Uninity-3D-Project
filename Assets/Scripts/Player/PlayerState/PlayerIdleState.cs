using UnityEngine;

/// <summary>
/// 玩家待机状态。
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
            return new PlayerStateTransitionRequest(typeof(PlayerAirState),PlayerStateTransitionReason.Jumped);
        }
        if (Context.InputSource.MoveInput != Vector2.zero)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerGroundMoveState), PlayerStateTransitionReason.StartedMoving);
        }
        return null;
    }

    public override void Enter(PlayerStateTransition transition)
    {
        if (transition.Reason == PlayerStateTransitionReason.StoppedMoving && transition.PreviousLocomotionMode == PlayerLocomotionMode.FastRun)
        {
            Context.AnimationController.RequestFastRunStop();
        }
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
