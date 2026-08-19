using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 最终应该怎么移动
/// </summary>
public enum PlayerMotionTranslationPolicy
{
    None,
    //速度
    VelocityDriven,
    //去除X/Z轨迹，只保留动画移动距离
    TravelAlongDirection,
    //保留运动轨迹
    LocalTrajectory
}
/// <summary>
/// 处理旋转方式
/// </summary>
public enum PlayerMotionRotationPolicy
{
    None,
    //朝输入意图
    FaceDirection,
    //使用动画旋转曲线
    ProfileYaw,
    //动画期间不旋转
    KeepFacing
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
/// 特殊动作处理
/// </summary>
public enum PlayerMotionControlPolicy
{
    None,
    Turn180,
    Dodge
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
    [SerializeField] private PlayerMotionControlPolicy controlPolicy;
    //希望动画完成时间
    [Min(0f)] [SerializeField] private float durationOverride;
    //移动倍率
    [Min(0f)] [SerializeField] private float translationScale = 1f;
    //控制权移交
    [Range(0f, 1f)] [SerializeField] private float handoffStartProgress = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float handoffEndProgress = 1f;
    [SerializeField] private AnimationCurve translationAuthority = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve rotationAuthority = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    //是否需要对应的动画表现
    [SerializeField] private bool requiresPresentation = true;

    public PlayerMotionProfile Profile => profile;
    public PlayerMotionTranslationPolicy TranslationPolicy => translationPolicy;
    public PlayerMotionRotationPolicy RotationPolicy => rotationPolicy;
    public PlayerMotionBasisPolicy BasisPolicy => basisPolicy;
    public PlayerMotionControlPolicy ControlPolicy => controlPolicy;
    //可控制动画播放时间，若没设设定使用默认的动画时长
    public float Duration => durationOverride > 0f ? durationOverride : profile == null ? 0f : profile.Duration;
    public float TranslationScale => translationScale;
    public float HandoffStartProgress => handoffStartProgress;
    public float HandoffEndProgress => handoffEndProgress;
    public bool RequiresPresentation => requiresPresentation;
    /// <summary>
    /// 将Handoff的过程从0，8-1.0重映射为0-1；
    /// </summary>
    public float CalculateHandoffProgress(float motionProgress)
    {
        if (handoffEndProgress <= handoffStartProgress) return motionProgress >= handoffEndProgress ? 1f : 0f;
        //过程相对于整体0-1映射
        return Mathf.Clamp01((motionProgress - handoffStartProgress) / (handoffEndProgress - handoffStartProgress));
    }

    public float EvaluateTranslationAuthority(float motionProgress) => EvaluateAuthority(translationAuthority, motionProgress);
    public float EvaluateRotationAuthority(float motionProgress) => EvaluateAuthority(rotationAuthority, motionProgress);
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
        if (rotationPolicy == PlayerMotionRotationPolicy.ProfileYaw && !profile.HasYaw) { errors?.Add(name + ": ProfileYaw 需要有效 Yaw channel。"); valid = false; }
        if (translationPolicy == PlayerMotionTranslationPolicy.LocalTrajectory && !profile.HasPlanarPosition) { errors?.Add(name + ": LocalTrajectory 需要有效 XZ channel。"); valid = false; }
        if (translationPolicy == PlayerMotionTranslationPolicy.TravelAlongDirection && !profile.HasTravelDistance) { errors?.Add(name + ": TravelAlongDirection 需要有效 Travel channel。"); valid = false; }
        return valid;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Unity环境中的初始化行为
    /// </summary>
    public void Configure(PlayerMotionProfile motionProfile, PlayerMotionTranslationPolicy translation, PlayerMotionRotationPolicy rotation, PlayerMotionBasisPolicy basis, PlayerMotionControlPolicy control, float runtimeDuration, float scale, float handoffStart, float handoffEnd, bool presentation = true)
    {
        profile = motionProfile;
        translationPolicy = translation;
        rotationPolicy = rotation;
        basisPolicy = basis;
        controlPolicy = control;
        durationOverride = runtimeDuration;
        translationScale = scale;
        handoffStartProgress = handoffStart;
        handoffEndProgress = handoffEnd;
        requiresPresentation = presentation;
        translationAuthority = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        rotationAuthority = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    }
#endif
    /// <summary>
    /// 依据过渡进程返回动画控制权重
    /// </summary>
    private float EvaluateAuthority(AnimationCurve curve, float motionProgress)
    {
        if (handoffEndProgress < handoffStartProgress) return 1f;
        //未开始时完全动画掌控
        if (motionProgress < handoffStartProgress) return 1f;
        //完全结束后交给程序掌控
        if (motionProgress >= handoffEndProgress) return 0f;
        //拿到0-1映射
        float handoff = CalculateHandoffProgress(motionProgress);
        
        return Mathf.Clamp01(curve == null ? 1f - handoff : curve.Evaluate(handoff));
    }
}
