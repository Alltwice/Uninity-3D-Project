using UnityEngine;

/// <summary>
/// 闪避 Gameplay 状态，拥有闪避能力的开始、逐帧执行和结束转换
/// </summary>
public sealed class PlayerDodgeState : PlayerStateBase
{
    public PlayerDodgeState(PlayerContext context) : base(context) { }
    public override PlayerLocomotionMode LocomotionMode => PlayerLocomotionMode.Dodge;

    public override PlayerStateTransitionRequest EvaluateInputTransition()
    {
        if (!Context.IsGrounded)
        {
            return null;
        }
        return Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Jump) && Context.Jump.CanJump(Context.IsGrounded) ? new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Jumped) : null;
    }

    public override void Enter(PlayerStateTransition transition)
    {
        Context.ActionBuffer.Consume(PlayerBufferedAction.Dodge);
        Context.Dodge.Begin();
    }

    public override void Tick(float deltaTime, ref PlayerGameplayIntent intent)
    {
        intent.LocomotionMode = PlayerLocomotionMode.Dodge;
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        if (!Context.IsGrounded)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
        }
        if (!Context.MotionSnapshot.JustCompleted)
        {
            return null;
        }
        if (Context.InputSource.MoveInput == Vector2.zero)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.DodgeCompleted);
        }
        return new PlayerStateTransitionRequest(typeof(PlayerFastRunState), PlayerStateTransitionReason.DodgeCompleted);
    }

    public override void Exit(PlayerStateTransition transition)
    {
        Context.Dodge.End();
    }
}
