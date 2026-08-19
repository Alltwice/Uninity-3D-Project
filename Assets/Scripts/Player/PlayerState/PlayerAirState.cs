/// <summary>
/// 玩家空中状态
/// </summary>
public sealed class PlayerAirState : PlayerStateBase
{
    public PlayerAirState(PlayerContext context) : base(context) { }
    public override PlayerLocomotionMode LocomotionMode => PlayerLocomotionMode.Air;

    public override void Enter(PlayerStateTransition transition)
    {
        if (transition.Reason == PlayerStateTransitionReason.Jumped)
        {
            Context.ActionBuffer.Consume(PlayerBufferedAction.Jump);
            Context.RequestJumpImpulse();
        }
    }

    public override void Tick(float deltaTime, ref PlayerGameplayIntent intent)
    {
        intent.LocomotionMode = PlayerLocomotionMode.Air;
        Context.ApplyPendingVerticalImpulse(ref intent);
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        if (!Context.IsGrounded)
        {
            return null;
        }
        if (Context.IsHardLandingImpact)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerHardLandingState), PlayerStateTransitionReason.HardLanded);
        }
        if (Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Jump) && Context.Jump.CanJump(Context.IsGrounded))
        {
            return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Jumped, true);
        }
        return new PlayerStateTransitionRequest(ResolveGroundStateType(), PlayerStateTransitionReason.Landed);
    }
}
