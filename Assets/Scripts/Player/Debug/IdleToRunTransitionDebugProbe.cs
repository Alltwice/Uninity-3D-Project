using System;
using System.Collections.Generic;
using System.Reflection;
using Animancer;
using UnityEngine;
using Unity.Profiling;

public struct IdleToRunMixerChildDebugSample
{
    public bool Valid;
    public string ClipName;
    public float NormalizedTime;
    public float Weight;
    public float Speed;
    public float EffectiveSpeed;
    public bool IsPlaying;
    public bool IsSynchronized;
}

public struct IdleToRunBoneDebugSample
{
    public bool Valid;
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public Vector3 LocalVelocity;
    public float AngularSpeed;
}

public struct IdleToRunTransitionDebugSample
{
    public int TestId;
    public int Frame;
    public int TestFrame;
    public int ObservedStallMarkerId;
    public bool ObservedStallMarker;
    public float Time;
    public float UnscaledTime;
    public string MotionName;
    public float Progress;
    public float PreviousProgress;
    public float DeltaTime;
    public Vector3 AuthoredPlanarDisplacement;
    public float AuthoredDistance;
    public float TranslationAuthority;
    public bool MotionCompleted;
    public PlayerMotorTranslationMode TranslationMode;
    public Vector3 CommandDisplacement;
    public float CommandDistance;
    public float CommandVelocity;
    public Vector3 TargetVelocity;
    public Vector3 PredictedVelocity;
    public Vector3 ActualPlanarDisplacement;
    public Vector3 ActualVelocity;
    public float ActualSpeed;
    public float IdleToRunWeight;
    public float GroundLocomotionWeight;
    public float PoseTransitionProgress;
    public string BoundaryClipName;
    public float BoundaryNormalizedTime;
    public float BoundarySpeed;
    public float BoundaryEffectiveSpeed;
    public bool BoundaryIsPlaying;
    public string GroundStateName;
    public float GroundNormalizedTime;
    public float GroundParameter;
    public float GroundSpeed;
    public float GroundEffectiveSpeed;
    public bool GroundIsPlaying;
    public int GroundChildCount;
    public int GroundSynchronizedChildCount;
    public IdleToRunMixerChildDebugSample GroundChild0;
    public IdleToRunMixerChildDebugSample GroundChild1;
    public bool AnimatorIsHuman;
    public Vector3 AnimatorLocalPosition;
    public Quaternion AnimatorLocalRotation;
    public IdleToRunBoneDebugSample Hips;
    public IdleToRunBoneDebugSample LeftFoot;
    public IdleToRunBoneDebugSample RightFoot;
    public float UnscaledDeltaTime;
    public float SmoothDeltaTime;
    public float MainThreadTimeMs;
    public long GcAllocatedBytes;
    public int GcCollectionCount0;
    public int GcCollectionCount1;
    public int GcCollectionCount2;
    public bool InTransitionWindow;
    public bool PotentialMovementStall;
    public bool PoseWeightAnomaly;
    public float SpeedDropPercent;
    public string Diagnostic;
}

/// <summary>
/// 在 PlayerSimulationDriver 完成本帧模拟和 Animancer 手动求值后，只读采集 IdleToRun handoff 数据。
/// </summary>
[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(PlayerSimulationDriver), typeof(PlayerMotionPlanner), typeof(PlayerMotor))]
public class IdleToRunTransitionDebugProbe : MonoBehaviour
{
    private const string IdleToRunMotionName = "IdleToRun";
    private struct BoneHistory
    {
        public bool Valid;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }

    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo RuntimeField = RequireField(typeof(PlayerMotionPlanner), "runtime");
    private static readonly FieldInfo DefinitionField = RequireField(typeof(PlayerMotionRuntime), "definition");
    private static readonly FieldInfo BasisField = RequireField(typeof(PlayerMotionRuntime), "basis");
    private static readonly FieldInfo TravelDirectionField = RequireField(typeof(PlayerMotionRuntime), "travelDirection");
    private static readonly FieldInfo PreviousProgressField = RequireField(typeof(PlayerMotionRuntime), "previousProgress");
    private static readonly FieldInfo CurrentProgressField = RequireField(typeof(PlayerMotionRuntime), "currentProgress");
    private static readonly FieldInfo InputSourceField = RequireField(typeof(PlayerSimulationDriver), "inputSource");
    private static readonly FieldInfo MovementReferenceField = RequireField(typeof(PlayerSimulationDriver), "movementReference");
    private static readonly FieldInfo BoundaryStateField = RequireField(typeof(PlayerAnimationController), "boundaryState");
    private static readonly FieldInfo HandoffLoopStateField = RequireField(typeof(PlayerAnimationController), "handoffLoopState");
    private static readonly FieldInfo ActiveBindingField = RequireField(typeof(PlayerAnimationController), "activeBinding");
    private static readonly FieldInfo GroundLocomotionTransitionField = RequireField(typeof(PlayerAnimationController), "groundLocomotionTransition");

    [SerializeField] private IdleToRunTransitionDebugTestRunner testRunner;
    [SerializeField] private IdleToRunTransitionDebugLogger logger;

    private PlayerSimulationDriver simulationDriver;
    private PlayerMotionPlanner motionPlanner;
    private PlayerMotor motor;
    private PlayerStateController stateController;
    private PlayerAnimationController animationController;
    private Transform movementReference;
    private Animator animator;
    private Transform hips;
    private Transform leftFoot;
    private Transform rightFoot;
    private object runtime;
    private PlayerMotorResult previousMotorResult;
    private bool hasPreviousMotorResult;
    private int postCompletionFramesRemaining;
    private float lastIdleToRunProgress;
    private AnimancerState lastIdleToRunBoundaryState;
    private BoneHistory hipsHistory;
    private BoneHistory leftFootHistory;
    private BoneHistory rightFootHistory;
    private ProfilerRecorder mainThreadTimeRecorder;
    private ProfilerRecorder gcAllocatedRecorder;
    private readonly Dictionary<PlayerMotionDefinition, string> motionNames = new Dictionary<PlayerMotionDefinition, string>();

    public void Configure(IdleToRunTransitionDebugTestRunner runner, IdleToRunTransitionDebugLogger debugLogger)
    {
        testRunner = runner;
        logger = debugLogger;
    }

    private void Awake()
    {
        simulationDriver = GetComponent<PlayerSimulationDriver>();
        motionPlanner = GetComponent<PlayerMotionPlanner>();
        motor = GetComponent<PlayerMotor>();
        stateController = GetComponent<PlayerStateController>();
        animationController = GetComponent<PlayerAnimationController>();
        movementReference = (Transform)MovementReferenceField.GetValue(simulationDriver);
        PlayerMotionCatalog catalog = motionPlanner.Catalog;
        for (int i = 0; catalog != null && i < catalog.Motions.Count; i++) motionNames[catalog.Motions[i].Definition] = catalog.Motions[i].Id.ToString();
        AnimancerComponent animancer = GetComponent<AnimancerComponent>();
        animator = animancer == null ? null : animancer.Animator;
        if (animator != null && animator.isHuman)
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }
        runtime = RuntimeField.GetValue(motionPlanner);
        mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        gcAllocatedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
    }

    private void LateUpdate()
    {
        PlayerMotorResult currentMotorResult = motor.CurrentResult;
        if (!hasPreviousMotorResult)
        {
            previousMotorResult = currentMotorResult;
            hasPreviousMotorResult = true;
        }
        if (testRunner != null && logger != null && testRunner.IsCapturing)
        {
            IdleToRunTransitionDebugSample sample = BuildSample(currentMotorResult);
            logger.Record(sample);
        }
        previousMotorResult = currentMotorResult;
    }

    private IdleToRunTransitionDebugSample BuildSample(PlayerMotorResult currentMotorResult)
    {
        float deltaTime = Time.deltaTime;
        PlayerMotionSnapshot snapshot = motionPlanner.Snapshot;
        PlayerMotionFrame motionFrame = BuildMotionFrame(snapshot);
        string motionName = ResolveMotionName(motionFrame.Definition);
        bool isIdleToRun = motionName == IdleToRunMotionName;
        if (isIdleToRun) lastIdleToRunProgress = motionFrame.CurrentProgress;
        if (snapshot.JustCompleted && isIdleToRun) postCompletionFramesRemaining = 8;
        else if (!isIdleToRun && postCompletionFramesRemaining > 0) postCompletionFramesRemaining--;
        PlayerGameplayIntent intent = BuildIntent();
        Vector3 predictedVelocity = PlayerMotionComposer.CalculateVelocity(previousMotorResult.HorizontalVelocity, intent.DesiredMoveDirection * ResolveTargetSpeed(intent.LocomotionMode), intent.LocomotionMode, motor.Config.Locomotion, deltaTime);
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, motionFrame, previousMotorResult, motor.Config, deltaTime, transform.forward);
        Vector3 commandDisplacement = command.TranslationMode == PlayerMotorTranslationMode.VelocityDriven ? predictedVelocity * deltaTime : command.PlanarDisplacement;
        IdleToRunTransitionDebugSample sample = new IdleToRunTransitionDebugSample();
        ReadAnimationState(ref sample);
        ReadPoseState(ref sample, deltaTime);
        float progress = motionFrame.IsValid ? motionFrame.CurrentProgress : postCompletionFramesRemaining > 0 ? lastIdleToRunProgress : 0f;
        float previousProgress = motionFrame.IsValid ? motionFrame.PreviousProgress : progress;
        float authority = motionFrame.IsValid ? motionFrame.TranslationAuthority : 0f;
        float poseProgress = motionFrame.IsValid ? snapshot.HandoffProgress : postCompletionFramesRemaining > 0 ? 1f : 0f;
        bool transitionWindow = isIdleToRun && motionFrame.CurrentProgress >= motionFrame.Definition.HandoffStartProgress || postCompletionFramesRemaining > 0;
        sample.TestId = testRunner.CurrentTestId;
        sample.Frame = Time.frameCount;
        sample.TestFrame = testRunner.CurrentTestFrame;
        sample.ObservedStallMarkerId = testRunner.ObservedStallMarkerId;
        sample.ObservedStallMarker = testRunner.ObservedStallMarkedThisFrame;
        sample.Time = Time.time;
        sample.UnscaledTime = Time.unscaledTime;
        sample.MotionName = motionName;
        sample.Progress = progress;
        sample.PreviousProgress = previousProgress;
        sample.DeltaTime = deltaTime;
        sample.AuthoredPlanarDisplacement = motionFrame.AuthoredPlanarDisplacement;
        sample.AuthoredDistance = motionFrame.AuthoredPlanarDisplacement.magnitude;
        sample.TranslationAuthority = authority;
        sample.MotionCompleted = snapshot.JustCompleted;
        sample.TranslationMode = command.TranslationMode;
        sample.CommandDisplacement = commandDisplacement;
        sample.CommandDistance = commandDisplacement.magnitude;
        sample.CommandVelocity = deltaTime > 0f ? commandDisplacement.magnitude / deltaTime : 0f;
        sample.TargetVelocity = command.TargetPlanarVelocity;
        sample.PredictedVelocity = predictedVelocity;
        sample.ActualPlanarDisplacement = currentMotorResult.ActualPlanarDisplacement;
        sample.ActualVelocity = currentMotorResult.HorizontalVelocity;
        sample.ActualSpeed = currentMotorResult.HorizontalSpeed;
        sample.PoseTransitionProgress = poseProgress;
        sample.UnscaledDeltaTime = Time.unscaledDeltaTime;
        sample.SmoothDeltaTime = Time.smoothDeltaTime;
        sample.MainThreadTimeMs = mainThreadTimeRecorder.Valid ? mainThreadTimeRecorder.LastValue * 0.000001f : 0f;
        sample.GcAllocatedBytes = gcAllocatedRecorder.Valid ? gcAllocatedRecorder.LastValue : 0L;
        sample.GcCollectionCount0 = GC.CollectionCount(0);
        sample.GcCollectionCount1 = GC.CollectionCount(1);
        sample.GcCollectionCount2 = GC.CollectionCount(2);
        sample.InTransitionWindow = transitionWindow;
        return sample;
    }

    private PlayerMotionFrame BuildMotionFrame(PlayerMotionSnapshot snapshot)
    {
        PlayerMotionDefinition definition = (PlayerMotionDefinition)DefinitionField.GetValue(runtime);
        if (definition == null || definition.Profile == null || !snapshot.IsActive && !snapshot.JustCompleted) return default;
        float previousProgress = (float)PreviousProgressField.GetValue(runtime);
        float currentProgress = (float)CurrentProgressField.GetValue(runtime);
        Quaternion basis = (Quaternion)BasisField.GetValue(runtime);
        Vector3 travelDirection = (Vector3)TravelDirectionField.GetValue(runtime);
        PlayerMotionProfile profile = definition.Profile;
        Vector3 authoredTranslation;
        switch (definition.TranslationPolicy)
        {
            case PlayerMotionTranslationPolicy.TravelAlongCapturedDirection:
            case PlayerMotionTranslationPolicy.TravelAlongDesiredDirection:
                authoredTranslation = travelDirection * ((profile.EvaluateTravelDistance(currentProgress) - profile.EvaluateTravelDistance(previousProgress)) * definition.TranslationScale);
                break;
            case PlayerMotionTranslationPolicy.LocalTrajectory:
                authoredTranslation = basis * ((profile.EvaluatePlanarPosition(currentProgress) - profile.EvaluatePlanarPosition(previousProgress)) * definition.TranslationScale);
                break;
            default:
                authoredTranslation = Vector3.zero;
                break;
        }
        float authoredYaw = definition.RotationPolicy == PlayerMotionRotationPolicy.ProfileYaw ? profile.EvaluateYaw(currentProgress) - profile.EvaluateYaw(previousProgress) : 0f;
        float remainingAuthoredYaw = definition.RotationPolicy == PlayerMotionRotationPolicy.ProfileYaw ? profile.EvaluateYaw(1f) - profile.EvaluateYaw(currentProgress) : 0f;
        return new PlayerMotionFrame(definition, authoredTranslation, authoredYaw, remainingAuthoredYaw, previousProgress, currentProgress, definition.EvaluateTranslationAuthority(currentProgress));
    }

    private PlayerGameplayIntent BuildIntent()
    {
        IPlayerInputSource inputSource = (IPlayerInputSource)InputSourceField.GetValue(simulationDriver);
        Vector2 moveInput = inputSource == null ? Vector2.zero : inputSource.MoveInput;
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        if (input.sqrMagnitude > 1f) input.Normalize();
        Vector3 forward = movementReference.forward;
        Vector3 right = movementReference.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        Vector3 desiredMoveDirection = forward * input.z + right * input.x;
        if (desiredMoveDirection.sqrMagnitude > 1f) desiredMoveDirection.Normalize();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(desiredMoveDirection, transform.forward);
        intent.LocomotionMode = stateController.CurrentLocomotionMode;
        return intent;
    }

    private void ReadAnimationState(ref IdleToRunTransitionDebugSample sample)
    {
        AnimancerState boundaryState = (AnimancerState)BoundaryStateField.GetValue(animationController);
        AnimancerState handoffLoopState = (AnimancerState)HandoffLoopStateField.GetValue(animationController);
        PlayerMotionAnimationBinding activeBinding = (PlayerMotionAnimationBinding)ActiveBindingField.GetValue(animationController);
        LinearMixerTransition groundTransition = (LinearMixerTransition)GroundLocomotionTransitionField.GetValue(animationController);
        bool boundaryIsIdleToRun = activeBinding != null && ResolveMotionName(activeBinding.Definition) == IdleToRunMotionName;
        if (boundaryIsIdleToRun && boundaryState != null) lastIdleToRunBoundaryState = boundaryState;
        else if (boundaryState == null && postCompletionFramesRemaining > 0) { boundaryState = lastIdleToRunBoundaryState; boundaryIsIdleToRun = boundaryState != null; }
        sample.IdleToRunWeight = boundaryIsIdleToRun && boundaryState != null ? boundaryState.Weight : 0f;
        if (boundaryState != null)
        {
            sample.BoundaryClipName = ResolveStateName(boundaryState);
            sample.BoundaryNormalizedTime = boundaryState.NormalizedTime;
            sample.BoundarySpeed = boundaryState.Speed;
            sample.BoundaryEffectiveSpeed = boundaryState.EffectiveSpeed;
            sample.BoundaryIsPlaying = boundaryState.IsPlaying;
        }
        AnimancerState groundState = handoffLoopState ?? groundTransition?.State;
        sample.GroundLocomotionWeight = groundState == null ? 0f : groundState.Weight;
        if (groundState == null) return;
        sample.GroundStateName = ResolveStateName(groundState);
        sample.GroundNormalizedTime = groundState.NormalizedTime;
        sample.GroundSpeed = groundState.Speed;
        sample.GroundEffectiveSpeed = groundState.EffectiveSpeed;
        sample.GroundIsPlaying = groundState.IsPlaying;
        LinearMixerState mixer = groundState as LinearMixerState;
        if (mixer == null) return;
        sample.GroundParameter = mixer.Parameter;
        sample.GroundChildCount = mixer.ChildCount;
        sample.GroundSynchronizedChildCount = mixer.SynchronizedChildCount;
        sample.GroundChild0 = ReadMixerChild(mixer, 0);
        sample.GroundChild1 = ReadMixerChild(mixer, 1);
    }

    private static IdleToRunMixerChildDebugSample ReadMixerChild(LinearMixerState mixer, int index)
    {
        if (index >= mixer.ChildCount) return default;
        AnimancerState child = mixer.GetChild(index);
        if (child == null) return default;
        return new IdleToRunMixerChildDebugSample
        {
            Valid = true,
            ClipName = ResolveStateName(child),
            NormalizedTime = child.NormalizedTime,
            Weight = child.Weight,
            Speed = child.Speed,
            EffectiveSpeed = child.EffectiveSpeed,
            IsPlaying = child.IsPlaying,
            IsSynchronized = mixer.IsSynchronized(child)
        };
    }

    private void ReadPoseState(ref IdleToRunTransitionDebugSample sample, float deltaTime)
    {
        if (animator == null) return;
        sample.AnimatorIsHuman = animator.isHuman;
        sample.AnimatorLocalPosition = animator.transform.localPosition;
        sample.AnimatorLocalRotation = animator.transform.localRotation;
        sample.Hips = ReadBone(hips, ref hipsHistory, deltaTime);
        sample.LeftFoot = ReadBone(leftFoot, ref leftFootHistory, deltaTime);
        sample.RightFoot = ReadBone(rightFoot, ref rightFootHistory, deltaTime);
    }

    private IdleToRunBoneDebugSample ReadBone(Transform bone, ref BoneHistory history, float deltaTime)
    {
        if (bone == null) return default;
        Transform animatorTransform = animator.transform;
        Vector3 localPosition = animatorTransform.InverseTransformPoint(bone.position);
        Quaternion localRotation = Quaternion.Inverse(animatorTransform.rotation) * bone.rotation;
        IdleToRunBoneDebugSample sample = new IdleToRunBoneDebugSample
        {
            Valid = true,
            LocalPosition = localPosition,
            LocalRotation = localRotation,
            LocalVelocity = history.Valid && deltaTime > 0f ? (localPosition - history.LocalPosition) / deltaTime : Vector3.zero,
            AngularSpeed = history.Valid && deltaTime > 0f ? Quaternion.Angle(history.LocalRotation, localRotation) / deltaTime : 0f
        };
        history.Valid = true;
        history.LocalPosition = localPosition;
        history.LocalRotation = localRotation;
        return sample;
    }

    private static string ResolveStateName(AnimancerState state)
    {
        if (state.Clip != null) return state.Clip.name;
        return state.MainObject == null ? state.GetType().Name : state.MainObject.name;
    }

    private string ResolveMotionName(PlayerMotionDefinition definition)
    {
        if (definition != null)
        {
            if (motionNames.TryGetValue(definition, out string motionName)) return motionName;
            return definition.name;
        }
        PlayerLocomotionMode mode = stateController.CurrentLocomotionMode;
        if (mode == PlayerLocomotionMode.Idle) return "Idle";
        if (mode == PlayerLocomotionMode.Walk || mode == PlayerLocomotionMode.Run) return "GroundLocomotion";
        return mode.ToString();
    }

    private float ResolveTargetSpeed(PlayerLocomotionMode mode)
    {
        PlayerMovementConfig.LocomotionSettings settings = motor.Config.Locomotion;
        switch (mode)
        {
            case PlayerLocomotionMode.Walk: return settings.WalkSpeed;
            case PlayerLocomotionMode.Run: return settings.RunSpeed;
            case PlayerLocomotionMode.FastRun: return settings.FastRunSpeed;
            case PlayerLocomotionMode.Air: return settings.AirMoveSpeed;
            default: return 0f;
        }
    }

    private static FieldInfo RequireField(Type type, string name)
    {
        return type.GetField(name, InstancePrivate) ?? throw new MissingFieldException(type.FullName, name);
    }

    private void OnDestroy()
    {
        mainThreadTimeRecorder.Dispose();
        gcAllocatedRecorder.Dispose();
    }
}
