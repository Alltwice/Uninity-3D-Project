using UnityEngine;

/// <summary>
/// 重落地期间锁定地面移动，恢复条件由专用动画播放进度决定。
/// </summary>
public sealed class PlayerHardLandingState : PlayerStateBase
{
    public PlayerHardLandingState(PlayerContext context) : base(context) { }

    public override void Tick()
    {
        Context.Motor.IdleMove();
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        if (!Context.Motor.IsGrounded)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
        }
        if (Context.InputSource.MoveInput != Vector2.zero)
        {
            return Context.AnimationController.CanInterruptHardLanding ? new PlayerStateTransitionRequest(ResolveGroundStateType(), PlayerStateTransitionReason.HardLandingRecovered) : null;
        }
        return Context.AnimationController.IsHardLandingComplete ? new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.HardLandingRecovered) : null;
    }
}
