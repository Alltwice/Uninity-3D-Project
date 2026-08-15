using System.Collections.Generic;
using UnityEngine;

public enum PlayerMotionTranslationPolicy
{
    None,
    VelocityDriven,
    TravelAlongDirection,
    LocalTrajectory
}

public enum PlayerMotionRotationPolicy
{
    None,
    FaceDirection,
    ProfileYaw,
    KeepFacing
}

public enum PlayerMotionBasisPolicy
{
    DesiredDirection,
    EntryVelocityDirection,
    EntryFacing
}

public enum PlayerMotionControlPolicy
{
    None,
    Turn180,
    Dodge
}

[CreateAssetMenu(fileName = "PlayerMotionDefinition", menuName = "Player/Motion/Definition")]
public sealed class PlayerMotionDefinition : ScriptableObject
{
    [SerializeField] private PlayerMotionProfile profile;
    [SerializeField] private PlayerMotionTranslationPolicy translationPolicy;
    [SerializeField] private PlayerMotionRotationPolicy rotationPolicy;
    [SerializeField] private PlayerMotionBasisPolicy basisPolicy;
    [SerializeField] private PlayerMotionControlPolicy controlPolicy;
    [Min(0f)] [SerializeField] private float durationOverride;
    [Min(0f)] [SerializeField] private float translationScale = 1f;
    [Range(0f, 1f)] [SerializeField] private float handoffStartProgress = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float handoffEndProgress = 1f;
    [SerializeField] private AnimationCurve translationAuthority = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve rotationAuthority = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private bool requiresPresentation = true;

    public PlayerMotionProfile Profile => profile;
    public PlayerMotionTranslationPolicy TranslationPolicy => translationPolicy;
    public PlayerMotionRotationPolicy RotationPolicy => rotationPolicy;
    public PlayerMotionBasisPolicy BasisPolicy => basisPolicy;
    public PlayerMotionControlPolicy ControlPolicy => controlPolicy;
    public float Duration => durationOverride > 0f ? durationOverride : profile == null ? 0f : profile.Duration;
    public float TranslationScale => translationScale;
    public float HandoffStartProgress => handoffStartProgress;
    public float HandoffEndProgress => handoffEndProgress;
    public bool RequiresPresentation => requiresPresentation;

    public float CalculateHandoffProgress(float motionProgress)
    {
        if (handoffEndProgress <= handoffStartProgress) return motionProgress >= handoffEndProgress ? 1f : 0f;
        return Mathf.Clamp01((motionProgress - handoffStartProgress) / (handoffEndProgress - handoffStartProgress));
    }

    public float EvaluateTranslationAuthority(float motionProgress) => EvaluateAuthority(translationAuthority, motionProgress);
    public float EvaluateRotationAuthority(float motionProgress) => EvaluateAuthority(rotationAuthority, motionProgress);

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

    private float EvaluateAuthority(AnimationCurve curve, float motionProgress)
    {
        if (handoffEndProgress <= handoffStartProgress) return 1f;
        if (motionProgress < handoffStartProgress) return 1f;
        if (motionProgress >= handoffEndProgress) return 0f;
        float handoff = CalculateHandoffProgress(motionProgress);
        return Mathf.Clamp01(curve == null ? 1f - handoff : curve.Evaluate(handoff));
    }
}
