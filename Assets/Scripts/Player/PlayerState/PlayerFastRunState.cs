using UnityEngine;

/// <summary>
/// Dodge 完成后保持的接地疾跑状态；跨越空中和重落地时由 Context 保存疾跑标识。
/// </summary>
public class PlayerFastRunState : PlayerStateBase
{
    public PlayerFastRunState(PlayerContext context) : base(context) { }
    public override PlayerLocomotionMode LocomotionMode => PlayerLocomotionMode.FastRun;

    public override void Enter(PlayerStateTransition transition)
    {
        Context.ActivateFastRun();
    }

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
        if (!Context.HasGroundMoveContinuationIntent)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.StoppedMoving);
        }
        if (!Context.IsFastRunLatched)
        {
            return new PlayerStateTransitionRequest(ResolveGroundStateType(), PlayerStateTransitionReason.Decelerated);
        }
        return null;
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
