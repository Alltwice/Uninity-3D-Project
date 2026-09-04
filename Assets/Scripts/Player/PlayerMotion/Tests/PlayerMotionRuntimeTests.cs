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

    [Test]
    public void MotionDefinition_EntryHandoffDefaultsClosedAndMapsProgress()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        Assert.That(definition.EntryHandoffEndProgress, Is.Zero);
        Assert.That(definition.HasEntryHandoff, Is.False);
        definition.ConfigureEntryHandoff(0.15f);
        Assert.That(definition.HasEntryHandoff, Is.True);
        Assert.That(definition.CalculateEntryHandoffProgress(0f), Is.Zero);
        Assert.That(definition.CalculateEntryHandoffProgress(0.075f), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(definition.CalculateEntryHandoffProgress(0.15f), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(definition.EvaluateEntryTranslationWeight(0f), Is.Zero);
        Assert.That(definition.EvaluateEntryTranslationWeight(0.15f), Is.EqualTo(1f).Within(0.0001f));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void MotionDefinition_ValidateRejectsOverlappingEntryAndExitHandoffs()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile, 0.7f, 1f);
        definition.ConfigureEntryHandoff(0.8f);
        List<string> errors = new List<string>();
        Assert.That(definition.Validate(errors), Is.False);
        Assert.That(errors.Exists(error => error.Contains("EntryHandoffEndProgress") && error.Contains("ExitHandoffStartProgress")), Is.True);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void MotionDefinition_FormerlySerializedFieldsKeepLegacyAssetNames()
    {
        FieldInfo exitStart = typeof(PlayerMotionDefinition).GetField("exitHandoffStartProgress", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo exitEnd = typeof(PlayerMotionDefinition).GetField("exitHandoffEndProgress", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo exitAuthority = typeof(PlayerMotionDefinition).GetField("exitTranslationAuthority", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(exitStart.GetCustomAttributes(typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute), false), Has.Length.EqualTo(1));
        Assert.That(exitEnd.GetCustomAttributes(typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute), false), Has.Length.EqualTo(1));
        Assert.That(exitAuthority.GetCustomAttributes(typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute), false), Has.Length.EqualTo(1));
    }

    [Test]
    public void Runtime_CapturesEntrySourceAndKeepsVelocityConstant()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        definition.ConfigureEntryHandoff(0.15f);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, new PlayerMotionEntrySource(PlayerLocomotionMode.Run, new Vector3(0f, 7f, 4f)), Vector3.forward, Vector3.forward);
        Assert.That(runtime.Snapshot.HasEntrySource, Is.True);
        Assert.That(runtime.Snapshot.EntrySourceLocomotionMode, Is.EqualTo(PlayerLocomotionMode.Run));
        Assert.That(runtime.Snapshot.EntryHandoffActive, Is.True);
        PlayerMotionFrame middle = runtime.Advance(0.075f, default);
        Assert.That(middle.EntryHandoffActive, Is.True);
        Assert.That(middle.EntryTargetTranslationWeight, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(middle.EntrySourcePlanarVelocity, Is.EqualTo(new Vector3(0f, 0f, 4f)));
        PlayerMotionFrame end = runtime.Advance(0.075f, default);
        Assert.That(end.EntryHandoffActive, Is.False);
        Assert.That(end.EntryTargetTranslationWeight, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(runtime.Snapshot.HasEntrySource, Is.True);
        Assert.That(runtime.Snapshot.EntryHandoffActive, Is.False);
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
        PlayerLocomotionPhaseSnapshot phase = new PlayerLocomotionPhaseSnapshot(true, true, PlayerLocomotionMode.Walk, PlayerFoot.Left, 0.25f, PlayerFoot.Left, PlayerFoot.Right, stepProgress);
        Assert.That(definition.ResolveEntryFoot(phase), Is.EqualTo(expectedFoot));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void MotionDefinition_PhaseSelectionFallsBackToLastFootWhenUnavailableOrDisabled()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        ConfigurePhaseFootSelection(definition, true, 0.5f);
        PlayerLocomotionPhaseSnapshot noPhase = new PlayerLocomotionPhaseSnapshot(true, false, PlayerLocomotionMode.Walk, PlayerFoot.Left, 0.75f, PlayerFoot.Left, PlayerFoot.Right, 0.9f);
        Assert.That(definition.ResolveEntryFoot(noPhase), Is.EqualTo(PlayerFoot.Left));
        ConfigurePhaseFootSelection(definition, false, 0.5f);
        PlayerLocomotionPhaseSnapshot phase = new PlayerLocomotionPhaseSnapshot(true, true, PlayerLocomotionMode.Walk, PlayerFoot.Left, 0.75f, PlayerFoot.Left, PlayerFoot.Right, 0.9f);
        Assert.That(definition.ResolveEntryFoot(phase), Is.EqualTo(PlayerFoot.Left));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void MotionDefinition_PhaseSelectionDependsOnlyOnStepProgress()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        ConfigurePhaseFootSelection(definition, true, 0.5f);
        PlayerLocomotionPhaseSnapshot firstPhase = new PlayerLocomotionPhaseSnapshot(true, true, PlayerLocomotionMode.Walk, PlayerFoot.Left, 0.25f, PlayerFoot.Left, PlayerFoot.Right, 0.75f);
        PlayerLocomotionPhaseSnapshot secondPhase = new PlayerLocomotionPhaseSnapshot(true, true, PlayerLocomotionMode.FastRun, PlayerFoot.Right, 0.9f, PlayerFoot.Left, PlayerFoot.Right, 0.75f);
        Assert.That(definition.ResolveEntryFoot(firstPhase), Is.EqualTo(PlayerFoot.Right));
        Assert.That(definition.ResolveEntryFoot(secondPhase), Is.EqualTo(PlayerFoot.Right));
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

    [TestCase("Assets/Settings/Player/Motion/Definitions/WalkToIdleDefinition.asset")]
    [TestCase("Assets/Settings/Player/Motion/Definitions/RunToIdleDefinition.asset")]
    [TestCase("Assets/Settings/Player/Motion/Definitions/FastRunToIdleDefinition.asset")]
    public void StopDefinitions_UseEntryAndExitHandoffWindows(string assetPath)
    {
        PlayerMotionDefinition definition = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerMotionDefinition>(assetPath);
        Assert.That(definition.EntryHandoffEndProgress, Is.EqualTo(0.15f).Within(0.0001f), assetPath);
        Assert.That(definition.ExitHandoffStartProgress, Is.EqualTo(0.7f).Within(0.0001f), assetPath);
        Assert.That(definition.ExitHandoffEndProgress, Is.EqualTo(1f).Within(0.0001f), assetPath);
        Assert.That(definition.Validate(new List<string>()), Is.True, assetPath);
    }

    [Test]
    public void Runtime_UsesResolvedProfileAndStartsAtZeroProgress()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        PlayerMotionProfile leftProfile = CreateTestProfile(2f);
        PlayerMotionProfile rightProfile = CreateTestProfile(3f);
        definition.ConfigureFootProfiles(leftProfile, rightProfile, true);
        ConfigurePhaseFootSelection(definition, true, 0.5f);
        PlayerLocomotionPhaseSnapshot phase = new PlayerLocomotionPhaseSnapshot(true, true, PlayerLocomotionMode.Walk, PlayerFoot.Right, 0.75f, PlayerFoot.Left, PlayerFoot.Right, 0.75f);
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
        Assert.That(definition.EvaluateExitTranslationAuthority(0.5f), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(definition.EvaluateExitTranslationAuthority(1f), Is.EqualTo(0f).Within(0.0001f));
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
    public void EntryHandoff_ComposerUsesUnifiedThreeWayTranslationWeights()
    {
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile, 0.7f, 1f);
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward);
        intent.LocomotionMode = PlayerLocomotionMode.Run;
        PlayerMotorResult result = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.forward * 2f, 0f, true, false, 0f, CollisionFlags.None);
        float deltaTime = 0.1f;
        float entryTargetWeight = 0.5f;
        float exitSourceWeight = 0.25f;
        PlayerMotionFrame frame = new PlayerMotionFrame(definition, profile, PlayerFoot.Unknown, Vector3.forward * 2f, 0f, 0f, 0f, 0.5f, exitSourceWeight, true, entryTargetWeight, Vector3.forward * 4f);
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, frame, result, config, deltaTime, Vector3.forward);
        Vector3 predictedTargetVelocity = PlayerMotionComposer.CalculateVelocity(result.HorizontalVelocity, intent.DesiredMoveDirection * config.Locomotion.RunSpeed, intent.LocomotionMode, config.Locomotion, deltaTime);
        Vector3 expected = Vector3.forward * 4f * deltaTime * (1f - entryTargetWeight)
            + Vector3.forward * 2f * (entryTargetWeight * exitSourceWeight)
            + predictedTargetVelocity * deltaTime * (entryTargetWeight * (1f - exitSourceWeight));
        Assert.That(command.TranslationMode, Is.EqualTo(PlayerMotorTranslationMode.DisplacementDriven));
        Assert.That(command.PlanarDisplacement.x, Is.EqualTo(expected.x).Within(0.0001f));
        Assert.That(command.PlanarDisplacement.y, Is.EqualTo(expected.y).Within(0.0001f));
        Assert.That(command.PlanarDisplacement.z, Is.EqualTo(expected.z).Within(0.0001f));
        Assert.That((1f - entryTargetWeight) + entryTargetWeight * exitSourceWeight + entryTargetWeight * (1f - exitSourceWeight), Is.EqualTo(1f).Within(0.0001f));
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void EntryHandoff_ComposerUsesSourceAtStartAndAuthoredAtEnd()
    {
        PlayerMovementConfig config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile, 0.7f, 1f);
        definition.ConfigureEntryHandoff(0.15f);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.zero, Vector3.forward);
        intent.LocomotionMode = PlayerLocomotionMode.Idle;
        PlayerMotorResult result = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.zero, 0f, true, false, 0f, CollisionFlags.None);
        runtime.Begin(definition, new PlayerMotionEntrySource(PlayerLocomotionMode.Walk, Vector3.forward * 4f), Vector3.forward, Vector3.forward);
        PlayerMotionFrame start = runtime.Advance(0f, intent);
        PlayerMotorCommand startCommand = PlayerMotionComposer.Compose(intent, start, result, config, 0.1f, Vector3.forward);
        Assert.That(start.EntryTargetTranslationWeight, Is.Zero);
        Assert.That(startCommand.PlanarDisplacement.z, Is.EqualTo(0.4f).Within(0.0001f));
        PlayerMotionFrame end = runtime.Advance(0.15f, intent);
        PlayerMotorCommand endCommand = PlayerMotionComposer.Compose(intent, end, result, config, 0.1f, Vector3.forward);
        Assert.That(end.EntryHandoffActive, Is.False);
        Assert.That(end.EntryTargetTranslationWeight, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(endCommand.PlanarDisplacement.z, Is.EqualTo(profile.EvaluateTravelDistance(0.15f)).Within(0.0001f));
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ZeroLengthHandoff_KeepsAuthoredAuthorityThroughCompletionFrame()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile, 1f, 1f);
        Assert.That(definition.EvaluateExitTranslationAuthority(1f), Is.EqualTo(1f));
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
        Assert.That(controller.GetType().GetProperty("PhaseSnapshot", BindingFlags.Instance | BindingFlags.Public), Is.Null);
    }

    [Test]
    public void PlayerAnimationController_ManuallySamplesStableLoopFromSimulationPhase()
    {
        GameObject instance = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        try
        {
            Component controller = instance.GetComponent("PlayerAnimationController");
            System.Type controllerType = controller.GetType();
            System.Type walkStateType = FindLoadedType("PlayerWalkState");
            MethodInfo initialize = controllerType.GetMethod("InitializeManualEvaluation", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo playStableLoop = controllerType.GetMethod("PlayStableLoop", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo present = controllerType.GetMethod("Present", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo stableLoopState = controllerType.GetField("stableLoopState", BindingFlags.Instance | BindingFlags.NonPublic);
            PlayerLocomotionPhaseSnapshot firstPhase = new PlayerLocomotionPhaseSnapshot(true, true, PlayerLocomotionMode.Walk, PlayerFoot.Left, 0.37f, PlayerFoot.Right, PlayerFoot.Left, 0.5f);
            initialize.Invoke(controller, null);
            playStableLoop.Invoke(controller, new object[] { walkStateType, firstPhase });
            AnimancerState state = (AnimancerState)stableLoopState.GetValue(controller);
            Assert.That(state, Is.Not.Null);
            Assert.That(state.Speed, Is.Zero);
            Assert.That(state.IsPlaying, Is.False);
            Assert.That(state.NormalizedTime, Is.EqualTo(firstPhase.NormalizedTime).Within(0.0001f));
            PlayerLocomotionPhaseSnapshot secondPhase = new PlayerLocomotionPhaseSnapshot(true, true, PlayerLocomotionMode.Walk, PlayerFoot.Left, 0.62f, PlayerFoot.Left, PlayerFoot.Right, 0.25f);
            present.Invoke(controller, new object[] { walkStateType, null, default(PlayerMotionSnapshot), secondPhase, 0f, null });
            Assert.That(state.NormalizedTime, Is.EqualTo(secondPhase.NormalizedTime).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void PlayerAnimationController_TransfersAndClearsEntrySourceLoop()
    {
        GameObject instance = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        try
        {
            Component controller = instance.GetComponent("PlayerAnimationController");
            System.Type controllerType = controller.GetType();
            System.Type walkStateType = FindLoadedType("PlayerWalkState");
            System.Type idleStateType = FindLoadedType("PlayerIdleState");
            MethodInfo initialize = controllerType.GetMethod("InitializeManualEvaluation", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo playStableLoop = controllerType.GetMethod("PlayStableLoop", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo present = controllerType.GetMethod("Present", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo stableLoopField = controllerType.GetField("stableLoopState", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo entrySourceField = controllerType.GetField("entrySourceLoopState", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo boundaryField = controllerType.GetField("boundaryState", BindingFlags.Instance | BindingFlags.NonPublic);
            PlayerLocomotionPhaseSnapshot phase = new PlayerLocomotionPhaseSnapshot(true, true, PlayerLocomotionMode.Walk, PlayerFoot.Left, 0.37f, PlayerFoot.Right, PlayerFoot.Left, 0.5f);
            initialize.Invoke(controller, null);
            playStableLoop.Invoke(controller, new object[] { walkStateType, phase });
            AnimancerState source = (AnimancerState)stableLoopField.GetValue(controller);
            PlayerMotionDefinition stopDefinition = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerMotionDefinition>("Assets/Settings/Player/Motion/Definitions/WalkToIdleDefinition.asset");
            PlayerMotionSnapshot entryMotion = new PlayerMotionSnapshot(stopDefinition, stopDefinition.Profile, PlayerFoot.Right, 900, 0.05f, 0f, false, true, true, 1f / 3f, PlayerLocomotionMode.Walk, true, false, false);
            present.Invoke(controller, new object[] { idleStateType, null, entryMotion, phase, 0f, null });
            AnimancerState entrySource = (AnimancerState)entrySourceField.GetValue(controller);
            AnimancerState boundary = (AnimancerState)boundaryField.GetValue(controller);
            Assert.That(entrySource, Is.SameAs(source));
            Assert.That(stableLoopField.GetValue(controller), Is.Null);
            Assert.That(entrySource.Weight, Is.EqualTo(2f / 3f).Within(0.0001f));
            Assert.That(boundary.Weight, Is.EqualTo(1f / 3f).Within(0.0001f));

            PlayerMotionSnapshot coreMotion = new PlayerMotionSnapshot(stopDefinition, stopDefinition.Profile, PlayerFoot.Right, 900, 0.2f, 0f, false, true, false, 1f, PlayerLocomotionMode.Walk, true, false, false);
            present.Invoke(controller, new object[] { idleStateType, null, coreMotion, phase, 0f, null });
            Assert.That(entrySourceField.GetValue(controller), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void DefaultAnimationSet_ResolvesStableLoopsAndLandingPresentations()
    {
        ScriptableObject animationSet = LoadDefaultAnimationSet();
        string[] loopModes = { "Idle", "Walk", "Run", "FastRun", "Air" };
        for (int i = 0; i < loopModes.Length; i++)
        {
            Assert.That(ResolveLoop(animationSet, loopModes[i], "Unknown", out object selection), Is.True, loopModes[i]);
            Assert.That(GetPropertyValue(selection, "IsValid"), Is.True, loopModes[i]);
        }
        Assert.That(ResolveLoop(animationSet, "HardLanding", "Unknown", out _), Is.True);
        Assert.That(ResolveLoop(animationSet, "Walk", "Left", out object walkLeft), Is.True);
        Assert.That(ResolveLoop(animationSet, "Walk", "Right", out object walkRight), Is.True);
        Assert.That(ResolveLoop(animationSet, "Run", "Left", out object runLeft), Is.True);
        Assert.That(GetClip(GetPropertyValue(walkLeft, "Transition")), Is.Not.SameAs(GetClip(GetPropertyValue(runLeft, "Transition"))));
        Assert.That(walkLeft.GetType().GetProperty("Profile", BindingFlags.Instance | BindingFlags.Public), Is.Null);
        Assert.That(ResolveCue(animationSet, "JumpStart", out object jumpStart), Is.True);
        Assert.That(ResolveCue(animationSet, "LandingLv1", out object landingLv1), Is.True);
        Assert.That(ResolveLandingPresentation(animationSet, "LandWalk", out object landWalk), Is.True);
        Assert.That(ResolveCue(animationSet, "HardLanding", out object hardLanding), Is.True);
        Assert.That(GetClip(jumpStart), Is.Not.Null);
        Assert.That(GetClip(landingLv1), Is.Not.Null);
        Assert.That(GetPropertyValue(landWalk, "Definition"), Is.Not.Null);
        Assert.That(GetClip(GetPropertyValue(landWalk, "Transition")), Is.Not.Null);
        Assert.That(GetClip(hardLanding), Is.Not.Null);
    }

    [Test]
    public void AnimationSet_ExposesLandingPresentationSlots()
    {
        ScriptableObject animationSet = LoadDefaultAnimationSet();
        UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(animationSet);
        UnityEditor.SerializedProperty landing = serialized.FindProperty("landing");
        Assert.That(landing, Is.Not.Null);
        string[] landingFields = { "land1", "land2", "land3", "land4" };
        for (int i = 0; i < landingFields.Length; i++)
        {
            UnityEditor.SerializedProperty field = landing.FindPropertyRelative(landingFields[i]);
            Assert.That(field, Is.Not.Null, landingFields[i]);
            Assert.That(field.FindPropertyRelative("_Clip").objectReferenceValue, Is.Not.Null, landingFields[i]);
        }
        string[] motionFields = { "landWalk", "landRun", "landRoll" };
        for (int i = 0; i < motionFields.Length; i++)
        {
            UnityEditor.SerializedProperty field = landing.FindPropertyRelative(motionFields[i]);
            Assert.That(field, Is.Not.Null, motionFields[i]);
            Assert.That(field.FindPropertyRelative("definition").objectReferenceValue, Is.Not.Null, motionFields[i]);
            Assert.That(field.FindPropertyRelative("defaultTransition").FindPropertyRelative("_Clip").objectReferenceValue, Is.Not.Null, motionFields[i]);
            Assert.That(field.FindPropertyRelative("leftTransition").FindPropertyRelative("_Clip").objectReferenceValue, Is.Null, motionFields[i]);
            Assert.That(field.FindPropertyRelative("rightTransition").FindPropertyRelative("_Clip").objectReferenceValue, Is.Null, motionFields[i]);
        }
    }

    [Test]
    public void DefaultAnimationSet_ContainsExactlyTheNineteenCatalogBindings()
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
        Assert.That(count, Is.EqualTo(19));
        Assert.That(definitions.Count, Is.EqualTo(19));
        UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(animationSet);
        Assert.That(serialized.FindProperty("motionBindings"), Is.Null);
        Assert.That(serialized.FindProperty("walk").FindPropertyRelative("motionBindings").arraySize, Is.EqualTo(6));
        Assert.That(serialized.FindProperty("run").FindPropertyRelative("motionBindings").arraySize, Is.EqualTo(6));
        Assert.That(serialized.FindProperty("sprint").FindPropertyRelative("motionBindings").arraySize, Is.EqualTo(3));
        Assert.That(serialized.FindProperty("landing"), Is.Not.Null);
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
            playEdge.Invoke(controller, new object[] { jumpStart, airStateType, default(PlayerLocomotionPhaseSnapshot), (ulong)1 });
            Assert.That(GetEventCallback(jumpStart), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [TestCase(PlayerLandingSeverity.Lv4, PlayerLocomotionMode.Walk)]
    [TestCase(PlayerLandingSeverity.Lv4, PlayerLocomotionMode.Run)]
    [TestCase(PlayerLandingSeverity.Lv4, PlayerLocomotionMode.FastRun)]
    public void LandingPresentationResolver_Lv4AlwaysUsesHardLanding(PlayerLandingSeverity severity, PlayerLocomotionMode targetGroundMode)
    {
        PlayerLandingSnapshot snapshot = new PlayerLandingSnapshot(1, severity, 0f, 0f, PlayerLocomotionMode.Run, true, targetGroundMode);
        Assert.That(ResolveLandingCue(snapshot, out string cueName), Is.True);
        Assert.That(cueName, Is.EqualTo("HardLand"));
    }

    [TestCase(PlayerLandingSeverity.Lv1, PlayerLocomotionMode.Walk)]
    [TestCase(PlayerLandingSeverity.Lv2, PlayerLocomotionMode.Run)]
    [TestCase(PlayerLandingSeverity.Lv3, PlayerLocomotionMode.FastRun)]
    public void LandingPresentationResolver_MoveIntentUsesTargetGroundMode(PlayerLandingSeverity severity, PlayerLocomotionMode targetGroundMode)
    {
        PlayerLandingSnapshot snapshot = new PlayerLandingSnapshot(1, severity, 0f, 0f, PlayerLocomotionMode.Idle, true, targetGroundMode);
        Assert.That(ResolveLandingCue(snapshot, out string cueName), Is.True);
        Assert.That(cueName, Is.EqualTo(targetGroundMode == PlayerLocomotionMode.Walk ? "LandWalk" : targetGroundMode == PlayerLocomotionMode.Run ? "LandRun" : "LandRoll"));
    }

    [TestCase(PlayerLandingSeverity.Lv1, "Land1")]
    [TestCase(PlayerLandingSeverity.Lv2, "Land2")]
    [TestCase(PlayerLandingSeverity.Lv3, "Land3")]
    public void LandingPresentationResolver_NoMoveIntentUsesSeverity(PlayerLandingSeverity severity, string expectedCue)
    {
        PlayerLandingSnapshot snapshot = new PlayerLandingSnapshot(1, severity, 0f, 0f, PlayerLocomotionMode.FastRun, false, PlayerLocomotionMode.Run);
        Assert.That(ResolveLandingCue(snapshot, out string cueName), Is.True);
        Assert.That(cueName, Is.EqualTo(expectedCue));
    }

    [Test]
    public void LandingPresentationResolver_InvalidTargetGroundModeFallsBackToSeverity()
    {
        PlayerLandingSnapshot snapshot = new PlayerLandingSnapshot(1, PlayerLandingSeverity.Lv2, 0f, 0f, PlayerLocomotionMode.FastRun, true, PlayerLocomotionMode.Idle);
        Assert.That(ResolveLandingCue(snapshot, out string cueName), Is.True);
        Assert.That(cueName, Is.EqualTo("Land2"));
    }

    [Test]
    public void LandingPresentationResolver_NonLandingSnapshotReturnsFalse()
    {
        Assert.That(ResolveLandingCue(default, out string cueName), Is.False);
        Assert.That(cueName, Is.Null);
    }

    [Test]
    public void SimulationDriver_BufferedJumpSuppressesLandingCue()
    {
        PlayerLandingSnapshot snapshot = new PlayerLandingSnapshot(1, PlayerLandingSeverity.Lv2, 0f, 0f, PlayerLocomotionMode.Walk, true, PlayerLocomotionMode.Walk);
        object transition = CreateStateTransition("PlayerAirState", "PlayerAirState", "Jumped");
        Assert.That(ResolveDriverLandingCue(snapshot, transition), Is.Null);
    }

    [Test]
    public void SimulationDriver_HardLandingStateAlwaysPassesHardLandingCue()
    {
        PlayerLandingSnapshot snapshot = new PlayerLandingSnapshot(1, PlayerLandingSeverity.Lv4, 0f, 0f, PlayerLocomotionMode.Run, true, PlayerLocomotionMode.Run);
        object transition = CreateStateTransition("PlayerAirState", "PlayerHardLandingState", "HardLanded");
        Assert.That(ResolveDriverLandingCue(snapshot, transition), Is.EqualTo("HardLand"));
    }

    [TestCase("PlayerIdleState", "Land1")]
    [TestCase("PlayerWalkState", "LandWalk")]
    [TestCase("PlayerRunState", "LandRun")]
    [TestCase("PlayerFastRunState", "LandRoll")]
    public void SimulationDriver_GroundStateUsesResolvedLandingCue(string currentStateName, string expectedCue)
    {
        PlayerLocomotionMode targetGroundMode = currentStateName == "PlayerWalkState" ? PlayerLocomotionMode.Walk : currentStateName == "PlayerRunState" ? PlayerLocomotionMode.Run : currentStateName == "PlayerFastRunState" ? PlayerLocomotionMode.FastRun : PlayerLocomotionMode.Idle;
        bool hasMoveIntent = targetGroundMode != PlayerLocomotionMode.Idle;
        PlayerLandingSnapshot snapshot = new PlayerLandingSnapshot(1, PlayerLandingSeverity.Lv1, 0f, 0f, PlayerLocomotionMode.Air, hasMoveIntent, targetGroundMode);
        object transition = CreateStateTransition("PlayerAirState", currentStateName, "Landed");
        Assert.That(ResolveDriverLandingCue(snapshot, transition), Is.EqualTo(expectedCue));
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
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, out PlayerFoot lastFoot, out PlayerFoot nextFoot, out float stepProgress), Is.True);
        Assert.That(lastFoot, Is.EqualTo(PlayerFoot.Left));
        Assert.That(nextFoot, Is.EqualTo(PlayerFoot.Right));
        Assert.That(stepProgress, Is.EqualTo(0.5f).Within(0.0001f));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_EvaluatesExactMarkerAndLoopSeam()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.25f), new PlantMarkerValue(PlayerFoot.Right, 0.75f));
        Assert.That(profile.TryEvaluateLoopPhase(0.25f, out PlayerFoot markerLastFoot, out PlayerFoot markerNextFoot, out float markerStepProgress), Is.True);
        Assert.That(markerLastFoot, Is.EqualTo(PlayerFoot.Left));
        Assert.That(markerNextFoot, Is.EqualTo(PlayerFoot.Right));
        Assert.That(markerStepProgress, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(profile.TryEvaluateLoopPhase(1.25f, out markerLastFoot, out markerNextFoot, out markerStepProgress), Is.True);
        Assert.That(markerLastFoot, Is.EqualTo(PlayerFoot.Left));
        Assert.That(markerStepProgress, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(profile.TryEvaluateLoopPhase(0f, out PlayerFoot seamLastFoot, out PlayerFoot seamNextFoot, out float seamStepProgress), Is.True);
        Assert.That(seamLastFoot, Is.EqualTo(PlayerFoot.Right));
        Assert.That(seamNextFoot, Is.EqualTo(PlayerFoot.Left));
        Assert.That(seamStepProgress, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(profile.TryEvaluateLoopPhase(1f, out seamLastFoot, out seamNextFoot, out seamStepProgress), Is.True);
        Assert.That(seamLastFoot, Is.EqualTo(PlayerFoot.Right));
        Assert.That(seamNextFoot, Is.EqualTo(PlayerFoot.Left));
        Assert.That(seamStepProgress, Is.EqualTo(0.5f).Within(0.0001f));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_RejectsInvalidLoopPhaseInputs()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.2f), new PlantMarkerValue(PlayerFoot.Right, 0.8f));
        Assert.That(profile.TryEvaluateLoopPhase(float.NaN, out _, out _, out _), Is.False);
        Assert.That(profile.TryEvaluateLoopPhase(float.PositiveInfinity, out _, out _, out _), Is.False);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_RejectsInvalidLoopMarkerConfigurations()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        List<string> errors = new List<string>();
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, out _, out _, out _), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.25f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, out _, out _, out _), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0f), new PlantMarkerValue(PlayerFoot.Right, 0.75f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, out _, out _, out _), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Right, 0.75f), new PlantMarkerValue(PlayerFoot.Left, 0.25f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, out _, out _, out _), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.25f), new PlantMarkerValue(PlayerFoot.Left, 0.75f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, out _, out _, out _), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.2f), new PlantMarkerValue(PlayerFoot.Right, 0.5f), new PlantMarkerValue(PlayerFoot.Left, 0.8f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, out _, out _, out _), Is.False);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.2f), new PlantMarkerValue(PlayerFoot.Right, 0.25f));
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, out _, out _, out _), Is.True);
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
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, out _, out _, out _), Is.False);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Profile_RejectsNonPositiveLoopCycleDistance()
    {
        PlayerMotionDefinition definition = CreateDefinition(out PlayerMotionProfile profile);
        SetPlantMarkers(profile, new PlantMarkerValue(PlayerFoot.Left, 0.25f), new PlantMarkerValue(PlayerFoot.Right, 0.75f));
        FieldInfo cycleDistanceField = typeof(PlayerMotionProfile).GetField("cycleDistance", BindingFlags.Instance | BindingFlags.NonPublic);
        cycleDistanceField.SetValue(profile, 0f);
        List<string> errors = new List<string>();
        Assert.That(profile.TryEvaluateLoopPhase(0.5f, out _, out _, out _), Is.False);
        Assert.That(profile.ValidateLoopPhase(errors), Is.False);
        Assert.That(errors.Exists(error => error.Contains("CycleDistance")), Is.True);
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

    private static PlayerMotionDefinition CreateDefinition(out PlayerMotionProfile profile, float exitHandoffStart = 1f, float exitHandoffEnd = 1f)
    {
        profile = ScriptableObject.CreateInstance<PlayerMotionProfile>();
        profile.SetBakedData(1f, 2, new[] { Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 2f) }, new[] { 0f, 1f, 2f }, new[] { 0f, 0f, 0f }, string.Empty, 0, string.Empty, string.Empty);
        PlayerMotionDefinition definition = ScriptableObject.CreateInstance<PlayerMotionDefinition>();
        definition.Configure(profile, PlayerMotionTranslationPolicy.TravelAlongCapturedDirection, PlayerMotionRotationPolicy.FaceDirection, PlayerMotionBasisPolicy.DesiredDirection, 0f, 1f, exitHandoffStart, exitHandoffEnd);
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

    private static bool ResolveLandingPresentation(ScriptableObject animationSet, string presentationName, out object binding)
    {
        object landing = GetPropertyValue(animationSet, "Landing");
        binding = landing.GetType().GetProperty(presentationName, BindingFlags.Instance | BindingFlags.Public).GetValue(landing);
        return binding != null && GetPropertyValue(binding, "Definition") != null;
    }

    private static bool ResolveLandingCue(PlayerLandingSnapshot snapshot, out string cueName)
    {
        System.Type resolverType = FindLoadedType("PlayerLandingPresentationResolver");
        System.Type presentationType = FindLoadedType("PlayerLandingPresentationKey");
        MethodInfo method = resolverType.GetMethod("TryResolve", BindingFlags.Static | BindingFlags.Public);
        object[] arguments = { snapshot, null };
        bool result = (bool)method.Invoke(null, arguments);
        cueName = result && arguments[1] != null ? System.Enum.GetName(presentationType, arguments[1]) : null;
        return result;
    }

    private static string ResolveDriverLandingCue(PlayerLandingSnapshot snapshot, object transition)
    {
        System.Type driverType = FindLoadedType("PlayerSimulationDriver");
        System.Type transitionType = FindLoadedType("PlayerStateTransition");
        System.Type nullableTransitionType = typeof(System.Nullable<>).MakeGenericType(transitionType);
        object nullableTransition = System.Activator.CreateInstance(nullableTransitionType, transition);
        MethodInfo method = driverType.GetMethod("ResolveLandingPresentation", BindingFlags.Static | BindingFlags.NonPublic);
        object result = method.Invoke(null, new[] { nullableTransition, (object)snapshot });
        return result == null ? null : System.Enum.GetName(FindLoadedType("PlayerLandingPresentationKey"), result);
    }

    private static object CreateStateTransition(string previousStateName, string currentStateName, string reasonName)
    {
        System.Type transitionType = FindLoadedType("PlayerStateTransition");
        System.Type reasonType = FindLoadedType("PlayerStateTransitionReason");
        return System.Activator.CreateInstance(transitionType, FindLoadedType(previousStateName), FindLoadedType(currentStateName), System.Enum.Parse(reasonType, reasonName));
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
