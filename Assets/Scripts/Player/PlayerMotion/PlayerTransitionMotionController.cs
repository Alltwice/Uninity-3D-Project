using UnityEngine;

public enum PlayerTransitionMotionType
{
    None,
    RunStart,
    RunStop
}

[RequireComponent(typeof(PlayerMotor))]
public sealed class PlayerTransitionMotionController : MonoBehaviour
{
    [SerializeField] private PlayerMotionConfig config;

    private PlayerMotor playerMotor;
    private PlayerMotionProfile currentProfile;
    private PlayerTransitionMotionType currentMotionType;
    private Quaternion motionBasis = Quaternion.identity;
    private float elapsedTime;
    private float previousProgress;
    private ulong playbackSequence;
    private bool rotateTowardsDesiredDirection;

    public PlayerMotionConfig Config => config;
    public PlayerMotionProfile CurrentProfile => currentProfile;
    public PlayerTransitionMotionType CurrentMotionType => currentMotionType;
    public bool IsActive => currentProfile != null;
    public float Progress => currentProfile == null || currentProfile.Duration <= 0f ? 0f : Mathf.Clamp01(elapsedTime / currentProfile.Duration);
    public float CurrentCumulativeDistance => currentProfile == null ? 0f : currentProfile.EvaluateTravelDistance(Progress);
    public float LastFrameDeltaDistance { get; private set; }
    public Vector3 LastActualFrameDisplacement { get; private set; }

    private void Awake()
    {
        playerMotor = GetComponent<PlayerMotor>();
    }

    public bool PlayTransition(PlayerStateTransition transition, bool isStandardRunTransition)
    {
        Cancel();
        if (!isStandardRunTransition || config.Mode != RunTransitionMotionMode.ProfileDriven) return false;
        PlayerMotionProfile profile;
        PlayerTransitionMotionType motionType;
        bool redirectToDesiredDirection;
        if (transition.PreviousStateType == typeof(PlayerIdleState) && transition.CurrentStateType == typeof(PlayerRunState))
        {
            profile = config.RunStartProfile;
            motionType = PlayerTransitionMotionType.RunStart;
            redirectToDesiredDirection = true;
            motionBasis = Quaternion.identity;
        }
        else if (transition.PreviousStateType == typeof(PlayerRunState) && transition.CurrentStateType == typeof(PlayerIdleState))
        {
            profile = config.RunStopProfile;
            motionType = PlayerTransitionMotionType.RunStop;
            redirectToDesiredDirection = false;
            Vector3 basisForward = playerMotor.HorizontalMoveDirection;
            if (basisForward.sqrMagnitude < 0.001f) basisForward = transform.forward;
            basisForward.y = 0f;
            motionBasis = Quaternion.LookRotation(basisForward.normalized, Vector3.up);
        }
        else
        {
            return false;
        }
        if (profile == null || profile.Duration <= 0f) return false;
        currentProfile = profile;
        currentMotionType = motionType;
        rotateTowardsDesiredDirection = redirectToDesiredDirection;
        elapsedTime = 0f;
        previousProgress = 0f;
        LastFrameDeltaDistance = 0f;
        LastActualFrameDisplacement = Vector3.zero;
        ++playbackSequence;
        playerMotor.SetMotionMode(PlayerMotionMode.ProfileDriven);
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (currentProfile == null) return;
        ulong sequence = playbackSequence;
        elapsedTime = Mathf.Min(elapsedTime + deltaTime, currentProfile.Duration);
        float currentProgress = Mathf.Clamp01(elapsedTime / currentProfile.Duration);
        Vector3 planarDisplacement;
        if (currentMotionType == PlayerTransitionMotionType.RunStart)
        {
            float previousDistance = currentProfile.EvaluateTravelDistance(previousProgress);
            float currentDistance = currentProfile.EvaluateTravelDistance(currentProgress);
            planarDisplacement = playerMotor.DesiredMoveDirection * (currentDistance - previousDistance);
        }
        else
        {
            Vector3 previousLocal = currentProfile.EvaluateLocalPosition(previousProgress);
            Vector3 currentLocal = currentProfile.EvaluateLocalPosition(currentProgress);
            planarDisplacement = motionBasis * (currentLocal - previousLocal);
        }
        previousProgress = currentProgress;
        LastFrameDeltaDistance = planarDisplacement.magnitude;
        Vector3 positionBeforeMove = transform.position;
        playerMotor.SubmitProfileMotion(planarDisplacement, rotateTowardsDesiredDirection);
        LastActualFrameDisplacement = transform.position - positionBeforeMove;
        LastActualFrameDisplacement = Vector3.ProjectOnPlane(LastActualFrameDisplacement, Vector3.up);
        if (currentProgress >= 1f) Complete(sequence);
    }

    public void Cancel()
    {
        ++playbackSequence;
        if (currentProfile != null && playerMotor.MotionMode == PlayerMotionMode.ProfileDriven)
        {
            playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
        }
        currentProfile = null;
        currentMotionType = PlayerTransitionMotionType.None;
        elapsedTime = 0f;
        previousProgress = 0f;
        LastFrameDeltaDistance = 0f;
    }

    private void Complete(ulong sequence)
    {
        if (sequence != playbackSequence || currentProfile == null) return;
        currentProfile = null;
        currentMotionType = PlayerTransitionMotionType.None;
        playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
    }
}
