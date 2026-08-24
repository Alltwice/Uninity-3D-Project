using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 最终应该怎么移动
/// </summary>
public enum PlayerMotionTranslationPolicy
{
    None = 0,
    //速度
    VelocityDriven = 1,
    //使用 Motion Begin 时捕获的方向和动画移动距离
    TravelAlongCapturedDirection = 2,
    //保留运动轨迹
    LocalTrajectory = 3,
    //使用当前 GameplayIntent 方向和动画移动距离
    TravelAlongDesiredDirection = 4
}
/// <summary>
/// 处理旋转方式
/// </summary>
public enum PlayerMotionRotationPolicy
{
    //动画期间不旋转
    KeepFacing = 0,
    //朝输入意图
    FaceDirection = 1,
    //使用动画旋转曲线
    ProfileYaw = 2
}
/// <summary>
/// 动画轨迹移动方向对应位置
/// </summary>
public enum PlayerMotionBasisPolicy
{
    //输入意图前方
    DesiredDirection,
    //输入瞬间朝向位置
    EntryVelocityDirection,
    //进入动画瞬间角色朝向
    EntryFacing
}
/// <summary>
/// Motion 被状态机打断后，决定是否仍解析源状态的退出表现
/// </summary>
public enum PlayerMotionInterruptedExitPolicy
{
    ResolveNormalTransitionMotion = 0,
    DirectToTargetPresentation = 1
}
/// <summary>
/// 动画数据定义
/// </summary>
[CreateAssetMenu(fileName = "PlayerMotionDefinition", menuName = "Player/Motion/Definition")]
public class PlayerMotionDefinition : ScriptableObject
{
    //用哪一份动画数据的轨迹
    [SerializeField] private PlayerMotionProfile profile;
    [SerializeField] private PlayerMotionTranslationPolicy translationPolicy;
    [SerializeField] private PlayerMotionRotationPolicy rotationPolicy;
    [SerializeField] private PlayerMotionBasisPolicy basisPolicy;
    //希望动画完成时间
    [Min(0f)] [SerializeField] private float durationOverride;
    //移动倍率
    [Min(0f)] [SerializeField] private float translationScale = 1f;
    //控制权移交
    [Range(0f, 1f)] [SerializeField] private float handoffStartProgress = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float handoffEndProgress = 1f;
    //状态转换承诺窗口；窗口只约束状态机何时接受普通请求，不拥有转换裁决权
    [Range(0f, 1f)] [SerializeField] private float transitionLockEndProgress;
    [SerializeField] private PlayerMotionInterruptedExitPolicy interruptedExitPolicy;
    [SerializeField] private AnimationCurve translationAuthority = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    //是否需要对应的动画表现
    [SerializeField] private bool requiresPresentation = true;

    public PlayerMotionProfile Profile => profile;
    public PlayerMotionTranslationPolicy TranslationPolicy => translationPolicy;
    public PlayerMotionRotationPolicy RotationPolicy => rotationPolicy;
    public PlayerMotionBasisPolicy BasisPolicy => basisPolicy;
    //可控制动画播放时间，若没设设定使用默认的动画时长
    public float Duration => durationOverride > 0f ? durationOverride : profile == null ? 0f : profile.Duration;
    public float TranslationScale => translationScale;
    public float HandoffStartProgress => handoffStartProgress;
    public float HandoffEndProgress => handoffEndProgress;
    public float TransitionLockEndProgress => transitionLockEndProgress;
    public PlayerMotionInterruptedExitPolicy InterruptedExitPolicy => interruptedExitPolicy;
    public bool RequiresPresentation => requiresPresentation;
    /// <summary>
    /// 将Handoff的过程从0.8-1.0重映射为0-1；
    /// </summary>
    public float CalculateHandoffProgress(float motionProgress)
    {
        if (handoffEndProgress <= handoffStartProgress) return motionProgress >= handoffEndProgress ? 1f : 0f;
        //过程相对于整体0-1映射
        return Mathf.Clamp01((motionProgress - handoffStartProgress) / (handoffEndProgress - handoffStartProgress));
    }

    public float EvaluateTranslationAuthority(float motionProgress) => EvaluateAuthority(translationAuthority, motionProgress);
    /// <summary>
    /// 数据校验
    /// </summary>
    public bool Validate(ICollection<string> errors)
    {
        bool valid = true;
        if (profile == null) { errors?.Add(name + ": 缺少 MotionProfile。"); return false; }
        valid &= profile.Validate(errors);
        if (float.IsNaN(Duration) || float.IsInfinity(Duration) || Duration <= 0f) { errors?.Add(name + ": Runtime Duration 必须是大于 0 的有限值。"); valid = false; }
        if (float.IsNaN(translationScale) || float.IsInfinity(translationScale)) { errors?.Add(name + ": TranslationScale 必须是有限值。"); valid = false; }
        if (handoffEndProgress < handoffStartProgress) { errors?.Add(name + ": HandoffEndProgress 不能早于 Start。"); valid = false; }
        if (float.IsNaN(transitionLockEndProgress) || float.IsInfinity(transitionLockEndProgress) || transitionLockEndProgress < 0f || transitionLockEndProgress > 1f) { errors?.Add(name + ": TransitionLockEndProgress 必须是 0 到 1 的有限值。"); valid = false; }
        if (rotationPolicy == PlayerMotionRotationPolicy.ProfileYaw && !profile.HasYaw) { errors?.Add(name + ": ProfileYaw 需要有效 Yaw channel。"); valid = false; }
        if (translationPolicy == PlayerMotionTranslationPolicy.LocalTrajectory && !profile.HasPlanarPosition) { errors?.Add(name + ": LocalTrajectory 需要有效 XZ channel。"); valid = false; }
        if ((translationPolicy == PlayerMotionTranslationPolicy.TravelAlongCapturedDirection || translationPolicy == PlayerMotionTranslationPolicy.TravelAlongDesiredDirection) && !profile.HasTravelDistance) { errors?.Add(name + ": TravelAlong 需要有效 Travel channel。"); valid = false; }
        return valid;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Unity 编辑器中配置轨迹、状态锁承诺窗口和中断表现策略。
    /// </summary>
    public void Configure(PlayerMotionProfile motionProfile, PlayerMotionTranslationPolicy translation, PlayerMotionRotationPolicy rotation, PlayerMotionBasisPolicy basis, 
        float runtimeDuration, float scale, float handoffStart, float handoffEnd, bool presentation = true, float transitionLockEndProgress = 0f, PlayerMotionInterruptedExitPolicy interruptedExitPolicy = PlayerMotionInterruptedExitPolicy.ResolveNormalTransitionMotion)
    {
        profile = motionProfile;
        translationPolicy = translation;
        rotationPolicy = rotation;
        basisPolicy = basis;
        durationOverride = runtimeDuration;
        translationScale = scale;
        handoffStartProgress = handoffStart;
        handoffEndProgress = handoffEnd;
        this.transitionLockEndProgress = transitionLockEndProgress;
        this.interruptedExitPolicy = interruptedExitPolicy;
        requiresPresentation = presentation;
        translationAuthority = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    }
#endif
    /// <summary>
    /// 依据过渡进程返回动画控制权重
    /// </summary>
    private float EvaluateAuthority(AnimationCurve curve, float motionProgress)
    {
        //零长度移交没有混合区间，由动画位移负责到完成帧结束
        if (handoffEndProgress <= handoffStartProgress) return 1f;
        //未开始时完全动画掌控
        if (motionProgress < handoffStartProgress) return 1f;
        //完全结束后交给程序掌控
        if (motionProgress >= handoffEndProgress) return 0f;
        //拿到0-1映射
        float handoff = CalculateHandoffProgress(motionProgress);
        
        return Mathf.Clamp01(curve == null ? 1f - handoff : curve.Evaluate(handoff));
    }
}
