/// <summary>
/// AnimationController 在完成 Animancer 图评估后提供给 Planner 的循环相位事实
/// </summary>
public struct PlayerLocomotionPhaseSnapshot
{
    public PlayerLocomotionPhaseSnapshot(bool hasLoop, bool hasPhase, PlayerMotionProfile profile, float normalizedTime, float effectiveSpeed, PlayerFoot lastPlantFoot, PlayerFoot nextPlantFoot, float stepProgress, float timeToNextPlant)
    {
        HasLoop = hasLoop;
        HasPhase = hasPhase;
        Profile = profile;
        NormalizedTime = normalizedTime;
        EffectiveSpeed = effectiveSpeed;
        LastPlantFoot = lastPlantFoot;
        NextPlantFoot = nextPlantFoot;
        StepProgress = stepProgress;
        TimeToNextPlant = timeToNextPlant;
    }

    public bool HasLoop { get; }
    public bool HasPhase { get; }
    public PlayerMotionProfile Profile { get; }
    public float NormalizedTime { get; }
    public float EffectiveSpeed { get; }
    public PlayerFoot LastPlantFoot { get; }
    public PlayerFoot NextPlantFoot { get; }
    public float StepProgress { get; }
    public float TimeToNextPlant { get; }
}
