using System.Collections.Generic;
using System.Reflection;
using Animancer;
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

    [TestCase(0.25f, PlayerFoot.Left)]
    [TestCase(0.499f, PlayerFoot.Left)]
    [TestCase(0.5f, PlayerFoot.Right)]
    [TestCase(0.75f, PlayerFoot.Right)]
    public void MotionDefinition_ResolvesEntryFootByPhaseThreshold(float stepProgress, PlayerFoot expectedFoot)
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        ConfigurePhaseFootSelection(definition, true, 0.5f);
        PlayerLocomotionPhaseSnapshot phase = new PlayerLocomotionPhaseSnapshot(true, true, profile, 0.25f, 1f, PlayerFoot.Left, PlayerFoot.Right, stepProgress, 0.1f);
        Assert.That(definition.ResolveEntryFoot(phase), Is.EqualTo(expectedFoot));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void MotionDefinition_PhaseSelectionFallsBackToLastFootWhenUnavailableOrDisabled()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        ConfigurePhaseFootSelection(definition, true, 0.5f);
        PlayerLocomotionPhaseSnapshot noPhase = new PlayerLocomotionPhaseSnapshot(true, false, profile, 0.75f, 8f, PlayerFoot.Left, PlayerFoot.Right, 0.9f, 0.001f);
        Assert.That(definition.ResolveEntryFoot(noPhase), Is.EqualTo(PlayerFoot.Left));
        ConfigurePhaseFootSelection(definition, false, 0.5f);
        PlayerLocomotionPhaseSnapshot phase = new PlayerLocomotionPhaseSnapshot(true, true, profile, 0.75f, 0.25f, PlayerFoot.Left, PlayerFoot.Right, 0.9f, 99f);
        Assert.That(definition.ResolveEntryFoot(phase), Is.EqualTo(PlayerFoot.Left));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void MotionDefinition_PhaseSelectionIgnoresEffectiveSpeedAndTimeToNextPlant()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        ConfigurePhaseFootSelection(definition, true, 0.5f);
        PlayerLocomotionPhaseSnapshot slowPhase = new PlayerLocomotionPhaseSnapshot(true, true, profile, 0.75f, 0.25f, PlayerFoot.Left, PlayerFoot.Right, 0.75f, 100f);
        PlayerLocomotionPhaseSnapshot fastPhase = new PlayerLocomotionPhaseSnapshot(true, true, profile, 0.75f, 10f, PlayerFoot.Left, PlayerFoot.Right, 0.75f, 0.001f);
        Assert.That(definition.ResolveEntryFoot(slowPhase), Is.EqualTo(PlayerFoot.Right));
        Assert.That(definition.ResolveEntryFoot(fastPhase), Is.EqualTo(PlayerFoot.Right));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void MotionDefinition_ValidatesPhaseSelectionContract()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        PlayerMotionProfile leftProfile = CreateTestProfile(2f);
        PlayerMotionProfile rightProfile = CreateTestProfile(3f);
        SetPlantMarkers(leftProfile, new PlantMarkerValue(PlayerFoot.Right, 0.2f));
        SetPlantMarkers(rightProfile, new PlantMarkerValue(PlayerFoot.Left, 0.2f));
        definition.ConfigureFootProfiles(leftProfile, rightProfile, true);
        ConfigurePhaseFootSelection(definition, true, 0.5f);
        List<string> errors = new List<string>();
        Assert.That(definition.Validate(errors), Is.True, string.Join("\n", errors));

        definition.ConfigureFootProfiles(leftProfile, rightProfile, false);
        errors.Clear();
        Assert.That(definition.Validate(errors), Is.False);
        Assert.That(errors.Exists(error => error.Contains("RequiresFootProfiles")), Is.True);
        definition.ConfigureFootProfiles(leftProfile, rightProfile, true);

        SetPlantMarkers(leftProfile, new PlantMarkerValue(PlayerFoot.Left, 0.2f));
        errors.Clear();
        Assert.That(definition.Validate(errors), Is.False);
        Assert.That(errors.Exists(error => error.Contains("Left Foot Profile 的首个真实 Plant")), Is.True);

        ConfigurePhaseFootSelection(definition, true, -0.1f);
        errors.Clear();
        Assert.That(definition.Validate(errors), Is.False);
        Assert.That(errors.Exists(error => error.Contains("NextPlantFootThreshold")), Is.True);

        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(leftProfile);
        Object.DestroyImmediate(rightProfile);
    }

    [TestCase("Assets/Settings/Player/Motion/Definitions/WalkToIdleDefinition.asset", PlayerFoot.Right, PlayerFoot.Left)]
    [TestCase("Assets/Settings/Player/Motion/Definitions/RunToIdleDefinition.asset", PlayerFoot.Right, PlayerFoot.Left)]
    [TestCase("Assets/Settings/Player/Motion/Definitions/FastRunToIdleDefinition.asset", PlayerFoot.Right, PlayerFoot.Left)]
    public void StopDefinitions_EnablePhaseFootSelectionWithOppositeEntryPlant(string assetPath, PlayerFoot leftExpectedPlant, PlayerFoot rightExpectedPlant)
    {
        PlayerMotionDefinition definition = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerMotionDefinition>(assetPath);
        Assert.That(definition, Is.Not.Null, assetPath);
        Assert.That(definition.UsePhaseFootSelection, Is.True, assetPath);
        Assert.That(definition.NextPlantFootThreshold, Is.EqualTo(0.5f).Within(0.0001f), assetPath);
        Assert.That(definition.RequiresFootProfiles, Is.True, assetPath);
        Assert.That(definition.LeftFootProfile, Is.Not.Null, assetPath);
        Assert.That(definition.RightFootProfile, Is.Not.Null, assetPath);
        Assert.That(definition.LeftFootProfile.PlantMarkers[0].Foot, Is.EqualTo(leftExpectedPlant), assetPath);
        Assert.That(definition.RightFootProfile.PlantMarkers[0].Foot, Is.EqualTo(rightExpectedPlant), assetPath);
    }

    [Test]
    public void Runtime_UsesResolvedProfileAndStartsAtZeroProgress()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        PlayerMotionProfile leftProfile = CreateTestProfile(2f);
        PlayerMotionProfile rightProfile = CreateTestProfile(3f);
        definition.ConfigureFootProfiles(leftProfile, rightProfile, true);
        ConfigurePhaseFootSelection(definition, true, 0.5f);
        PlayerLocomotionPhaseSnapshot phase = new PlayerLocomotionPhaseSnapshot(true, true, profile, 0.75f, 4f, PlayerFoot.Left, PlayerFoot.Right, 0.75f, 0.001f);
        PlayerFoot entryFoot = definition.ResolveEntryFoot(phase);
        PlayerMotionProfile selectedProfile = definition.ResolveProfile(entryFoot);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, selectedProfile, entryFoot, Vector3.forward, Vector3.forward);
        Assert.That(runtime.Snapshot.ActiveProfile, Is.SameAs(rightProfile));
        Assert.That(runtime.Snapshot.EntryLastPlantFoot, Is.EqualTo(PlayerFoot.Right));
        Assert.That(runtime.Snapshot.Progress, Is.EqualTo(0f));
        Assert.That(runtime.Advance(0f, default).CurrentProgress, Is.EqualTo(0f));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(leftProfile);
        Object.DestroyImmediate(rightProfile);
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
    public void PlayerAnimationController_HasNoSerializedClipTransitions()
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        Component controller = prefab.GetComponent("PlayerAnimationController");
        FieldInfo[] fields = controller.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (FieldInfo field in fields) Assert.That(field.FieldType.Name, Is.Not.EqualTo("ClipTransition"), field.Name);
        UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(controller);
        Assert.That(serialized.FindProperty("animancer"), Is.Not.Null);
        Assert.That(serialized.FindProperty("animationSet").objectReferenceValue, Is.Not.Null);
        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(serialized.FindProperty("animationSet").objectReferenceValue), Is.EqualTo("Assets/Settings/Player/Motion/DefaultPlayerAnimationSet.asset"));
    }

    [Test]
    public void DefaultAnimationSet_ResolvesAllStableLoopsAndCues()
    {
        ScriptableObject animationSet = LoadDefaultAnimationSet();
        string[] loopModes = { "Idle", "Walk", "Run", "FastRun", "Air" };
        for (int i = 0; i < loopModes.Length; i++)
        {
            Assert.That(ResolveLoop(animationSet, loopModes[i], "Unknown", out object selection), Is.True, loopModes[i]);
            Assert.That(GetPropertyValue(selection, "IsValid"), Is.True, loopModes[i]);
            if (loopModes[i] == "Idle" || loopModes[i] == "Air") Assert.That(GetPropertyValue(selection, "Profile"), Is.Null, loopModes[i]);
        }
        Assert.That(ResolveLoop(animationSet, "HardLanding", "Unknown", out object hardLandingSelection), Is.True);
        Assert.That(GetPropertyValue(hardLandingSelection, "Profile"), Is.Null);
        Assert.That(ResolveLoop(animationSet, "Walk", "Left", out object walkLeft), Is.True);
        Assert.That(ResolveLoop(animationSet, "Walk", "Right", out object walkRight), Is.True);
        Assert.That(ResolveLoop(animationSet, "Run", "Left", out object runLeft), Is.True);
        Assert.That(GetClip(GetPropertyValue(walkLeft, "Transition")), Is.Not.SameAs(GetClip(GetPropertyValue(runLeft, "Transition"))));
        Assert.That(GetPropertyValue(walkLeft, "Profile"), Is.Not.Null);
        Assert.That(GetPropertyValue(walkRight, "Profile"), Is.Not.Null);
        Assert.That(GetPropertyValue(runLeft, "Profile"), Is.Not.Null);
        Assert.That(ResolveCue(animationSet, "JumpStart", out object jumpStart), Is.True);
        Assert.That(ResolveCue(animationSet, "Landing", out object landing), Is.True);
        Assert.That(ResolveCue(animationSet, "HardLanding", out object hardLanding), Is.True);
        Assert.That(GetClip(jumpStart), Is.Not.Null);
        Assert.That(GetClip(landing), Is.Not.Null);
        Assert.That(GetClip(hardLanding), Is.Not.Null);
    }

    [Test]
    public void DefaultAnimationSet_ContainsExactlyTheSixteenCatalogBindings()
    {
        ScriptableObject animationSet = LoadDefaultAnimationSet();
        HashSet<UnityEngine.Object> definitions = new HashSet<UnityEngine.Object>();
        int count = 0;
        foreach (object binding in (System.Collections.IEnumerable)GetPropertyValue(animationSet, "MotionBindings"))
        {
            Assert.That(binding, Is.Not.Null);
            UnityEngine.Object definition = (UnityEngine.Object)GetPropertyValue(binding, "Definition");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definitions.Add(definition), Is.True, definition.name);
            count++;
        }
        Assert.That(count, Is.EqualTo(16));
        Assert.That(definitions.Count, Is.EqualTo(16));
        UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(animationSet);
        Assert.That(serialized.FindProperty("motionBindings"), Is.Null);
        Assert.That(serialized.FindProperty("walk").FindPropertyRelative("motionBindings").arraySize, Is.EqualTo(6));
        Assert.That(serialized.FindProperty("run").FindPropertyRelative("motionBindings").arraySize, Is.EqualTo(6));
        Assert.That(serialized.FindProperty("sprint").FindPropertyRelative("motionBindings").arraySize, Is.EqualTo(3));
        Assert.That(serialized.FindProperty("other").FindPropertyRelative("motionBindings").arraySize, Is.EqualTo(1));
    }

    [Test]
    public void DefaultAnimationSet_ValidatesCatalogAndBakedSources()
    {
        ScriptableObject animationSet = LoadDefaultAnimationSet();
        List<string> errors = new List<string>();
        Assert.That(Validate(animationSet, errors), Is.True, string.Join("\n", errors));
    }

    [Test]
    public void AnimationSet_ValidationReportsMissingAndDuplicateBindings()
    {
        ScriptableObject animationSet = Object.Instantiate(LoadDefaultAnimationSet());
        UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(animationSet);
        UnityEditor.SerializedProperty jumpClip = serialized.FindProperty("jump").FindPropertyRelative("jumpStart").FindPropertyRelative("_Clip");
        jumpClip.objectReferenceValue = null;
        UnityEditor.SerializedProperty walkBindings = serialized.FindProperty("walk").FindPropertyRelative("motionBindings");
        UnityEditor.SerializedProperty runBindings = serialized.FindProperty("run").FindPropertyRelative("motionBindings");
        int originalRunSize = runBindings.arraySize;
        runBindings.arraySize = originalRunSize + 1;
        runBindings.GetArrayElementAtIndex(originalRunSize).FindPropertyRelative("definition").objectReferenceValue = walkBindings.GetArrayElementAtIndex(0).FindPropertyRelative("definition").objectReferenceValue;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        List<string> errors = new List<string>();
        Assert.That(Validate(animationSet, errors), Is.False);
        Assert.That(errors.Exists(error => error.Contains("Jump.JumpStart")), Is.True, string.Join("\n", errors));
        Assert.That(errors.Exists(error => error.Contains("分类之间重复")), Is.True, string.Join("\n", errors));
        Object.DestroyImmediate(animationSet);
    }

    [Test]
    public void PlayingPresentationEdge_DoesNotModifySharedTransitionEvents()
    {
        ScriptableObject animationSet = LoadDefaultAnimationSet();
        Assert.That(ResolveCue(animationSet, "JumpStart", out object jumpStart), Is.True);
        Assert.That(GetEventCallback(jumpStart), Is.Null);
        GameObject instance = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        try
        {
            Component controller = instance.GetComponent("PlayerAnimationController");
            MethodInfo playEdge = controller.GetType().GetMethod("PlayPresentationEdge", BindingFlags.Instance | BindingFlags.NonPublic);
            System.Type airStateType = controller.GetType().Assembly.GetType("PlayerAirState");
            playEdge.Invoke(controller, new object[] { jumpStart, airStateType, (ulong)1 });
            Assert.That(GetEventCallback(jumpStart), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
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
        Assert.That(profile.ResolveLastPlantFoot(0.1f, PlayerFoot.Unknown), Is.EqualTo(PlayerFoot.Unknown));
        Assert.That(profile.ResolveLastPlantFoot(0.2f, PlayerFoot.Unknown), Is.EqualTo(PlayerFoot.Left));
        Assert.That(profile.ResolveLastPlantFoot(0.59f, PlayerFoot.Right), Is.EqualTo(PlayerFoot.Left));
        Assert.That(profile.ResolveLastPlantFoot(0.6f, PlayerFoot.Left), Is.EqualTo(PlayerFoot.Right));
        Assert.That(profile.ResolveLastPlantFoot(1f, PlayerFoot.Right), Is.EqualTo(PlayerFoot.Left));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_EvaluatesLoopPhaseAtStepMidpoint()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.25f), new PlantMarkerValue(PlayerFoot.Right, 0.75f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 2f, out PlayerLocomotionPhaseSnapshot snapshot), Is.True);
        Assert.That(snapshot.HasLoop, Is.True);
        Assert.That(snapshot.HasPhase, Is.True);
        Assert.That(snapshot.Profile, Is.SameAs(profile));
        Assert.That(snapshot.NormalizedTime, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(snapshot.EffectiveSpeed, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(snapshot.LastPlantFoot, Is.EqualTo(PlayerFoot.Left));
        Assert.That(snapshot.NextPlantFoot, Is.EqualTo(PlayerFoot.Right));
        Assert.That(snapshot.StepProgress, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(snapshot.TimeToNextPlant, Is.EqualTo(0.125f).Within(0.0001f));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_EvaluatesExactMarkerAndLoopSeam()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.25f), new PlantMarkerValue(PlayerFoot.Right, 0.75f));
        Assert.That(profile.TryEvaluateLoopPhase(0.25f, 1f, out PlayerLocomotionPhaseSnapshot markerSnapshot), Is.True);
        Assert.That(markerSnapshot.NormalizedTime, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(markerSnapshot.LastPlantFoot, Is.EqualTo(PlayerFoot.Left));
        Assert.That(markerSnapshot.NextPlantFoot, Is.EqualTo(PlayerFoot.Right));
        Assert.That(markerSnapshot.StepProgress, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(profile.TryEvaluateLoopPhase(1.25f, 1f, out markerSnapshot), Is.True);
        Assert.That(markerSnapshot.LastPlantFoot, Is.EqualTo(PlayerFoot.Left));
        Assert.That(markerSnapshot.StepProgress, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(profile.TryEvaluateLoopPhase(0f, 1f, out PlayerLocomotionPhaseSnapshot seamSnapshot), Is.True);
        Assert.That(seamSnapshot.LastPlantFoot, Is.EqualTo(PlayerFoot.Right));
        Assert.That(seamSnapshot.NextPlantFoot, Is.EqualTo(PlayerFoot.Left));
        Assert.That(seamSnapshot.StepProgress, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(profile.TryEvaluateLoopPhase(1f, 1f, out seamSnapshot), Is.True);
        Assert.That(seamSnapshot.LastPlantFoot, Is.EqualTo(PlayerFoot.Right));
        Assert.That(seamSnapshot.NextPlantFoot, Is.EqualTo(PlayerFoot.Left));
        Assert.That(seamSnapshot.StepProgress, Is.EqualTo(0.5f).Within(0.0001f));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_TimeToNextPlantScalesWithEffectiveSpeed()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.2f), new PlantMarkerValue(PlayerFoot.Right, 0.8f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 1f, out PlayerLocomotionPhaseSnapshot slowSnapshot), Is.True);
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 2f, out PlayerLocomotionPhaseSnapshot fastSnapshot), Is.True);
        Assert.That(fastSnapshot.StepProgress, Is.EqualTo(slowSnapshot.StepProgress).Within(0.0001f));
        Assert.That(fastSnapshot.TimeToNextPlant, Is.EqualTo(slowSnapshot.TimeToNextPlant * 0.5f).Within(0.0001f));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_RejectsInvalidLoopPhaseInputs()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.25f), new PlantMarkerValue(PlayerFoot.Right, 0.75f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 0f, out PlayerLocomotionPhaseSnapshot snapshot), Is.False);
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, -1f, out snapshot), Is.False);
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, float.NaN, out snapshot), Is.False);
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, float.PositiveInfinity, out snapshot), Is.False);
        Assert.That(profile.TryEvaluateLoopPhase(float.NaN, 1f, out snapshot), Is.False);
        Assert.That(profile.TryEvaluateLoopPhase(float.PositiveInfinity, 1f, out snapshot), Is.False);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_RejectsInvalidLoopMarkerConfigurations()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        List<string> errors = new List<string>();
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 1f, out PlayerLocomotionPhaseSnapshot snapshot), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.25f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 1f, out snapshot), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0f), new PlantMarkerValue(PlayerFoot.Right, 0.75f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 1f, out snapshot), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Right, 0.75f), new PlantMarkerValue(PlayerFoot.Left, 0.25f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 1f, out snapshot), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.25f), new PlantMarkerValue(PlayerFoot.Left, 0.75f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 1f, out snapshot), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.2f), new PlantMarkerValue(PlayerFoot.Right, 0.5f), new PlantMarkerValue(PlayerFoot.Left, 0.8f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 1f, out snapshot), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.2f), new PlantMarkerValue(PlayerFoot.Right, 0.25f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 1f, out snapshot), Is.True);
        Assert.That(profile.ValidateLoopPhase(errors), Is.False);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_RejectsNonPositiveLoopDuration()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.25f), new PlantMarkerValue(PlayerFoot.Right, 0.75f));
        FieldInfo durationField = typeof(PlayerMotionProfile).GetField("duration", BindingFlags.Instance | BindingFlags.NonPublic);
        durationField.SetValue(profile, 0f);
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, 1f, out PlayerLocomotionPhaseSnapshot snapshot), Is.False);
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
        Assert.That(runtime.Snapshot.EntryLastPlantFoot, Is.EqualTo(PlayerFoot.Unknown));
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

    private static void ConfigurePhaseFootSelection(PlayerMotionDefinition definition, bool enabled, float threshold)
    {
        UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(definition);
        serialized.Update();
        serialized.FindProperty("usePhaseFootSelection").boolValue = enabled;
        serialized.FindProperty("nextPlantFootThreshold").floatValue = threshold;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static PlayerMotionProfile CreateTestProfile(float travelDistance)
    {
        PlayerMotionProfile profile = ScriptableObject.CreateInstance<PlayerMotionProfile>();
        profile.SetBakedData(1f, 2, new[] { Vector2.zero, new Vector2(0f, travelDistance * 0.5f), new Vector2(0f, travelDistance) }, new[] { 0f, travelDistance * 0.5f, travelDistance }, new[] { 0f, 0f, 0f }, string.Empty, 0, string.Empty, string.Empty);
        return profile;
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

    private static ScriptableObject LoadDefaultAnimationSet()
    {
        return UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/Player/Motion/DefaultPlayerAnimationSet.asset");
    }

    private static bool ResolveLoop(ScriptableObject animationSet, string modeName, string footName, out object selection)
    {
        System.Type setType = animationSet.GetType();
        System.Type modeType = FindLoadedType("PlayerLocomotionMode");
        System.Type footType = FindLoadedType("PlayerFoot");
        MethodInfo method = setType.GetMethod("TryResolveLoop");
        object[] arguments = { System.Enum.Parse(modeType, modeName), System.Enum.Parse(footType, footName), null };
        bool result = (bool)method.Invoke(animationSet, arguments);
        selection = arguments[2];
        return result;
    }

    private static bool ResolveCue(ScriptableObject animationSet, string cueName, out object transition)
    {
        System.Type setType = animationSet.GetType();
        System.Type cueType = FindLoadedType("PlayerAnimationCue");
        MethodInfo method = setType.GetMethod("TryResolveCue");
        object[] arguments = { System.Enum.Parse(cueType, cueName), null };
        bool result = (bool)method.Invoke(animationSet, arguments);
        transition = arguments[1];
        return result;
    }

    private static bool Validate(ScriptableObject animationSet, ICollection<string> errors)
    {
        MethodInfo method = animationSet.GetType().GetMethod("Validate");
        return (bool)method.Invoke(animationSet, new object[] { errors });
    }

    private static object GetPropertyValue(object target, string propertyName)
    {
        return target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public).GetValue(target);
    }

    private static UnityEngine.Object GetClip(object transition)
    {
        return (UnityEngine.Object)GetPropertyValue(transition, "Clip");
    }

    private static object GetEventCallback(object transition)
    {
        return ((ClipTransition)transition).Events.OnEnd;
    }

    private static System.Type FindLoadedType(string typeName)
    {
        foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType(typeName);
            if (type != null) return type;
        }
        return null;
    }

}
