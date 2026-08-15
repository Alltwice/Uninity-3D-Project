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
        runtime.Begin(definition, Vector3.forward, Vector3.forward, Vector3.forward);
        Vector3 total = Vector3.zero;
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward);
        int guard = fps * 3;
        while (!runtime.Snapshot.JustCompleted && guard-- > 0) total += runtime.Advance(1f / fps, intent, Vector3.forward, 30f, 35f).AuthoredPlanarDisplacement;
        Assert.That(total.z, Is.EqualTo(profile.EvaluateTravelDistance(1f)).Within(0.0001f));
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void YawUnwrap_CrossingSignedBoundaryRemainsContinuous()
    {
        float unwrapped = PlayerMotionMath.UnwrapYaw(179f, -179f, 179f);
        Assert.That(unwrapped, Is.EqualTo(181f).Within(0.0001f));
    }

    [Test]
    public void Replacement_NewInstanceOwnsCompletion()
    {
        PlayerMotionDefinition first = CreateDefinition(out PlayerMotionProfile firstProfile);
        PlayerMotionDefinition second = CreateDefinition(out PlayerMotionProfile secondProfile);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        ulong oldId = runtime.Begin(first, Vector3.forward, Vector3.forward, Vector3.forward);
        runtime.Advance(0.5f, PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward), Vector3.forward, 30f, 35f);
        ulong newId = runtime.Begin(second, Vector3.forward, Vector3.forward, Vector3.forward);
        Assert.That(newId, Is.Not.EqualTo(oldId));
        Assert.That(runtime.Snapshot.ActiveDefinition, Is.SameAs(second));
        Assert.That(runtime.Snapshot.JustCancelled, Is.True);
        runtime.Advance(1f, PlayerGameplayIntent.Create(Vector3.forward, Vector3.forward), Vector3.forward, 30f, 35f);
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
        runtime.Begin(definition, Vector3.forward, Vector3.forward, Vector3.forward);
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
        PlayerMotorCommand authored = PlayerMotionComposer.Compose(intent, new PlayerMotionFrame(definition, Vector3.forward, 0f, 1f, 0f), result, config, 0.1f, Vector3.forward);
        PlayerMotorCommand locomotion = PlayerMotionComposer.Compose(intent, new PlayerMotionFrame(definition, Vector3.forward, 0f, 0f, 0f), result, config, 0.1f, Vector3.forward);
        Assert.That(authored.TranslationMode, Is.EqualTo(PlayerMotorTranslationMode.DisplacementDriven));
        Assert.That(authored.PlanarDisplacement.z, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(locomotion.TranslationMode, Is.EqualTo(PlayerMotorTranslationMode.VelocityDriven));
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
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
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, new PlayerMotionFrame(definition, Vector3.forward, 0f, 0.5f, 0f), result, config, 0.25f, Vector3.forward);
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
        Assert.That(definition.EvaluateRotationAuthority(1f), Is.EqualTo(1f));
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
        runtime.Begin(definition, Vector3.forward, Vector3.forward, Vector3.forward);
        Vector3 total = Vector3.zero;
        int guard = fps * 3;
        while (!runtime.Snapshot.JustCompleted && guard-- > 0)
        {
            PlayerMotionFrame frame = runtime.Advance(1f / fps, intent, Vector3.forward, 30f, 120f);
            PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, frame, result, config, 1f / fps, Vector3.forward);
            total += command.PlanarDisplacement;
        }
        Assert.That(total.z, Is.EqualTo(profile.EvaluateTravelDistance(1f)).Within(0.0001f));
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void PresentationPhase_IsTheMotionProgress()
    {
        PlayerMotionSnapshot snapshot = new PlayerMotionSnapshot(null, 4, 0.43f, 0f, false, true, false, false, 1f, 1f);
        Assert.That(PlayerMotionPresentationPhase.ResolveBoundaryProgress(snapshot), Is.EqualTo(0.43f));
    }

    [Test]
    public void MotorKinematics_UsesActualDisplacement()
    {
        Vector3 actualVelocity = PlayerMotorKinematics.CalculateActualPlanarVelocity(new Vector3(0.2f, 0f, 0f), 0.1f);
        Assert.That(actualVelocity.x, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void Turn180_ZeroInputCancelsForExactlyThisSnapshot()
    {
        PlayerMotionDefinition definition = CreateTurnDefinition(out PlayerMotionProfile profile);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.back, Vector3.back);
        PlayerMotionFrame frame = runtime.Advance(0.1f, PlayerGameplayIntent.Create(Vector3.zero, Vector3.forward), Vector3.forward, 30f, 120f);
        Assert.That(frame.IsValid, Is.False);
        Assert.That(runtime.Snapshot.JustCancelled, Is.True);
        Assert.That(runtime.Snapshot.IsActive, Is.False);
        runtime.BeginFrame();
        Assert.That(runtime.Snapshot.JustCancelled, Is.False);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Turn180_LargeIntentChangeCancels()
    {
        PlayerMotionDefinition definition = CreateTurnDefinition(out PlayerMotionProfile profile);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        runtime.Begin(definition, Vector3.forward, Vector3.back, Vector3.back);
        runtime.Advance(0.1f, PlayerGameplayIntent.Create(Vector3.right, Vector3.forward), Vector3.forward, 30f, 120f);
        Assert.That(runtime.Snapshot.JustCancelled, Is.True);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Turn180_RotationReleaseKeepsTranslationActive()
    {
        PlayerMotionDefinition definition = CreateTurnDefinition(out PlayerMotionProfile profile);
        PlayerMotionRuntime runtime = new PlayerMotionRuntime();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(Vector3.back, Vector3.forward);
        runtime.Begin(definition, Vector3.forward, Vector3.back, Vector3.back);
        PlayerMotionFrame locked = runtime.Advance(0.25f, intent, Vector3.forward, 30f, 120f);
        Assert.That(locked.RotationAuthority, Is.GreaterThan(0f));
        Vector3 releasedFacing = Quaternion.AngleAxis(90f, Vector3.up) * Vector3.forward;
        PlayerMotionFrame released = runtime.Advance(0.25f, intent, releasedFacing, 30f, 120f);
        Assert.That(released.RotationAuthority, Is.Zero);
        Assert.That(released.AuthoredPlanarDisplacement.sqrMagnitude, Is.GreaterThan(0f));
        Assert.That(runtime.Snapshot.IsActive, Is.True);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(profile);
    }

    private static PlayerMotionDefinition CreateDefinition(out PlayerMotionProfile profile, float handoffStart = 1f, float handoffEnd = 1f)
    {
        profile = ScriptableObject.CreateInstance<PlayerMotionProfile>();
        profile.SetBakedData(1f, 2, new[] { Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 2f) }, new[] { 0f, 1f, 2f }, new[] { 0f, 0f, 0f }, string.Empty, 0, string.Empty, string.Empty);
        PlayerMotionDefinition definition = ScriptableObject.CreateInstance<PlayerMotionDefinition>();
        definition.Configure(profile, PlayerMotionTranslationPolicy.TravelAlongDirection, PlayerMotionRotationPolicy.FaceDirection, PlayerMotionBasisPolicy.DesiredDirection, PlayerMotionControlPolicy.None, 0f, 1f, handoffStart, handoffEnd);
        return definition;
    }

    private static PlayerMotionDefinition CreateTurnDefinition(out PlayerMotionProfile profile)
    {
        profile = ScriptableObject.CreateInstance<PlayerMotionProfile>();
        profile.SetBakedData(1f, 2, new[] { Vector2.zero, new Vector2(0.5f, 0f), new Vector2(1f, 0f) }, new[] { 0f, 0.5f, 1f }, new[] { 0f, -90f, -180f }, string.Empty, 0, string.Empty, string.Empty);
        PlayerMotionDefinition definition = ScriptableObject.CreateInstance<PlayerMotionDefinition>();
        definition.Configure(profile, PlayerMotionTranslationPolicy.LocalTrajectory, PlayerMotionRotationPolicy.ProfileYaw, PlayerMotionBasisPolicy.EntryFacing, PlayerMotionControlPolicy.Turn180, 0f, 1f, 0.8f, 1f);
        return definition;
    }
}
