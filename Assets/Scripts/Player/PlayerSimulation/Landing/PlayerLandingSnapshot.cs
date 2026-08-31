/// <summary>
/// 一次落地事件的严重度
/// </summary>
public enum PlayerLandingSeverity
{
    Lv1 = 1,
    Lv2 = 2,
    Lv3 = 3,
    Lv4 = 4
}

/// <summary>
/// 一次完整空中生命周期结束时生成的落地事实
/// </summary>
public struct PlayerLandingSnapshot
{
    public PlayerLandingSnapshot(ulong sequence, PlayerLandingSeverity severity, float fallDistance, float impactSpeed, PlayerLocomotionMode airEntryGroundMode, bool hasMoveIntentAtImpact, PlayerLocomotionMode targetGroundMode)
    {
        Sequence = sequence;
        Severity = severity;
        //坠落高度
        FallDistance = fallDistance;
        //坠落速度
        ImpactSpeed = impactSpeed;
        //移动前地面状态
        AirEntryGroundMode = airEntryGroundMode;
        //有无移动
        HasMoveIntentAtImpact = hasMoveIntentAtImpact;
        TargetGroundMode = targetGroundMode;
    }

    public ulong Sequence { get; }
    public bool IsLandingEvent => Sequence != 0;
    public PlayerLandingSeverity Severity { get; }
    public float FallDistance { get; }
    public float ImpactSpeed { get; }
    public PlayerLocomotionMode AirEntryGroundMode { get; }
    public bool HasMoveIntentAtImpact { get; }
    public PlayerLocomotionMode TargetGroundMode { get; }
}
