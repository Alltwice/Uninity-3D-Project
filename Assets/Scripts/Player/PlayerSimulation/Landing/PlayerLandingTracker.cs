using UnityEngine;

/// <summary>
/// 跨帧记录一次空中生命周期，并在落地帧生成一次性落地事实
/// </summary>
public class PlayerLandingTracker
{
    private readonly PlayerMovementConfig.LandingSettings settings;
    //是否开始进行记录
    private bool trackingAir;
    private float peakHeight;
    private PlayerLocomotionMode lastGroundMode = PlayerLocomotionMode.Idle;
    private PlayerLocomotionMode airEntryGroundMode = PlayerLocomotionMode.Idle;
    private bool hasGroundModeSample;
    private ulong sequence;

    public PlayerLandingTracker(PlayerMovementConfig.LandingSettings landingSettings)
    {
        settings = landingSettings;
    }
    /// <summary>
    /// 在空中时进行的的状态演进，最终返回落地状态快照
    /// </summary>
    public PlayerLandingSnapshot Advance(PlayerMotorResult motorResult, float currentHeight, PlayerLocomotionMode currentLocomotionMode, PlayerLocomotionMode targetGroundMode, bool hasMoveIntent)
    {
        //空中持续演进
        if (!motorResult.IsGrounded)
        {
            if (!trackingAir)
            {
                trackingAir = true;
                peakHeight = currentHeight;
                airEntryGroundMode = hasGroundModeSample ? lastGroundMode : targetGroundMode;
            }
            else
            {
                peakHeight = Mathf.Max(peakHeight, currentHeight);
            }
            return default;
        }
        PlayerLandingSnapshot snapshot = default;
        //落地后开始处理最终数据
        if (motorResult.JustLanded)
        {
            float fallDistance = trackingAir ? Mathf.Max(0f, peakHeight - currentHeight) : 0f;
            PlayerLandingSeverity severity = ResolveSeverity(fallDistance, motorResult.LandingImpactSpeed);
            snapshot = new PlayerLandingSnapshot(++sequence, severity, fallDistance, motorResult.LandingImpactSpeed, airEntryGroundMode, hasMoveIntent, targetGroundMode);
        }
        trackingAir = false;
        //下一次演进时的airEntryGroundMode
        lastGroundMode = IsGroundMode(currentLocomotionMode) ? currentLocomotionMode : targetGroundMode;
        //一次有效的空中数据演进完毕
        hasGroundModeSample = true;
        return snapshot;
    }

    public void Reset(PlayerLocomotionMode groundMode = PlayerLocomotionMode.Idle)
    {
        trackingAir = false;
        peakHeight = 0f;
        lastGroundMode = groundMode;
        airEntryGroundMode = groundMode;
        hasGroundModeSample = true;
    }
    /// <summary>
    /// 依据掉落距离或者速度决定落地严重程度
    /// </summary>
    private PlayerLandingSeverity ResolveSeverity(float fallDistance, float impactSpeed)
    {
        if (fallDistance >= settings.Lv4MinFallDistance || impactSpeed >= settings.Lv4MinImpactSpeed) return PlayerLandingSeverity.Lv4;
        if (fallDistance >= settings.Lv3MinFallDistance || impactSpeed >= settings.Lv3MinImpactSpeed) return PlayerLandingSeverity.Lv3;
        if (fallDistance >= settings.Lv2MinFallDistance || impactSpeed >= settings.Lv2MinImpactSpeed) return PlayerLandingSeverity.Lv2;
        return PlayerLandingSeverity.Lv1;
    }

    private static bool IsGroundMode(PlayerLocomotionMode mode)
    {
        return mode == PlayerLocomotionMode.Idle || mode == PlayerLocomotionMode.Walk || mode == PlayerLocomotionMode.Run || mode == PlayerLocomotionMode.FastRun;
    }
}
