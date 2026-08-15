using UnityEngine;

/// <summary>
/// Dodge 完成后保持移动输入时进入的疾跑状态。
/// </summary>
public sealed class PlayerFastRunState : PlayerStateBase
{
    public PlayerFastRunState(PlayerContext context) : base(context) { }
    public override PlayerLocomotionMode LocomotionMode => PlayerLocomotionMode.FastRun;

    public override PlayerStateTransitionRequest EvaluateInputTransition()
    {
        if (!Context.IsGrounded)
        {
            return null;
        }
        if (Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Jump) && Context.Jump.CanJump(Context.IsGrounded))
        {
            return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Jumped);
        }
        if (Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Dodge) && Context.Dodge.CanDodge)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerDodgeState), PlayerStateTransitionReason.DodgeStarted);
        }
        return Context.InputSource.MoveInput == Vector2.zero ? new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.StoppedMoving) : null;
    }

    public override void Tick(float deltaTime, ref PlayerGameplayIntent intent)
    {
        intent.LocomotionMode = PlayerLocomotionMode.FastRun;
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        return Context.IsGrounded ? null : new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
    }
}
