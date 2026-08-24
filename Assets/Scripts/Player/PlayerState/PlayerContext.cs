using UnityEngine;

/// <summary>
/// 玩家状态共享的稳定依赖
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
    public bool IsWalkMode => isWalkMode;
    public bool HasGroundMoveContinuationIntent => hasGroundMoveContinuationIntent;
    public bool IsFastRunLatched => isFastRunLatched;

    private float pendingVerticalImpulse;
    private bool hasPendingVerticalImpulse;
    private float groundMoveInputReleaseElapsed;
    private bool hasGroundMoveContinuationIntent;
    private bool isFastRunLatched;
    //原始输入信号，用于记录是否按下切换键
    private bool walkToggleSignal;
    private bool isWalkMode;

    public PlayerContext(PlayerJump jump, PlayerDodge dodge, IPlayerInputSource inputSource, IPlayerActionBuffer actionBuffer, PlayerMovementConfig movementConfig)
    {
        Jump = jump;
        Dodge = dodge;
        InputSource = inputSource;
        ActionBuffer = actionBuffer;
        MovementConfig = movementConfig;
        walkToggleSignal = inputSource.IsWalkMode;
    }

    public void SetSimulationFacts(PlayerMotorResult motorResult, PlayerMotionSnapshot motionSnapshot)
    {
        MotorResult = motorResult;
        MotionSnapshot = motionSnapshot;
    }

    /// <summary>
    /// 根据本帧原始移动输入更新地面移动意图，并消费 WalkToggle 信号；零输入只在宽限期结束后确认停止
    /// </summary>
    public void UpdateLocomotionIntent(float deltaTime)
    {
        bool currentWalkToggleSignal = InputSource.IsWalkMode;
        if (currentWalkToggleSignal != walkToggleSignal)
        {
            walkToggleSignal = currentWalkToggleSignal;
            //疾跑解锁后强制切换掉walk状态
            if (!isFastRunLatched) isWalkMode = !isWalkMode;
        }
        if (InputSource.MoveInput != Vector2.zero)
        {
            groundMoveInputReleaseElapsed = 0f;
            hasGroundMoveContinuationIntent = true;
            return;
        }
        if (!hasGroundMoveContinuationIntent)
        {
            return;
        }
        groundMoveInputReleaseElapsed += deltaTime;
        if (groundMoveInputReleaseElapsed < MovementConfig.Locomotion.GroundMoveInputReleaseGraceTime)
        {
            return;
        }
        //以上条件均不满足时可以清楚输入指令
        hasGroundMoveContinuationIntent = false;
        if (isFastRunLatched) ClearFastRun();
    }

    public void ActivateFastRun()
    {
        isFastRunLatched = true;
        isWalkMode = false;
    }

    public void ClearFastRun()
    {
        isFastRunLatched = false;
        isWalkMode = false;
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
