using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ProjectTools.AnimationPreview;

public class PlayerFootMotionTests
{
        [Test]
        public void ClipSamplingIsStableAfterPreviewTimeChanges()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/X Bot.fbx");
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Animation/Player/Walk/Walk_Lfoot.fbx").OfType<AnimationClip>().First(candidate => !candidate.name.StartsWith("__preview__", StringComparison.Ordinal));
            PlayerFootCalibration calibration = AssetDatabase.LoadAssetAtPath<PlayerFootCalibration>("Assets/Settings/Player/Motion/FootCalibration/XBotFootCalibration.asset");
            using AnimationPreviewSession session = new AnimationPreviewSession();
            Assert.That(session.SetModel(model), Is.True);
            Assert.That(session.SetClip(new AnimationPreviewClipEntry(clip, AssetDatabase.GetAssetPath(clip))), Is.True);
            PlayerMotionBakeResult initial = session.SampleMotion(60, calibration);
            session.SetTime(session.Length);
            PlayerMotionBakeResult afterTimeChange = session.SampleMotion(60, calibration);
            Assert.That(Vector3.Distance(initial.LeftFootPositions[0], afterTimeChange.LeftFootPositions[0]), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(initial.LeftFootPositions[initial.LeftFootPositions.Length - 1], afterTimeChange.LeftFootPositions[afterTimeChange.LeftFootPositions.Length - 1]), Is.LessThan(0.001f));
        }

        [Test]
        public void DetectFootMotionProducesPlantAndLiftWithHysteresis()
        {
            PlayerFootCalibration calibration = CreateCalibration();
            Vector3[] positions = new Vector3[36];
            for (int index = 0; index < positions.Length; index++)
            {
                float height = index < 20 ? 0f : Mathf.Lerp(0f, 0.12f, (index - 20) / 8f);
                positions[index] = new Vector3(0f, height, 0f);
            }
            PlayerFootMotionBakeData data = PlayerMotionBaker.DetectFootMotion(positions, 60, calibration);
            Assert.That(Array.Exists(data.AutoMarkers, marker => marker.Plant), Is.True);
            Assert.That(Array.Exists(data.AutoMarkers, marker => marker.Lift), Is.True);
            Assert.That(data.AutoMarkers[10].Contact, Is.True);
            Assert.That(data.AutoMarkers[data.AutoMarkers.Length - 1].Contact, Is.False);
            UnityEngine.Object.DestroyImmediate(calibration);
        }

        [Test]
        public void DetectFootMotionKeepsContactBetweenContactAndReleaseHeight()
        {
            PlayerFootCalibration calibration = CreateCalibration();
            Vector3[] positions = new Vector3[60];
            for (int index = 0; index < positions.Length; index++)
            {
                float height = index < 20 ? 0f : index < 50 ? Mathf.Lerp(0f, 0.05f, (index - 20) / 30f) : Mathf.Lerp(0.05f, 0.09f, (index - 50) / 9f);
                positions[index] = new Vector3(0f, height, 0f);
            }
            PlayerFootMotionBakeData data = PlayerMotionBaker.DetectFootMotion(positions, 60, calibration);
            Assert.That(data.AutoMarkers[35].Contact, Is.True);
            Assert.That(data.AutoMarkers[35].Lift, Is.False);
            Assert.That(Array.Exists(data.AutoMarkers, marker => marker.Lift), Is.True);
            UnityEngine.Object.DestroyImmediate(calibration);
        }

        [Test]
        public void ProfileAndRuntimeUseSelectedFootProfile()
        {
            PlayerMotionProfile defaultProfile = CreateProfile(1f);
            PlayerMotionProfile leftProfile = CreateProfile(2f);
            PlayerMotionProfile rightProfile = CreateProfile(3f);
            PlayerMotionDefinition definition = ScriptableObject.CreateInstance<PlayerMotionDefinition>();
            definition.Configure(defaultProfile, PlayerMotionTranslationPolicy.TravelAlongCapturedDirection, PlayerMotionRotationPolicy.KeepFacing, PlayerMotionBasisPolicy.EntryFacing, 0f, 1f, 1f, 1f);
            definition.ConfigureFootProfiles(leftProfile, rightProfile, true);
            Assert.That(definition.ResolveProfile(PlayerFoot.Left), Is.SameAs(leftProfile));
            Assert.That(definition.ResolveProfile(PlayerFoot.Right), Is.SameAs(rightProfile));
            Assert.That(definition.ResolveProfile(PlayerFoot.Unknown), Is.SameAs(defaultProfile));

            PlayerMotionRuntime runtime = new PlayerMotionRuntime();
            runtime.Begin(definition, leftProfile, PlayerFoot.Left, Vector3.forward, Vector3.forward);
            PlayerMotionFrame frame = runtime.Advance(1f, default);
            Assert.That(runtime.Snapshot.ActiveProfile, Is.SameAs(leftProfile));
            Assert.That(runtime.Snapshot.SupportFoot, Is.EqualTo(PlayerFoot.Left));
            Assert.That(frame.CurrentProgress, Is.EqualTo(0.5f).Within(0.001f));

            UnityEngine.Object.DestroyImmediate(defaultProfile);
            UnityEngine.Object.DestroyImmediate(leftProfile);
            UnityEngine.Object.DestroyImmediate(rightProfile);
            UnityEngine.Object.DestroyImmediate(definition);
        }

        [Test]
        public void ManualFootMarkersCanBeCopiedAndRestored()
        {
            PlayerMotionProfile profile = CreateProfile(1f);
            PlayerFootMotionBakeData left = CreateFootData(true);
            PlayerFootMotionBakeData right = CreateFootData(false);
            profile.SetBakedData(1f, 60, CreatePositions(), CreateFloats(), CreateFloats(), "clip", 1L, "model", "dependency", left, right, "calibration", "settings");
            profile.CopyAutoFootMarkersToManual(PlayerFoot.Left);
            Assert.That(profile.LeftFoot.UseManualOverride, Is.True);
            profile.RestoreAutomaticFootMarkers(PlayerFoot.Left);
            Assert.That(profile.LeftFoot.UseManualOverride, Is.False);
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void CalibrationHashChangesWhenThresholdChanges()
        {
            PlayerFootCalibration calibration = CreateCalibration();
            string before = calibration.SettingsHash;
            calibration.Configure(null, Vector3.zero, Vector3.zero, 0f, 0.03f, 0.07f, 0.2f, 0.25f, 0.05f);
            Assert.That(calibration.SettingsHash, Is.Not.EqualTo(before));
            UnityEngine.Object.DestroyImmediate(calibration);
        }

        private static PlayerFootCalibration CreateCalibration()
        {
            PlayerFootCalibration calibration = ScriptableObject.CreateInstance<PlayerFootCalibration>();
            calibration.Configure(null, Vector3.zero, Vector3.zero, 0f, 0.04f, 0.07f, 0.2f, 0.25f, 0.05f);
            return calibration;
        }

        private static PlayerMotionProfile CreateProfile(float duration)
        {
            PlayerMotionProfile profile = ScriptableObject.CreateInstance<PlayerMotionProfile>();
            profile.SetBakedData(duration, 60, CreatePositions(), CreateFloats(), CreateFloats(), "clip", 1L, "model", "dependency");
            return profile;
        }

        private static Vector2[] CreatePositions() => new[] { Vector2.zero, new Vector2(1f, 0f), new Vector2(2f, 0f) };
        private static float[] CreateFloats() => new[] { 0f, 0.5f, 1f };

        private static PlayerFootMotionBakeData CreateFootData(bool contact)
        {
            PlayerFootContactMarker[] markers = contact
                ? new[] { new PlayerFootContactMarker(false, false, false), new PlayerFootContactMarker(true, true, false), new PlayerFootContactMarker(false, false, true) }
                : new[] { new PlayerFootContactMarker(false, false, false), new PlayerFootContactMarker(false, false, false), new PlayerFootContactMarker(false, false, false) };
            return new PlayerFootMotionBakeData
            {
                SoleHeight = new[] { 0f, 0f, 0.1f },
                VerticalSpeed = new[] { 0f, 0f, 0f },
                HorizontalSpeed = new[] { 0f, 0f, 0f },
                StableTime = new[] { 0f, 0.05f, 0f },
                AutoMarkers = markers
            };
        }
}
