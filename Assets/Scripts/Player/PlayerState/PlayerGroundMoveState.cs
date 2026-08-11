using UnityEngine;

/// <summary>
/// 负责普通地面移动与临时闪避能力的生命周期。
/// </summary>
public class PlayerGroundMoveState : PlayerStateBase
{
    private bool fastRunAfterDodge;
    private bool dodgeCompletedWithoutInput;

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

        if (Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Dodge) && Context.Dodge.CanDodge)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerGroundMoveState), PlayerStateTransitionReason.DodgeStarted, true);
        }

        if (Context.Dodge.IsActive)
        {
            return null;
        }

        if (Context.InputSource.MoveInput == Vector2.zero)
        {
            PlayerStateTransitionReason reason = dodgeCompletedWithoutInput ? PlayerStateTransitionReason.DodgeCompleted : PlayerStateTransitionReason.StoppedMoving;
            return new PlayerStateTransitionRequest(typeof(PlayerIdleState), reason);
        }

        return null;
    }

    public override void Enter(PlayerStateTransition transition)
    {
        dodgeCompletedWithoutInput = false;
        if (transition.Reason != PlayerStateTransitionReason.DodgeStarted)
        {
            return;
        }

        Context.ActionBuffer.Consume(PlayerBufferedAction.Dodge);
        Vector3 initialDirection = Context.Motor.GetWorldInputDirection();
        if (initialDirection.sqrMagnitude < 0.0001f)
        {
            initialDirection = Context.Motor.transform.forward;
        }

        Context.Dodge.StartDodge(initialDirection);
    }

    public override void Tick()
    {
        bool hasMoveInput = Context.InputSource.MoveInput != Vector2.zero;
        if (Context.Dodge.IsActive)
        {
            PlayerDodgeExitMode exitMode = hasMoveInput ? PlayerDodgeExitMode.FastRun : PlayerDodgeExitMode.Idle;
            DodgeTickResult result = Context.Dodge.Tick(Time.deltaTime, Context.Motor.GetWorldInputDirection(), exitMode);
            Context.Motor.DodgeMove(result.Direction, result.HorizontalDistance);
            if (result.JustCompleted)
            {
                fastRunAfterDodge = hasMoveInput;
                dodgeCompletedWithoutInput = !hasMoveInput;
            }

            return;
        }

        if (!hasMoveInput)
        {
            fastRunAfterDodge = false;
        }

        if (fastRunAfterDodge)
        {
            Context.Motor.MoveFastRunAfterDodge();
            return;
        }

        Context.Motor.MoveFromInput();
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        if (!Context.Motor.IsGrounded)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
        }

        if (dodgeCompletedWithoutInput)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.DodgeCompleted);
        }

        return null;
    }

    public override void Exit(PlayerStateTransition transition)
    {
        if (Context.Dodge.IsActive)
        {
            Context.Dodge.Cancel(ResolveCancelReason(transition));
        }

        fastRunAfterDodge = false;
        dodgeCompletedWithoutInput = false;
    }

    private DodgeCancelReason ResolveCancelReason(PlayerStateTransition transition)
    {
        if (transition.Reason == PlayerStateTransitionReason.Jumped)
        {
            return DodgeCancelReason.Jumped;
        }

        if (transition.Reason == PlayerStateTransitionReason.Fell)
        {
            return DodgeCancelReason.BecameAirborne;
        }

        return DodgeCancelReason.OtherAction;
    }
}
