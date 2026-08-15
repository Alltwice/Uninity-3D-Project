using UnityEngine;

public readonly struct PlayerMotionFrame
{
    public PlayerMotionFrame(PlayerMotionDefinition definition, Vector3 authoredPlanarDisplacement, float authoredYawDelta, float translationAuthority, float rotationAuthority)
    {
        Definition = definition;
        AuthoredPlanarDisplacement = authoredPlanarDisplacement;
        AuthoredYawDelta = authoredYawDelta;
        TranslationAuthority = translationAuthority;
        RotationAuthority = rotationAuthority;
    }

    public PlayerMotionDefinition Definition { get; }
    public Vector3 AuthoredPlanarDisplacement { get; }
    public float AuthoredYawDelta { get; }
    public float TranslationAuthority { get; }
    public float RotationAuthority { get; }
    public bool IsValid => Definition != null;
}

public readonly struct PlayerMotionSnapshot
{
    public PlayerMotionSnapshot(PlayerMotionDefinition activeDefinition, ulong instanceId, float progress, float handoffProgress, bool handoffActive, bool isActive, bool justCompleted, bool justCancelled, float translationAuthority, float rotationAuthority)
    {
        ActiveDefinition = activeDefinition;
        InstanceId = instanceId;
        Progress = progress;
        HandoffProgress = handoffProgress;
        HandoffActive = handoffActive;
        IsActive = isActive;
        JustCompleted = justCompleted;
        JustCancelled = justCancelled;
        TranslationAuthority = translationAuthority;
        RotationAuthority = rotationAuthority;
    }

    public PlayerMotionDefinition ActiveDefinition { get; }
    public ulong InstanceId { get; }
    public float Progress { get; }
    public float HandoffProgress { get; }
    public bool HandoffActive { get; }
    public bool IsActive { get; }
    public bool JustCompleted { get; }
    public bool JustCancelled { get; }
    public float TranslationAuthority { get; }
    public float RotationAuthority { get; }
}

public sealed class PlayerMotionRuntime
{
    private PlayerMotionDefinition definition;
    private Quaternion basis = Quaternion.identity;
    private Vector3 travelDirection;
    private Vector3 requestDirection;
    private ulong sequence;
    private ulong instanceId;
    private float elapsedTime;
    private float previousProgress;
    private float currentProgress;
    private bool isActive;
    private bool justCompleted;
    private bool justCancelled;
    private bool rotationReleased;
    private PlayerMotionFrame currentFrame;

    public PlayerMotionFrame CurrentFrame => currentFrame;
    public PlayerMotionSnapshot Snapshot => BuildSnapshot();

    public void BeginFrame()
    {
        if (!isActive && (justCompleted || justCancelled)) definition = null;
        justCompleted = false;
        justCancelled = false;
        currentFrame = default;
    }

    public ulong Begin(PlayerMotionDefinition nextDefinition, Vector3 basisDirection, Vector3 initialTravelDirection, Vector3 initialRequestDirection, float startProgress = 0f)
    {
        bool replaced = isActive;
        definition = nextDefinition;
        instanceId = ++sequence;
        elapsedTime = Mathf.Clamp01(startProgress) * (definition == null ? 0f : definition.Duration);
        previousProgress = Mathf.Clamp01(startProgress);
        currentProgress = previousProgress;
        basisDirection = NormalizePlanar(basisDirection, Vector3.forward);
        travelDirection = NormalizePlanar(initialTravelDirection, basisDirection);
        requestDirection = NormalizePlanar(initialRequestDirection, travelDirection);
        basis = Quaternion.LookRotation(basisDirection, Vector3.up);
        rotationReleased = false;
        justCompleted = false;
        justCancelled = replaced;
        isActive = definition != null && definition.Profile != null && definition.Duration > 0f;
        currentFrame = default;
        return instanceId;
    }

    public void Cancel()
    {
        if (!isActive) return;
        isActive = false;
        justCompleted = false;
        justCancelled = true;
        currentFrame = default;
    }

    public PlayerMotionFrame Advance(float deltaTime, PlayerGameplayIntent intent, Vector3 currentFacing, float turnIntentTolerance, float turnRotationUnlockAngle)
    {
        if (!isActive || definition == null) return default;
        if (definition.ControlPolicy == PlayerMotionControlPolicy.Turn180)
        {
            Vector3 desired = NormalizePlanar(intent.DesiredMoveDirection, Vector3.zero);
            if (desired.sqrMagnitude < 0.0001f || Vector3.Angle(requestDirection, desired) > turnIntentTolerance)
            {
                Cancel();
                return default;
            }
            if (!rotationReleased && Vector3.Angle(NormalizePlanar(currentFacing, requestDirection), desired) <= turnRotationUnlockAngle) rotationReleased = true;
        }
        else if (definition.ControlPolicy == PlayerMotionControlPolicy.Dodge && intent.DesiredMoveDirection.sqrMagnitude > 0.0001f)
        {
            travelDirection = NormalizePlanar(intent.DesiredMoveDirection, travelDirection);
        }
        previousProgress = currentProgress;
        elapsedTime = Mathf.Min(definition.Duration, elapsedTime + Mathf.Max(0f, deltaTime));
        currentProgress = definition.Duration > 0f ? Mathf.Clamp01(elapsedTime / definition.Duration) : 1f;
        PlayerMotionProfile profile = definition.Profile;
        Vector3 authoredTranslation = EvaluateTranslation(profile, definition, previousProgress, currentProgress);
        float authoredYaw = definition.RotationPolicy == PlayerMotionRotationPolicy.ProfileYaw ? profile.EvaluateYaw(currentProgress) - profile.EvaluateYaw(previousProgress) : 0f;
        float translationWeight = definition.EvaluateTranslationAuthority(currentProgress);
        float rotationWeight = rotationReleased ? 0f : definition.EvaluateRotationAuthority(currentProgress);
        currentFrame = new PlayerMotionFrame(definition, authoredTranslation, authoredYaw, translationWeight, rotationWeight);
        if (currentProgress >= 1f)
        {
            isActive = false;
            justCompleted = true;
        }
        return currentFrame;
    }

    private Vector3 EvaluateTranslation(PlayerMotionProfile profile, PlayerMotionDefinition motionDefinition, float fromProgress, float toProgress)
    {
        switch (motionDefinition.TranslationPolicy)
        {
            case PlayerMotionTranslationPolicy.TravelAlongDirection:
                return travelDirection * ((profile.EvaluateTravelDistance(toProgress) - profile.EvaluateTravelDistance(fromProgress)) * motionDefinition.TranslationScale);
            case PlayerMotionTranslationPolicy.LocalTrajectory:
                return basis * ((profile.EvaluatePlanarPosition(toProgress) - profile.EvaluatePlanarPosition(fromProgress)) * motionDefinition.TranslationScale);
            default:
                return Vector3.zero;
        }
    }

    private PlayerMotionSnapshot BuildSnapshot()
    {
        float handoff = definition == null ? 0f : definition.CalculateHandoffProgress(currentProgress);
        bool handoffActive = definition != null && currentProgress >= definition.HandoffStartProgress;
        return new PlayerMotionSnapshot(definition, instanceId, currentProgress, handoff, handoffActive, isActive, justCompleted, justCancelled, currentFrame.TranslationAuthority, currentFrame.RotationAuthority);
    }

    private static Vector3 NormalizePlanar(Vector3 value, Vector3 fallback)
    {
        value.y = 0f;
        if (value.sqrMagnitude > 0.0001f) return value.normalized;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.zero;
    }
}

public static class PlayerMotionMath
{
    public static float UnwrapYaw(float previousWrappedYaw, float currentWrappedYaw, float previousUnwrappedYaw)
    {
        return previousUnwrappedYaw + Mathf.DeltaAngle(previousWrappedYaw, currentWrappedYaw);
    }
}

public static class PlayerMotionPresentationPhase
{
    public static float ResolveBoundaryProgress(PlayerMotionSnapshot snapshot) => snapshot.Progress;
}
