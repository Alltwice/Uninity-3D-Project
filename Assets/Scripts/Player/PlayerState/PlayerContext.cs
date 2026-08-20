using UnityEngine;

/// <summary>
/// 玩家状态共享的稳定依赖。
/// </summary>
public sealed class PlayerContext
{
    public PlayerJump Jump { get; }
    public PlayerDodge Dodge { get; }
    public IPlayerInputSource InputSource { get; }
    public IPlayerActionBuffer ActionBuffer { get; }
    public PlayerMovementConfig MovementConfig { get; }
    public PlayerMotorResult MotorResult { get; private set; }
    public PlayerMotionSnapshot MotionSnapshot { get; private set; }
    public bool IsGrounded => MotorResult.IsGrounded;
    public bool IsHardLandingImpact => MotorResult.JustLanded && MotorResult.LandingImpactSpeed >= MovementConfig.Landing.HardLandingMinImpactSpeed;

    private float pendingVerticalImpulse;
    private bool hasPendingVerticalImpulse;

    public PlayerContext(PlayerJump jump, PlayerDodge dodge, IPlayerInputSource inputSource, IPlayerActionBuffer actionBuffer, PlayerMovementConfig movementConfig)
    {
        Jump = jump;
        Dodge = dodge;
        InputSource = inputSource;
        ActionBuffer = actionBuffer;
        MovementConfig = movementConfig;
    }

    public void SetSimulationFacts(PlayerMotorResult motorResult, PlayerMotionSnapshot motionSnapshot)
    {
        MotorResult = motorResult;
        MotionSnapshot = motionSnapshot;
    }

    public void RequestJumpImpulse()
    {
        pendingVerticalImpulse = Jump.CalculateImpulse();
        hasPendingVerticalImpulse = true;
    }

    public void ApplyPendingVerticalImpulse(ref PlayerGameplayIntent intent)
    {
        if (!hasPendingVerticalImpulse) return;
        intent.RequestVerticalImpulse(pendingVerticalImpulse);
        hasPendingVerticalImpulse = false;
    }
}
