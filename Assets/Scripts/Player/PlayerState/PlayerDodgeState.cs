using UnityEngine;

/// <summary>
/// 闪避 Gameplay 状态，拥有闪避能力的开始、逐帧执行和结束转换。
/// </summary>
public sealed class PlayerDodgeState : PlayerStateBase
{
    private bool completed;

    public PlayerDodgeState(PlayerContext context) : base(context) { }

    public override PlayerStateTransitionRequest EvaluateInputTransition()
    {
        if (!Context.Motor.IsGrounded)
        {
            return null;
        }
        return Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Jump) && Context.Jump.CanJump ? new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Jumped) : null;
    }

    public override void Enter(PlayerStateTransition transition)
    {
        completed = false;
        Context.ActionBuffer.Consume(PlayerBufferedAction.Dodge);
        Vector3 initialDirection = Context.Motor.GetWorldMoveDirection(Context.InputSource.MoveInput);
        if (initialDirection.sqrMagnitude < 0.0001f)
        {
            initialDirection = Context.Motor.transform.forward;
        }
        Context.Dodge.Begin(initialDirection);
    }

    public override void Tick()
    {
        DodgeTickResult result = Context.Dodge.Tick(Time.deltaTime, Context.Motor.GetWorldMoveDirection(Context.InputSource.MoveInput));
        Context.Motor.DodgeMove(result.Direction, result.HorizontalDistance);
        completed = result.JustCompleted;
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        if (!Context.Motor.IsGrounded)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
        }
        return completed ? new PlayerStateTransitionRequest(Context.InputSource.MoveInput == Vector2.zero ? typeof(PlayerIdleState) : typeof(PlayerFastRunState), PlayerStateTransitionReason.DodgeCompleted) : null;
    }

    public override void Exit(PlayerStateTransition transition)
    {
        if (!completed)
        {
            Context.Dodge.Cancel();
        }
    }
}
