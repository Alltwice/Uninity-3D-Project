using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class PlayerLandingTrackerTests
{
    private PlayerMovementConfig config;
    private PlayerLandingTracker tracker;

    [SetUp]
    public void SetUp()
    {
        config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        tracker = new PlayerLandingTracker(config.Landing);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(config);
    }

    [Test]
    public void Advance_TracksPeakAndEmitsOneLandingSnapshot()
    {
        Assert.That(tracker.Advance(Result(true), 0f, PlayerLocomotionMode.Run, PlayerLocomotionMode.Run, true).IsLandingEvent, Is.False);
        tracker.Advance(Result(false), 0.5f, PlayerLocomotionMode.Air, PlayerLocomotionMode.Run, true);
        tracker.Advance(Result(false), 2.5f, PlayerLocomotionMode.Air, PlayerLocomotionMode.Run, true);
        tracker.Advance(Result(false), 1f, PlayerLocomotionMode.Air, PlayerLocomotionMode.Idle, false);

        PlayerLandingSnapshot landing = tracker.Advance(Result(true, true, 4f), 0f, PlayerLocomotionMode.Air, PlayerLocomotionMode.Idle, false);

        Assert.That(landing.IsLandingEvent, Is.True);
        Assert.That(landing.Sequence, Is.EqualTo(1));
        Assert.That(landing.Severity, Is.EqualTo(PlayerLandingSeverity.Lv3));
        Assert.That(landing.FallDistance, Is.EqualTo(2.5f).Within(0.0001f));
        Assert.That(landing.ImpactSpeed, Is.EqualTo(4f));
        Assert.That(landing.AirEntryGroundMode, Is.EqualTo(PlayerLocomotionMode.Run));
        Assert.That(landing.HasMoveIntentAtImpact, Is.False);
        Assert.That(landing.TargetGroundMode, Is.EqualTo(PlayerLocomotionMode.Idle));
        Assert.That(tracker.Advance(Result(true), 0f, PlayerLocomotionMode.Idle, PlayerLocomotionMode.Idle, false).IsLandingEvent, Is.False);
    }

    [Test]
    public void Advance_UsesHighestSeverityFromDistanceAndImpact()
    {
        tracker.Advance(Result(true), 0f, PlayerLocomotionMode.Walk, PlayerLocomotionMode.Walk, true);
        tracker.Advance(Result(false), 0.5f, PlayerLocomotionMode.Air, PlayerLocomotionMode.Walk, true);

        PlayerLandingSnapshot landing = tracker.Advance(Result(true, true, config.Landing.Lv4MinImpactSpeed), 0f, PlayerLocomotionMode.Air, PlayerLocomotionMode.Walk, true);

        Assert.That(landing.FallDistance, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(landing.Severity, Is.EqualTo(PlayerLandingSeverity.Lv4));
    }

    [Test]
    public void Advance_FirstAirSampleUsesTargetModeAsEntryFact()
    {
        tracker.Advance(Result(false), 1f, PlayerLocomotionMode.Air, PlayerLocomotionMode.FastRun, true);
        PlayerLandingSnapshot landing = tracker.Advance(Result(true, true, 0f), 1f, PlayerLocomotionMode.Air, PlayerLocomotionMode.FastRun, true);

        Assert.That(landing.AirEntryGroundMode, Is.EqualTo(PlayerLocomotionMode.FastRun));
        Assert.That(landing.Severity, Is.EqualTo(PlayerLandingSeverity.Lv1));
    }

    [Test]
    public void Reset_DiscardsCurrentAirLifecycleAndKeepsSequenceMonotonic()
    {
        tracker.Advance(Result(false), 4f, PlayerLocomotionMode.Air, PlayerLocomotionMode.Run, true);
        PlayerLandingSnapshot first = tracker.Advance(Result(true, true, 10f), 0f, PlayerLocomotionMode.Air, PlayerLocomotionMode.Run, true);
        tracker.Advance(Result(false), 5f, PlayerLocomotionMode.Air, PlayerLocomotionMode.Run, true);
        tracker.Reset(PlayerLocomotionMode.Walk);
        tracker.Advance(Result(false), 1f, PlayerLocomotionMode.Air, PlayerLocomotionMode.Walk, true);
        PlayerLandingSnapshot second = tracker.Advance(Result(true, true, 0f), 1f, PlayerLocomotionMode.Air, PlayerLocomotionMode.Walk, true);

        Assert.That(first.Sequence, Is.EqualTo(1));
        Assert.That(second.Sequence, Is.EqualTo(2));
        Assert.That(second.FallDistance, Is.Zero);
        Assert.That(second.AirEntryGroundMode, Is.EqualTo(PlayerLocomotionMode.Walk));
    }

    [Test]
    public void LandingSettings_DefaultThresholdsAreOrdered()
    {
        List<string> errors = new List<string>();
        Assert.That(config.Landing.Validate(errors), Is.True);
        Assert.That(errors, Is.Empty);
    }

    private static PlayerMotorResult Result(bool isGrounded, bool justLanded = false, float impactSpeed = 0f)
    {
        return new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.zero, 0f, isGrounded, justLanded, impactSpeed, CollisionFlags.None);
    }
}
