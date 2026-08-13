/// <summary>
/// 玩家空中状态。
/// </summary>
public sealed class PlayerAirState : PlayerStateBase
{
    public PlayerAirState(PlayerContext context) : base(context) { }

    public override void Enter(PlayerStateTransition transition)
    {
        if (transition.Reason == PlayerStateTransitionReason.Jumped)
        {
            Context.ActionBuffer.Consume(PlayerBufferedAction.Jump);
            Context.Jump.ExecuteJump();
        }
    }

    public override void Tick()
    {
        Context.Motor.AirMove(Context.Motor.DesiredMoveDirection);
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        if (!Context.Motor.IsGrounded)
        {
            return null;
        }
        if (Context.Motor.IsHardLandingImpact)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerHardLandingState), PlayerStateTransitionReason.HardLanded);
        }
        if (Context.ActionBuffer.HasBuffered(PlayerBufferedAction.Jump) && Context.Jump.CanJump)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Jumped, true);
        }
        return new PlayerStateTransitionRequest(ResolveGroundStateType(), PlayerStateTransitionReason.Landed);
    }
}
