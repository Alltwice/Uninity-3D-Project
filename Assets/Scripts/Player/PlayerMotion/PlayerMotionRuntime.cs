using UnityEngine;

/// <summary>
/// 在一帧中特殊动画希望贡献的位移数据
/// </summary>
public struct PlayerMotionFrame
{
    public PlayerMotionFrame(PlayerMotionDefinition definition, Vector3 authoredPlanarDisplacement, float authoredYawDelta, float remainingAuthoredYaw, float previousProgress, float currentProgress, float translationAuthority)
        : this(definition, definition == null ? null : definition.Profile, PlayerFoot.Unknown, authoredPlanarDisplacement, authoredYawDelta, remainingAuthoredYaw, previousProgress, currentProgress, translationAuthority)
    {
    }

    public PlayerMotionFrame(PlayerMotionDefinition definition, PlayerMotionProfile profile, PlayerFoot entryLastPlantFoot, Vector3 authoredPlanarDisplacement, float authoredYawDelta, float remainingAuthoredYaw, float previousProgress, float currentProgress, float translationAuthority)
    {
        //定义由谁产生
        Definition = definition;
        Profile = profile;
        EntryLastPlantFoot = entryLastPlantFoot;
        //这一帧应该产生多少位移
        AuthoredPlanarDisplacement = authoredPlanarDisplacement;
        //一帧产生旋转
        AuthoredYawDelta = authoredYawDelta;
        RemainingAuthoredYaw = remainingAuthoredYaw;
        PreviousProgress = previousProgress;
        CurrentProgress = currentProgress;
        //动画移动轨迹和代码的控制权占比
        TranslationAuthority = translationAuthority;
    }

    public PlayerMotionDefinition Definition { get; }
    public PlayerMotionProfile Profile { get; }
    public PlayerFoot EntryLastPlantFoot { get; }
    public Vector3 AuthoredPlanarDisplacement { get; }
    public float AuthoredYawDelta { get; }
    public float RemainingAuthoredYaw { get; }
    public float PreviousProgress { get; }
    public float CurrentProgress { get; }
    public float TranslationAuthority { get; }
    //查找有无有效输入
    public bool IsValid => Definition != null;
}
/// <summary>
/// 供外部获取的motion状态快照
/// </summary>
public struct PlayerMotionSnapshot
{
    public PlayerMotionSnapshot(PlayerMotionDefinition activeDefinition, ulong instanceId, float progress, float handoffProgress, bool handoffActive, bool isActive, bool justCompleted, bool justCancelled, bool isTransitionLocked = false)
        : this(activeDefinition, activeDefinition == null ? null : activeDefinition.Profile, PlayerFoot.Unknown, instanceId, progress, handoffProgress, handoffActive, isActive, justCompleted, justCancelled, isTransitionLocked)
    {
    }

    public PlayerMotionSnapshot(PlayerMotionDefinition activeDefinition, PlayerMotionProfile activeProfile, PlayerFoot entryLastPlantFoot, ulong instanceId, float progress, float handoffProgress, bool handoffActive, bool isActive, bool justCompleted, bool justCancelled, bool isTransitionLocked = false)
    {
        ActiveDefinition = activeDefinition;
        ActiveProfile = activeProfile;
        EntryLastPlantFoot = entryLastPlantFoot;
        InstanceId = instanceId;
        Progress = progress;
        HandoffProgress = handoffProgress;
        HandoffActive = handoffActive;
        IsActive = isActive;
        JustCompleted = justCompleted;
        JustCancelled = justCancelled;
        IsTransitionLocked = isTransitionLocked;
    }

    public PlayerMotionDefinition ActiveDefinition { get; }
    public PlayerMotionProfile ActiveProfile { get; }
    public PlayerFoot EntryLastPlantFoot { get; }
    public ulong InstanceId { get; }
    public float Progress { get; }
    public float HandoffProgress { get; }
    public bool HandoffActive { get; }
    public bool IsActive { get; }
    public bool JustCompleted { get; }
    public bool JustCancelled { get; }
    public bool IsTransitionLocked { get; }
}
public class PlayerMotionRuntime
{
    private PlayerMotionDefinition definition;
    private PlayerMotionProfile profile;
    private PlayerFoot entryLastPlantFoot;
    //消除角色动画影响转向世界位置
    private Quaternion basis = Quaternion.identity;
    //玩家移动数据
    private Vector3 travelDirection;
    private ulong sequence;
    private ulong instanceId;
    private float elapsedTime;
    private float duration;
    private float previousProgress;
    private float currentProgress;
    private bool isActive;
    private bool justCompleted;
    private bool justCancelled;

    public PlayerMotionSnapshot Snapshot => BuildSnapshot();
    /// <summary>
    /// 处理单帧事件例如跳跃开始结束等
    /// </summary>
    public void BeginFrame()
    {
        if (!isActive && (justCompleted || justCancelled))
        {
            definition = null;
            profile = null;
            entryLastPlantFoot = PlayerFoot.Unknown;
            duration = 0f;
        }
        justCompleted = false;
        justCancelled = false;
    }
    /// <summary>
    /// 动画启动时的基础设定
    /// </summary>
    public ulong Begin(PlayerMotionDefinition nextDefinition, Vector3 basisDirection, Vector3 initialTravelDirection, float startProgress = 0f)
    {
        return Begin(nextDefinition, nextDefinition == null ? null : nextDefinition.Profile, PlayerFoot.Unknown, basisDirection, initialTravelDirection, startProgress);
    }

    public ulong Begin(PlayerMotionDefinition nextDefinition, PlayerMotionProfile selectedProfile, PlayerFoot selectedEntryLastPlantFoot, Vector3 basisDirection, Vector3 initialTravelDirection, float startProgress = 0f)
    {
        bool replaced = isActive;
        //切换动画数据
        definition = nextDefinition;
        profile = selectedProfile ?? (definition == null ? null : definition.ResolveProfile(selectedEntryLastPlantFoot));
        entryLastPlantFoot = selectedEntryLastPlantFoot;
        duration = definition == null ? 0f : definition.GetDuration(profile);
        instanceId = ++sequence;
        //当前开始动画执行时间
        elapsedTime = Mathf.Clamp01(startProgress) * duration;
        previousProgress = Mathf.Clamp01(startProgress);
        currentProgress = previousProgress;
        //记录前方
        basisDirection = NormalizePlanar(basisDirection, Vector3.forward);
        //实际运动方向
        travelDirection = NormalizePlanar(initialTravelDirection, basisDirection);
        //创建面向玩家前方的旋转
        basis = Quaternion.LookRotation(basisDirection, Vector3.up);
        justCompleted = false;
        justCancelled = replaced;
        isActive = definition != null && profile != null && duration > 0f;
        //返回这一次的动画处理ID
        return instanceId;
    }

    public void Cancel()
    {
        if (!isActive) return;
        isActive = false;
        justCompleted = false;
        justCancelled = true;
    }
    /// <summary>
    /// 按固定间隔时间推进动画演进
    /// </summary>
    public PlayerMotionFrame Advance(float deltaTime, PlayerGameplayIntent intent)
    {
        if (!isActive || definition == null) return default;
        if (definition.TranslationPolicy == PlayerMotionTranslationPolicy.TravelAlongDesiredDirection && intent.DesiredMoveDirection.sqrMagnitude > 0.0001f) travelDirection = NormalizePlanar(intent.DesiredMoveDirection, travelDirection);
        previousProgress = currentProgress;
        //推进deltatime的时间
        elapsedTime = Mathf.Min(duration, elapsedTime + Mathf.Max(0f, deltaTime));
        //计算进程
        currentProgress = duration > 0f ? Mathf.Clamp01(elapsedTime / duration) : 1f;
        PlayerMotionProfile activeProfile = profile;
        //拿到需要烘焙移动的位移数据
        Vector3 authoredTranslation = EvaluateTranslation(activeProfile, definition, previousProgress, currentProgress);
        //一帧要转多少度
        float authoredYaw = definition.RotationPolicy == PlayerMotionRotationPolicy.ProfileYaw ? activeProfile.EvaluateYaw(currentProgress) - activeProfile.EvaluateYaw(previousProgress) : 0f;
        //检查从当前开始距离旋转结束还差多少度
        float remainingAuthoredYaw = definition.RotationPolicy == PlayerMotionRotationPolicy.ProfileYaw ? activeProfile.EvaluateYaw(1f) - activeProfile.EvaluateYaw(currentProgress) : 0f;
        //拿到动画控制权重
        float translationWeight = definition.EvaluateTranslationAuthority(currentProgress);
        //产生这一帧等待消费的移动数据
        PlayerMotionFrame frame = new PlayerMotionFrame(definition, activeProfile, entryLastPlantFoot, authoredTranslation, authoredYaw, remainingAuthoredYaw, previousProgress, currentProgress, translationWeight);
        if (currentProgress >= 1f)
        {
            isActive = false;
            justCompleted = true;
        }
        return frame;
    }
    /// <summary>
    /// 利用烘焙动画数据文件执行移动
    /// </summary>
    private Vector3 EvaluateTranslation(PlayerMotionProfile profile, PlayerMotionDefinition motionDefinition, float fromProgress, float toProgress)
    {
        switch (motionDefinition.TranslationPolicy)
        {
            case PlayerMotionTranslationPolicy.TravelAlongCapturedDirection:
            case PlayerMotionTranslationPolicy.TravelAlongDesiredDirection:
                //方向*（移动过程比例*整体缩放）可理解为速度
                return travelDirection * ((profile.EvaluateTravelDistance(toProgress) - profile.EvaluateTravelDistance(fromProgress)) * motionDefinition.TranslationScale);
            case PlayerMotionTranslationPolicy.LocalTrajectory:
                return basis * ((profile.EvaluatePlanarPosition(toProgress) - profile.EvaluatePlanarPosition(fromProgress)) * motionDefinition.TranslationScale);
            default:
                return Vector3.zero;
        }
    }
    /// <summary>
    /// 建立快照
    /// </summary>
    /// <returns></returns>
    private PlayerMotionSnapshot BuildSnapshot()
    {
        float handoff = definition == null ? 0f : definition.CalculateHandoffProgress(currentProgress);
        bool handoffActive = definition != null && currentProgress >= definition.HandoffStartProgress;
        //这里处理动画锁
        bool isTransitionLocked = definition != null && isActive && currentProgress < definition.TransitionLockEndProgress;
        return new PlayerMotionSnapshot(definition, profile, entryLastPlantFoot, instanceId, currentProgress, handoff, handoffActive, isActive, justCompleted, justCancelled, isTransitionLocked);
    }
    /// <summary>
    /// 去除y分量并将其向量化
    /// </summary>
    private static Vector3 NormalizePlanar(Vector3 value, Vector3 fallback)
    {
        value.y = 0f;
        if (value.sqrMagnitude > 0.0001f) return value.normalized;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.zero;
    }
}
