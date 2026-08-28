using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerMotionRuntimeTests
{
    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void ProfileEvaluator_TotalDisplacementIsFrameRateIndependent(int fps)
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.forward);
        Vector3 total = Vector3.zero;
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward);
        int guard = fps * 3;
        while (!runtime.Snapshot.JustCompleted && guard-- > 0) total += runtime.Advance(1f / fps, intent).AuthoredPlanarDisplacement;
        Assert.That(total.z, Is.EqualTo(profile.EvaluateTravelDistance(1f)).Within(0.0001f));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Replacement_NewInstanceOwnsCompletion()
    {
        PlayerMotionDefinition first = CreateDefinition(out PlayerMotionProfile firstProfile);
        PlayerMotionDefinition second = CreateDefinition(out PlayerMotionProfile secondProfile);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        ulong oldId = runtime.Begin(first, Vector3.forward, Vector3.forward);
        runtime.Advance(0.5f, PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward));
        ulong newId = runtime.Begin(second, Vector3.forward, Vector3.forward);
        Assert.That(newId, Is.Not.EqualTo(oldId));
        Assert.That(runtime.Snapshot.ActiveDefinition, Is.SameAs(second));
        Assert.That(runtime.Snapshot.JustCancelled, Is.True);
        runtime.Advance(1f, PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward));
        Assert.That(runtime.Snapshot.InstanceId, Is.EqualTo(newId));
        Assert.That(runtime.Snapshot.JustCompleted, Is.True);
        Object.DestroyImmediate(first);
        Object.DestroyImmediate(firstProfile);
        Object.DestroyImmediate(second);
        Object.DestroyImmediate(secondProfile);
    }

    [Test]
    public void Cancellation_JustCancelledLivesForOneSnapshotFrame()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.forward);
        runtime.Cancel();
        Assert.That(runtime.Snapshot.JustCancelled, Is.True);
        runtime.BeginFrame();
        Assert.That(runtime.Snapshot.JustCancelled, Is.False);
        Assert.That(runtime.Snapshot.ActiveDefinition, Is.Null);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Composer_UsesAuthoredAtFullAuthorityAndVelocityAtZeroAuthority()
    {
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward);
        intent.LocomotionMode = PlayerLocomotionMode.Run;
        PlayerMotorResult result = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.forward * 2f, 0f, true, false, 0f, CollisionFlags.None);
        PlayerMotorCommand authored = PlayerMotionComposer.Compose(intent, new PlayerMotionFrame(definition, Vector3.forward, 0f, 0f, 0f, 0f, 1f), result, config, 0.1f, Vector3.forward);
        PlayerMotorCommand locomotion = PlayerMotionComposer.Compose(intent, new PlayerMotionFrame(definition, Vector3.forward, 0f, 0f, 0f, 0f, 0f), result, config, 0.1f, Vector3.forward);
        Assert.That(authored.TranslationMode, Is.EqualTo(PlayerMotorTranslationMode.DisplacementDriven));
        Assert.That(authored.PlanarDisplacement.z, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(locomotion.TranslationMode, Is.EqualTo(PlayerMotorTranslationMode.VelocityDriven));
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void MotionRuntime_ReportsTransitionLockUntilConfiguredProgress()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        Assert.That(definition.TransitionLockEndProgress, Is.EqualTo(0f));
        Assert.That(definition.InterruptedExitPolicy, Is.EqualTo(PlayerMotionInterruptedExitPolicy.ResolveNormalTransitionMotion));
        definition.Configure(profile, PlayerMotionTranslationPolicy.TravelAlongCapturedDirection, PlayerMotionRotationPolicy.FaceDirection, PlayerMotionBasisPolicy.DesiredDirection, 0f, 1f, 0.8f, 1f, true, 0.6f, PlayerMotionInterruptedExitPolicy.DirectToTargetPresentation);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.forward);
        Assert.That(runtime.Snapshot.IsTransitionLocked, Is.True);
        runtime.Advance(0.5f, PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward));
        Assert.That(runtime.Snapshot.IsTransitionLocked, Is.True);
        runtime.Advance(0.11f, PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward));
        Assert.That(runtime.Snapshot.IsTransitionLocked, Is.False);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void MotionDefinition_ValidateRejectsInvalidTransitionLockProgress()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        definition.Configure(profile, PlayerMotionTranslationPolicy.TravelAlongCapturedDirection, PlayerMotionRotationPolicy.FaceDirection, PlayerMotionBasisPolicy.DesiredDirection, 0f, 1f, 0.8f, 1f, true, -0.1f);
        System.Collections.Generic.List<string> errors = new System.Collections.Generic.List<string>();
        Assert.That(definition.Validate(errors), Is.False);
        Assert.That(errors.Exists(error => error.Contains("TransitionLockEndProgress")), Is.True);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void FastRunTurnDefinitions_UseDirectPresentationAfterSixtyPercent()
    {
        PlayerMotionDefinition left = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerMotionDefinition>("Assets/Settings/Player/Motion/Definitions/FastRunTurn180LeftDefinition.asset");
        PlayerMotionDefinition right = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerMotionDefinition>("Assets/Settings/Player/Motion/Definitions/FastRunTurn180RightDefinition.asset");
        Assert.That(left.TransitionLockEndProgress, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(right.TransitionLockEndProgress, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(left.InterruptedExitPolicy, Is.EqualTo(PlayerMotionInterruptedExitPolicy.DirectToTargetPresentation));
        Assert.That(right.InterruptedExitPolicy, Is.EqualTo(PlayerMotionInterruptedExitPolicy.DirectToTargetPresentation));
    }

    [TestCase(PlayerLocomotionMode.Walk)]
    [TestCase(PlayerLocomotionMode.Run)]
    [TestCase(PlayerLocomotionMode.FastRun)]
    public void Composer_ZeroDesiredDirectionStopsGroundModes(PlayerLocomotionMode locomotionMode)
    {
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.zero, Vector3.forward);
        intent.LocomotionMode = locomotionMode;
        PlayerMotorResult result = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.forward * 3f, 0f, true, false, 0f, CollisionFlags.None);
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, default, result, config, 0.1f, Vector3.forward);
        Assert.That(command.TargetPlanarVelocity, Is.EqualTo(Vector3.zero));
        Object.DestroyImmediate(config);
    }

    [Test]
    public void DefaultMovementConfig_UsesGroundMoveInputReleaseGraceTime()
    {
        PlayerMovementConfig config = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>("Assets/Settings/Player/Motion/DefaultPlayerMovementConfig.asset");
        Assert.That(config.Locomotion.GroundMoveInputReleaseGraceTime, Is.EqualTo(0.1f).Within(0.0001f));
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void LocomotionIntent_ReleaseGraceUsesElapsedTimeAndReinputResetsIt(int fps)
    {
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        GameObject inputObject = new GameObject("LocomotionIntentTestInput");
        System.Type inputType = System.Type.GetType("PlayerInputReader, Assembly-CSharp");
        System.Type contextType = System.Type.GetType("PlayerContext, Assembly-CSharp");
        Assert.That(inputType, Is.Not.Null);
        Assert.That(contextType, Is.Not.Null);
        if (inputType == null || contextType == null)
        {
            Object.DestroyImmediate(inputObject);
            Object.DestroyImmediate(config);
            return;
        }
        Component input = inputObject.AddComponent(inputType);
        ConstructorInfo contextConstructor = null;
        foreach (ConstructorInfo constructor in contextType.GetConstructors())
        {
            if (constructor.GetParameters().Length == 5)
            {
                contextConstructor = constructor;
                break;
            }
        }
        MethodInfo updateIntent = contextType.GetMethod("UpdateLocomotionIntent");
        MethodInfo activateFastRun = contextType.GetMethod("ActivateFastRun");
        PropertyInfo hasContinuation = contextType.GetProperty("HasGroundMoveContinuationIntent");
        PropertyInfo isFastRunLatched = contextType.GetProperty("IsFastRunLatched");
        PropertyInfo isWalkMode = contextType.GetProperty("IsWalkMode");
        FieldInfo moveInput = inputType.GetField("<MoveInput>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo walkModeSignal = inputType.GetField("<IsWalkMode>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(contextConstructor, Is.Not.Null);
        Assert.That(updateIntent, Is.Not.Null);
        Assert.That(activateFastRun, Is.Not.Null);
        Assert.That(hasContinuation, Is.Not.Null);
        Assert.That(isFastRunLatched, Is.Not.Null);
        Assert.That(isWalkMode, Is.Not.Null);
        Assert.That(moveInput, Is.Not.Null);
        Assert.That(walkModeSignal, Is.Not.Null);
        if (contextConstructor == null || updateIntent == null || activateFastRun == null || hasContinuation == null || isFastRunLatched == null || isWalkMode == null || moveInput == null || walkModeSignal == null)
        {
            Object.DestroyImmediate(inputObject);
            Object.DestroyImmediate(config);
            return;
        }
        object context = contextConstructor.Invoke(new object[] { null, null, input, null, config });
        updateIntent.Invoke(context, new object[] { 0f });
        Assert.That((bool)isWalkMode.GetValue(context), Is.False);
        walkModeSignal.SetValue(input, true);
        updateIntent.Invoke(context, new object[] { 0f });
        Assert.That((bool)isWalkMode.GetValue(context), Is.True);
        moveInput.SetValue(input, Vector2.up);
        updateIntent.Invoke(context, new object[] { 0f });
        moveInput.SetValue(input, Vector2.zero);
        updateIntent.Invoke(context, new object[] { config.Locomotion.GroundMoveInputReleaseGraceTime });
        Assert.That((bool)isWalkMode.GetValue(context), Is.True);
        activateFastRun.Invoke(context, null);
        Assert.That((bool)isWalkMode.GetValue(context), Is.False);
        walkModeSignal.SetValue(input, false);
        updateIntent.Invoke(context, new object[] { 0f });
        walkModeSignal.SetValue(input, true);
        updateIntent.Invoke(context, new object[] { 0f });
        Assert.That((bool)isWalkMode.GetValue(context), Is.False);
        moveInput.SetValue(input, new Vector2(0f, 1f));
        updateIntent.Invoke(context, new object[] { 0f });
        Assert.That((bool)hasContinuation.GetValue(context), Is.True);
        Assert.That((bool)isFastRunLatched.GetValue(context), Is.True);
        moveInput.SetValue(input, Vector2.zero);
        updateIntent.Invoke(context, new object[] { config.Locomotion.GroundMoveInputReleaseGraceTime * 0.5f });
        Assert.That((bool)hasContinuation.GetValue(context), Is.True);
        moveInput.SetValue(input, Vector2.right);
        updateIntent.Invoke(context, new object[] { 1f / fps });
        moveInput.SetValue(input, Vector2.zero);
        float deltaTime = 1f / fps;
        int frames = 0;
        while ((bool)hasContinuation.GetValue(context) && frames < fps)
        {
            updateIntent.Invoke(context, new object[] { deltaTime });
            frames++;
        }
        float elapsedTime = frames * deltaTime;
        Assert.That(elapsedTime, Is.GreaterThanOrEqualTo(config.Locomotion.GroundMoveInputReleaseGraceTime - 0.0001f));
        Assert.That(elapsedTime, Is.LessThanOrEqualTo(config.Locomotion.GroundMoveInputReleaseGraceTime + deltaTime + 0.0001f));
        Assert.That((bool)hasContinuation.GetValue(context), Is.False);
        Assert.That((bool)isFastRunLatched.GetValue(context), Is.False);
        Assert.That((bool)isWalkMode.GetValue(context), Is.False);
        walkModeSignal.SetValue(input, false);
        updateIntent.Invoke(context, new object[] { 0f });
        Assert.That((bool)isWalkMode.GetValue(context), Is.True);
        Object.DestroyImmediate(inputObject);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void HardLanding_TargetsZeroHorizontalVelocityWithMoveInput()
    {
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.right, Vector3.forward);
        intent.LocomotionMode = PlayerLocomotionMode.HardLanding;
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, default, new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.forward * 3f, 0f, true, false, 0f, CollisionFlags.None), config, 0.1f, Vector3.forward);
        Assert.That(command.TargetPlanarVelocity, Is.EqualTo(Vector3.zero));
        Object.DestroyImmediate(config);
    }

    [Test]
    public void HardLanding_LocksRotationWithMoveInput()
    {
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.right, Vector3.forward);
        intent.LocomotionMode = PlayerLocomotionMode.HardLanding;
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, default, new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.zero, 0f, true, false, 0f, CollisionFlags.None), config, 0.1f, Vector3.forward);
        Assert.That(command.RotationMode, Is.EqualTo(PlayerMotorRotationMode.None));
        Object.DestroyImmediate(config);
    }

    [Test]
    public void Idle_FacesMoveInputWithSameInputAsHardLanding()
    {
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.right, Vector3.forward);
        intent.LocomotionMode = PlayerLocomotionMode.Idle;
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, default, new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.zero, 0f, true, false, 0f, CollisionFlags.None), config, 0.1f, Vector3.forward);
        Assert.That(command.RotationMode, Is.EqualTo(PlayerMotorRotationMode.FaceDirection));
        Object.DestroyImmediate(config);
    }

    [Test]
    public void HardLanding_UsesGroundDecelerationForExistingHorizontalInertia()
    {
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.right, Vector3.forward);
        intent.LocomotionMode = PlayerLocomotionMode.HardLanding;
        PlayerMotorResult result = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.forward * 10f, 0f, true, false, 0f, CollisionFlags.None);
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, default, result, config, 0.1f, Vector3.forward);
        Assert.That(command.PlanarAcceleration, Is.EqualTo(config.Locomotion.GroundDeceleration));
        Assert.That(PlayerMotionComposer.CalculateVelocity(result.HorizontalVelocity, command.TargetPlanarVelocity, PlayerLocomotionMode.HardLanding, config.Locomotion, 0.1f).magnitude, Is.EqualTo(7.5f).Within(0.0001f));
        Object.DestroyImmediate(config);
    }

    [Test]
    public void Handoff_EndpointsAndComposerHaveNoDoubleMovement()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile, 0.5f, 1f);
        Assert.That(definition.EvaluateTranslationAuthority(0.5f), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(definition.EvaluateTranslationAuthority(1f), Is.EqualTo(0f).Within(0.0001f));
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward);
        intent.LocomotionMode = PlayerLocomotionMode.Run;
        PlayerMotorResult result = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.forward * 4f, 0f, true, false, 0f, CollisionFlags.None);
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, new PlayerMotionFrame(definition, Vector3.forward, 0f, 0f, 0f, 0f, 0.5f), result, config, 0.25f, Vector3.forward);
        Assert.That(command.PlanarDisplacement.z, Is.EqualTo(1f).Within(0.0001f));
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ZeroLengthHandoff_KeepsAuthoredAuthorityThroughCompletionFrame()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile, 1f, 1f);
        Assert.That(definition.EvaluateTranslationAuthority(1f), Is.EqualTo(1f));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void ZeroLengthHandoff_ComposedDistanceIsFrameRateIndependent(int fps)
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile, 1f, 1f);
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward);
        intent.LocomotionMode = PlayerLocomotionMode.Dodge;
        PlayerMotorResult result = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.zero, 0f, true, false, 0f, CollisionFlags.None);
        runtime.Begin(definition, Vector3.forward, Vector3.forward);
        Vector3 total = Vector3.zero;
        int guard = fps * 3;
        while (!runtime.Snapshot.JustCompleted && guard-- > 0)
        {
            PlayerMotionFrame frame = runtime.Advance(1f / fps, intent);
            PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, frame, result, config, 1f / fps, Vector3.forward);
            total += command.PlanarDisplacement;
        }
        Assert.That(total.z, Is.EqualTo(profile.EvaluateTravelDistance(1f)).Within(0.0001f));
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void MotorKinematics_UsesActualDisplacement()
    {
        Vector3 actualVelocity = PlayerMotorKinematics.CalculateActualPlanarVelocity(new Vector3(0.2f, 0f, 0f), 0.1f);
        Assert.That(actualVelocity.x, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void PlayerPrefab_AllowsSubMillimeterAccelerationSteps()
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        CharacterController controller = prefab.GetComponent<CharacterController>();
        Assert.That(controller.minMoveDistance, Is.Zero, "PlayerMotor 会用实际位移回写水平速度；非零 MinMoveDistance 会在高帧率下反复丢弃从零加速的首批位移。");
    }

    [Test]
    public void PlayerPrefab_UsesIndependentWalkAndRunClipTransitions()
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        Component controller = prefab.GetComponent("PlayerAnimationController");
        UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(controller);
        UnityEditor.SerializedProperty walk = serialized.FindProperty("walkLoopTransition");
        UnityEditor.SerializedProperty run = serialized.FindProperty("runLoopTransition");
        Assert.That(walk, Is.Not.Null);
        Assert.That(run, Is.Not.Null);
        Assert.That(serialized.FindProperty("groundLocomotionTransition"), Is.Null);
        Assert.That(walk.FindPropertyRelative("_Clip").objectReferenceValue, Is.Not.Null);
        Assert.That(run.FindPropertyRelative("_Clip").objectReferenceValue, Is.Not.Null);
        Assert.That(walk.FindPropertyRelative("_Clip").objectReferenceValue, Is.Not.SameAs(run.FindPropertyRelative("_Clip").objectReferenceValue));
        Assert.That(walk.FindPropertyRelative("_FadeDuration").floatValue, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(run.FindPropertyRelative("_FadeDuration").floatValue, Is.EqualTo(0.4f).Within(0.0001f));
    }

    [Test]
    public void TravelAlongDesiredDirection_ZeroInputKeepsCapturedDirection()
    {
        PlayerMotionDefinition definition = CreateTurnDefinition(out PlayerMotionProfile profile);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.back);
        PlayerMotionFrame frame = runtime.Advance(0.1f, PlayerGameplayIntent.Create(Vector3.zero, Vector3.forward));
        Assert.That(frame.IsValid, Is.True);
        Assert.That(frame.AuthoredPlanarDisplacement.z, Is.LessThan(0f));
        Assert.That(runtime.Snapshot.JustCancelled, Is.False);
        Assert.That(runtime.Snapshot.IsActive, Is.True);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void TravelAlongDesiredDirection_LargeIntentChangeSteersWithoutCancelling()
    {
        PlayerMotionDefinition definition = CreateTurnDefinition(out PlayerMotionProfile profile);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.back);
        PlayerMotionFrame frame = runtime.Advance(0.1f, PlayerGameplayIntent.Create(Vector3.right, Vector3.forward));
        Assert.That(frame.AuthoredPlanarDisplacement.x, Is.GreaterThan(0f));
        Assert.That(runtime.Snapshot.JustCancelled, Is.False);
        Assert.That(runtime.Snapshot.IsActive, Is.True);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ProfileYaw_DoesNotCorrectWhenProfileWillReachDesiredFacing()
    {
        PlayerMotionDefinition definition = CreateTurnDefinition(out PlayerMotionProfile profile);
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.back, Vector3.forward);
        PlayerMotorResult result = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.zero, 0f, true, false, 0f, CollisionFlags.None);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.back);
        PlayerMotionFrame frame = runtime.Advance(0.25f, intent);
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, frame, result, config, 0.25f, Vector3.forward);
        Assert.That(command.RotationMode, Is.EqualTo(PlayerMotorRotationMode.YawDelta));
        Assert.That(frame.AuthoredYawDelta, Is.EqualTo(-45f).Within(0.0001f));
        Assert.That(frame.RemainingAuthoredYaw, Is.EqualTo(-135f).Within(0.0001f));
        Assert.That(command.YawDelta, Is.EqualTo(frame.AuthoredYawDelta).Within(0.0001f));
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ProfileYaw_DistributesFinalFacingCorrectionAcrossRemainingProgress()
    {
        PlayerMotionDefinition definition = CreateTurnDefinition(out PlayerMotionProfile profile);
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerMotorResult result = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.zero, 0f, true, false, 0f, CollisionFlags.None);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.back);
        PlayerGameplayIntent initialIntent = PlayerGameplayIntent.Create(Vector3.back, Vector3.forward);
        PlayerMotionFrame initialFrame = runtime.Advance(0.5f, initialIntent);
        PlayerMotorCommand initialCommand = PlayerMotionComposer.Compose(initialIntent, initialFrame, result, config, 0.5f, Vector3.forward);
        Vector3 currentFacing = Quaternion.AngleAxis(initialCommand.YawDelta, Vector3.up) * Vector3.forward;
        Vector3 desiredFacing = Quaternion.AngleAxis(-150f, Vector3.up) * Vector3.forward;
        PlayerGameplayIntent changedIntent = PlayerGameplayIntent.Create(desiredFacing, currentFacing);
        PlayerMotionFrame changedFrame = runtime.Advance(0.25f, changedIntent);
        PlayerMotorCommand changedCommand = PlayerMotionComposer.Compose(changedIntent, changedFrame, result, config, 0.25f, currentFacing);
        Assert.That(changedFrame.AuthoredYawDelta, Is.EqualTo(-45f).Within(0.0001f));
        Assert.That(changedFrame.RemainingAuthoredYaw, Is.EqualTo(-45f).Within(0.0001f));
        Assert.That(changedCommand.YawDelta, Is.EqualTo(-30f).Within(0.0001f));
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ProfileYaw_CorrectsOnlyOffsetBeyondProfileTarget()
    {
        PlayerMotionDefinition definition = CreateTurnDefinition(out PlayerMotionProfile profile);
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerMotorResult result = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.zero, 0f, true, false, 0f, CollisionFlags.None);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.back);
        PlayerGameplayIntent initialIntent = PlayerGameplayIntent.Create(Vector3.back, Vector3.forward);
        PlayerMotionFrame initialFrame = runtime.Advance(0.5f, initialIntent);
        PlayerMotorCommand initialCommand = PlayerMotionComposer.Compose(initialIntent, initialFrame, result, config, 0.5f, Vector3.forward);
        Vector3 currentFacing = Quaternion.AngleAxis(initialCommand.YawDelta, Vector3.up) * Vector3.forward;
        Vector3 desiredFacing = Quaternion.AngleAxis(-200f, Vector3.up) * Vector3.forward;
        PlayerGameplayIntent changedIntent = PlayerGameplayIntent.Create(desiredFacing, currentFacing);
        PlayerMotionFrame changedFrame = runtime.Advance(0.25f, changedIntent);
        PlayerMotorCommand changedCommand = PlayerMotionComposer.Compose(changedIntent, changedFrame, result, config, 0.25f, currentFacing);
        Assert.That(changedCommand.YawDelta, Is.EqualTo(-55f).Within(0.0001f));
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ProfileYaw_CompletesFinalFacingCorrectionOnLastFrame()
    {
        PlayerMotionDefinition definition = CreateTurnDefinition(out PlayerMotionProfile profile);
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerMotorResult result = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.zero, 0f, true, false, 0f, CollisionFlags.None);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.back);
        Vector3 desiredFacing = Quaternion.AngleAxis(-150f, Vector3.up) * Vector3.forward;
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(desiredFacing, Vector3.forward);
        PlayerMotionFrame frame = runtime.Advance(1f, intent);
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, frame, result, config, 1f, Vector3.forward);
        Assert.That(frame.CurrentProgress, Is.EqualTo(1f));
        Assert.That(command.YawDelta, Is.EqualTo(-150f).Within(0.0001f));
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_ResolvesMostRecentPlantForNonLoopMotion()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.2f), new PlantMarkerValue(PlayerFoot.Right, 0.6f), new PlantMarkerValue(PlayerFoot.Left, 0.85f));
        Assert.That(profile.HasPlantMarkers, Is.True);
        Assert.That(profile.ResolveSupportFoot(0.1f, PlayerFoot.Unknown), Is.EqualTo(PlayerFoot.Unknown));
        Assert.That(profile.ResolveSupportFoot(0.2f, PlayerFoot.Unknown), Is.EqualTo(PlayerFoot.Left));
        Assert.That(profile.ResolveSupportFoot(0.59f, PlayerFoot.Right), Is.EqualTo(PlayerFoot.Left));
        Assert.That(profile.ResolveSupportFoot(0.6f, PlayerFoot.Left), Is.EqualTo(PlayerFoot.Right));
        Assert.That(profile.ResolveSupportFoot(1f, PlayerFoot.Right), Is.EqualTo(PlayerFoot.Left));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_ResolvesLastPlantBeforeFirstMarkerAcrossLoopBoundary()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.25f), new PlantMarkerValue(PlayerFoot.Right, 0.75f));
        Assert.That(profile.ResolveLoopSupportFoot(0f, PlayerFoot.Unknown), Is.EqualTo(PlayerFoot.Right));
        Assert.That(profile.ResolveLoopSupportFoot(0.24f, PlayerFoot.Left), Is.EqualTo(PlayerFoot.Right));
        Assert.That(profile.ResolveLoopSupportFoot(0.25f, PlayerFoot.Right), Is.EqualTo(PlayerFoot.Left));
        Assert.That(profile.ResolveLoopSupportFoot(0.8f, PlayerFoot.Left), Is.EqualTo(PlayerFoot.Right));
        Assert.That(profile.ResolveLoopSupportFoot(1f, PlayerFoot.Unknown), Is.EqualTo(PlayerFoot.Right));
        Assert.That(profile.ResolveLoopSupportFoot(1.25f, PlayerFoot.Right), Is.EqualTo(PlayerFoot.Left));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_EmptyPlantMarkersPreserveFallback()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        Assert.That(profile.HasPlantMarkers, Is.False);
        Assert.That(profile.PlantMarkers, Is.Empty);
        Assert.That(profile.ResolveSupportFoot(0.5f, PlayerFoot.Unknown), Is.EqualTo(PlayerFoot.Unknown));
        Assert.That(profile.ResolveLoopSupportFoot(0.5f, PlayerFoot.Left), Is.EqualTo(PlayerFoot.Left));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Runtime_DefaultBeginUsesDefaultProfileAndPreservesUnknownFoot()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.forward);
        Assert.That(runtime.Snapshot.ActiveProfile, Is.SameAs(profile));
        Assert.That(runtime.Snapshot.SupportFoot, Is.EqualTo(PlayerFoot.Unknown));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_ValidationRejectsInvalidPlantMarkers()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Unknown, 0.25f), new PlantMarkerValue(PlayerFoot.Left, 0.1f), new PlantMarkerValue(PlayerFoot.Left, 0.2f));
        List<string> errors = new List<string>();
        Assert.That(profile.Validate(errors), Is.False);
        Assert.That(errors.Exists(error => error.Contains("Plant Marker 的脚")), Is.True);
        Assert.That(errors.Exists(error => error.Contains("Plant Marker 必须按时间排序")), Is.True);
        Assert.That(errors.Exists(error => error.Contains("同一脚的 Plant Marker")), Is.True);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    private static PlayerMotionDefinition CreateDefinition(out PlayerMotionProfile profile, float handoffStart = 1f, float handoffEnd = 1f)
    {
        profile = ScriptableObject.CreateInstance<PlayerMotionProfile>();
        profile.SetBakedData(1f, 2, new[] { Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 2f) }, new[] { 0f, 1f, 2f }, new[] { 0f, 0f, 0f }, string.Empty, 0, string.Empty, string.Empty);
        PlayerMotionDefinition definition = ScriptableObject.CreateInstance<PlayerMotionDefinition>();
        definition.Configure(profile, PlayerMotionTranslationPolicy.TravelAlongCapturedDirection, PlayerMotionRotationPolicy.FaceDirection, PlayerMotionBasisPolicy.DesiredDirection, 0f, 1f, handoffStart, handoffEnd);
        return definition;
    }

    private static void SetPlantMarkers(PlayerMotionProfile profile, params PlantMarkerValue[] values)
    {
        UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(profile);
        serialized.Update();
        UnityEditor.SerializedProperty markerArray = serialized.FindProperty("plantMarkers");
        markerArray.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            UnityEditor.SerializedProperty marker = markerArray.GetArrayElementAtIndex(index);
            marker.FindPropertyRelative("foot").enumValueIndex = (int)values[index].Foot;
            marker.FindPropertyRelative("normalizedTime").floatValue = values[index].NormalizedTime;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private struct PlantMarkerValue
    {
        public PlantMarkerValue(PlayerFoot foot, float normalizedTime)
        {
            Foot = foot;
            NormalizedTime = normalizedTime;
        }

        public PlayerFoot Foot;
        public float NormalizedTime;
    }

    private static PlayerMotionDefinition CreateTurnDefinition(out PlayerMotionProfile profile)
    {
        profile = ScriptableObject.CreateInstance<PlayerMotionProfile>();
        profile.SetBakedData(1f, 2, new[] { Vector2.zero, new Vector2(0.5f, 0f), new Vector2(1f, 0f) }, new[] { 0f, 0.5f, 1f }, new[] { 0f, -90f, -180f }, string.Empty, 0, string.Empty, string.Empty);
        PlayerMotionDefinition definition = ScriptableObject.CreateInstance<PlayerMotionDefinition>();
        definition.Configure(profile, PlayerMotionTranslationPolicy.TravelAlongDesiredDirection, PlayerMotionRotationPolicy.ProfileYaw, PlayerMotionBasisPolicy.EntryFacing, 0f, 1f, 0.8f, 1f);
        return definition;
    }

}
