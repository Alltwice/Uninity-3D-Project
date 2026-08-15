using UnityEngine;

/// <summary>
/// 重落地期间锁定地面移动，恢复条件由状态自身的 Gameplay Progress 决定。
/// </summary>
public sealed class PlayerHardLandingState : PlayerStateBase
{
    private float elapsedTime;

    public PlayerHardLandingState(PlayerContext context) : base(context) { }
    public override PlayerLocomotionMode LocomotionMode => PlayerLocomotionMode.Idle;
    public override float PresentationProgress => Mathf.Clamp01(elapsedTime / Context.MovementConfig.Landing.HardLandingDuration);

    public override void Enter(PlayerStateTransition transition)
    {
        elapsedTime = 0f;
    }

    public override void Tick(float deltaTime, ref PlayerGameplayIntent intent)
    {
        intent.LocomotionMode = PlayerLocomotionMode.Idle;
        elapsedTime = Mathf.Min(Context.MovementConfig.Landing.HardLandingDuration, elapsedTime + deltaTime);
    }

    public override PlayerStateTransitionRequest EvaluateResultTransition()
    {
        if (!Context.IsGrounded)
        {
            return new PlayerStateTransitionRequest(typeof(PlayerAirState), PlayerStateTransitionReason.Fell);
        }
        if (Context.InputSource.MoveInput != Vector2.zero)
        {
            return PresentationProgress >= Context.MovementConfig.Landing.HardLandingInterruptProgress ? new PlayerStateTransitionRequest(ResolveGroundStateType(), PlayerStateTransitionReason.HardLandingRecovered) : null;
        }
        return PresentationProgress >= 1f ? new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.HardLandingRecovered) : null;
    }
}
