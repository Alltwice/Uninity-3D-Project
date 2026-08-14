using System.Collections.Generic;
using UnityEngine;

public enum PlayerMotionCaptureKind
{
    None,
    RunStart,
    RunStop
}

public readonly struct PlayerMotionCaptureSample
{
    public PlayerMotionCaptureSample(float time, float localX, float localZ, float travelDistance, Vector3 animatorDelta, Vector3 actualDelta)
    {
        Time = time;
        LocalX = localX;
        LocalZ = localZ;
        TravelDistance = travelDistance;
        AnimatorDelta = animatorDelta;
        ActualDelta = actualDelta;
    }

    public float Time { get; }
    public float LocalX { get; }
    public float LocalZ { get; }
    public float TravelDistance { get; }
    public Vector3 AnimatorDelta { get; }
    public Vector3 ActualDelta { get; }
}

public sealed class PlayerMotionCaptureData
{
    public PlayerMotionCaptureData(PlayerMotionCaptureKind kind, float duration, IReadOnlyList<PlayerMotionCaptureSample> samples)
    {
        Kind = kind;
        Duration = duration;
        Samples = samples;
    }

    public PlayerMotionCaptureKind Kind { get; }
    public float Duration { get; }
    public IReadOnlyList<PlayerMotionCaptureSample> Samples { get; }
}

[RequireComponent(typeof(PlayerMotor), typeof(PlayerTransitionMotionController))]
public sealed class PlayerMotionCaptureRecorder : MonoBehaviour
{
    private readonly List<PlayerMotionCaptureSample> samples = new List<PlayerMotionCaptureSample>();
    private PlayerTransitionMotionController transitionMotionController;
    private PlayerMotor playerMotor;
    private PlayerMotionCaptureKind armedKind;
    private PlayerMotionCaptureKind capturingKind;
    private Quaternion inverseCaptureStartRotation;
    private float elapsedTime;
    private float cumulativeLocalX;
    private float cumulativeLocalZ;
    private float cumulativeTravelDistance;

    public PlayerMotionConfig Config => transitionMotionController.Config;
    public PlayerMotionCaptureKind ArmedKind => armedKind;
    public PlayerMotionCaptureKind CapturingKind => capturingKind;
    public PlayerMotionCaptureData CompletedCapture { get; private set; }

    private void Awake()
    {
        transitionMotionController = GetComponent<PlayerTransitionMotionController>();
        playerMotor = GetComponent<PlayerMotor>();
    }

    private void Update()
    {
        if (capturingKind != PlayerMotionCaptureKind.None && playerMotor.MotionMode != PlayerMotionMode.AnimationDriven)
        {
            FinishCapture();
        }
    }

    public void ArmRunStartCapture()
    {
        Arm(PlayerMotionCaptureKind.RunStart);
    }

    public void ArmRunStopCapture()
    {
        Arm(PlayerMotionCaptureKind.RunStop);
    }

    public void CancelCapture()
    {
        armedKind = PlayerMotionCaptureKind.None;
        capturingKind = PlayerMotionCaptureKind.None;
        samples.Clear();
    }

    public void HandleTransition(PlayerStateTransition transition, bool isStandardRunTransition)
    {
        if (capturingKind != PlayerMotionCaptureKind.None) FinishCapture();
        if (armedKind == PlayerMotionCaptureKind.None || !isStandardRunTransition || Config.Mode != RunTransitionMotionMode.RuntimeRootMotion) return;
        bool matches = armedKind == PlayerMotionCaptureKind.RunStart && transition.PreviousStateType == typeof(PlayerIdleState) && transition.CurrentStateType == typeof(PlayerRunState) ||
                       armedKind == PlayerMotionCaptureKind.RunStop && transition.PreviousStateType == typeof(PlayerRunState) && transition.CurrentStateType == typeof(PlayerIdleState);
        if (!matches || playerMotor.MotionMode != PlayerMotionMode.AnimationDriven) return;
        BeginCapture(armedKind);
    }

    public void RecordAnimatorMotion(Vector3 animatorDeltaPosition, Vector3 actualDisplacement, float deltaTime)
    {
        if (capturingKind == PlayerMotionCaptureKind.None) return;
        if (playerMotor.MotionMode != PlayerMotionMode.AnimationDriven)
        {
            FinishCapture();
            return;
        }
        Vector3 horizontalRootDelta = Vector3.ProjectOnPlane(animatorDeltaPosition, Vector3.up);
        Vector3 localDelta = inverseCaptureStartRotation * horizontalRootDelta;
        cumulativeLocalX += localDelta.x;
        cumulativeLocalZ += localDelta.z;
        cumulativeTravelDistance += horizontalRootDelta.magnitude;
        elapsedTime += deltaTime;
        samples.Add(new PlayerMotionCaptureSample(elapsedTime, cumulativeLocalX, cumulativeLocalZ, cumulativeTravelDistance, horizontalRootDelta, Vector3.ProjectOnPlane(actualDisplacement, Vector3.up)));
    }

    public void ConsumeCompletedCapture()
    {
        CompletedCapture = null;
    }

    private void Arm(PlayerMotionCaptureKind kind)
    {
        CancelCapture();
        CompletedCapture = null;
        armedKind = kind;
    }

    private void BeginCapture(PlayerMotionCaptureKind kind)
    {
        armedKind = PlayerMotionCaptureKind.None;
        capturingKind = kind;
        inverseCaptureStartRotation = Quaternion.Inverse(transform.rotation);
        elapsedTime = 0f;
        cumulativeLocalX = 0f;
        cumulativeLocalZ = 0f;
        cumulativeTravelDistance = 0f;
        samples.Clear();
        samples.Add(new PlayerMotionCaptureSample(0f, 0f, 0f, 0f, Vector3.zero, Vector3.zero));
    }

    private void FinishCapture()
    {
        if (capturingKind == PlayerMotionCaptureKind.None) return;
        CompletedCapture = new PlayerMotionCaptureData(capturingKind, elapsedTime, samples.ToArray());
        capturingKind = PlayerMotionCaptureKind.None;
    }
}
