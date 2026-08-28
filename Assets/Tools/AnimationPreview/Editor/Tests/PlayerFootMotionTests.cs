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
        public void SampleFootMotionProducesThreeFiniteEqualLengthArrays()
        {
            PlayerFootCalibration calibration = CreateCalibration();
            Vector3[] positions = new Vector3[36];
            for (int index = 0; index < positions.Length; index++) positions[index] = new Vector3(index * 0.01f, Mathf.Sin(index * 0.2f) * 0.03f, index * 0.005f);
            PlayerFootMotionBakeData data = PlayerMotionBaker.SampleFootMotion(positions, 60, calibration);
            Assert.That(data.SoleHeight, Is.Not.Null);
            Assert.That(data.VerticalSpeed, Is.Not.Null);
            Assert.That(data.HorizontalSpeed, Is.Not.Null);
            Assert.That(data.SoleHeight.Length, Is.EqualTo(positions.Length));
            Assert.That(data.VerticalSpeed.Length, Is.EqualTo(positions.Length));
            Assert.That(data.HorizontalSpeed.Length, Is.EqualTo(positions.Length));
            Assert.That(data.SoleHeight.All(IsFinite), Is.True);
            Assert.That(data.VerticalSpeed.All(IsFinite), Is.True);
            Assert.That(data.HorizontalSpeed.All(IsFinite), Is.True);
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
        public void RunLoopRightProfile_ContainsConfiguredPlantMarkers()
        {
            PlayerMotionProfile profile = AssetDatabase.LoadAssetAtPath<PlayerMotionProfile>("Assets/Settings/Player/Motion/Profiles/RunLoopRightFootMotionProfile.asset");
            Assert.That(profile, Is.Not.Null);
            List<PlayerFootPlantMarkerEditor.MarkerValue> markers = PlayerFootPlantMarkerEditor.Read(profile);
            Assert.That(markers.Count, Is.EqualTo(3));
            Assert.That(markers[0].Foot, Is.EqualTo(PlayerFoot.Right));
            Assert.That(markers[0].NormalizedTime, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(markers[1].Foot, Is.EqualTo(PlayerFoot.Left));
            Assert.That(markers[1].NormalizedTime, Is.EqualTo(0.41055f).Within(0.0001f));
            Assert.That(markers[2].Foot, Is.EqualTo(PlayerFoot.Right));
            Assert.That(markers[2].NormalizedTime, Is.EqualTo(0.9375f).Within(0.0001f));
        }

        [Test]
        public void PlantMarkersAreSortedAndPreserveRepeatedFootMarkers()
        {
            PlayerMotionProfile profile = CreateProfile(1f, 11);
            Assert.That(PlayerFootPlantMarkerEditor.TryAdd(profile, PlayerFoot.Right, 0.8f), Is.True);
            Assert.That(PlayerFootPlantMarkerEditor.TryAdd(profile, PlayerFoot.Left, 0.6f), Is.True);
            Assert.That(PlayerFootPlantMarkerEditor.TryAdd(profile, PlayerFoot.Right, 0.89f), Is.True);
            Assert.That(PlayerFootPlantMarkerEditor.TryAdd(profile, PlayerFoot.Left, 0.55f), Is.True);
            List<PlayerFootPlantMarkerEditor.MarkerValue> markers = PlayerFootPlantMarkerEditor.Read(profile);
            Assert.That(markers.Count, Is.EqualTo(4));
            Assert.That(markers[0].Foot, Is.EqualTo(PlayerFoot.Left));
            Assert.That(markers[0].NormalizedTime, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(markers[1].Foot, Is.EqualTo(PlayerFoot.Left));
            Assert.That(markers[1].NormalizedTime, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(markers[2].Foot, Is.EqualTo(PlayerFoot.Right));
            Assert.That(markers[2].NormalizedTime, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(markers[3].Foot, Is.EqualTo(PlayerFoot.Right));
            Assert.That(markers[3].NormalizedTime, Is.EqualTo(0.89f).Within(0.0001f));
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void DeletePlantRemovesNearestMarkerOnlyWithinOneSampleInterval()
        {
            PlayerMotionProfile profile = CreateProfile(1f, 21);
            PlayerFootPlantMarkerEditor.TryAdd(profile, PlayerFoot.Left, 0.2f);
            PlayerFootPlantMarkerEditor.TryAdd(profile, PlayerFoot.Right, 0.5f);
            PlayerFootPlantMarkerEditor.TryAdd(profile, PlayerFoot.Left, 0.8f);
            Assert.That(PlayerFootPlantMarkerEditor.TryRemoveNearest(profile, 0.52f, out PlayerFoot removedFoot), Is.True);
            Assert.That(removedFoot, Is.EqualTo(PlayerFoot.Right));
            Assert.That(PlayerFootPlantMarkerEditor.Read(profile).Count, Is.EqualTo(2));
            Assert.That(PlayerFootPlantMarkerEditor.TryRemoveNearest(profile, 0.95f, out _), Is.False);
            Assert.That(PlayerFootPlantMarkerEditor.Read(profile).Count, Is.EqualTo(2));
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void RebakePreservesPlantMarkers()
        {
            PlayerMotionProfile profile = CreateProfile(1f, 11);
            PlayerFootPlantMarkerEditor.TryAdd(profile, PlayerFoot.Left, 0.2f);
            PlayerFootPlantMarkerEditor.TryAdd(profile, PlayerFoot.Right, 0.8f);
            List<PlayerFootPlantMarkerEditor.MarkerValue> before = PlayerFootPlantMarkerEditor.Read(profile);
            profile.SetBakedData(1f, 60, CreatePositions(11), CreateFloats(11), CreateFloats(11), "clip", 1L, "model", "dependency", CreateFootData(), CreateFootData(), "calibration", "settings");
            List<PlayerFootPlantMarkerEditor.MarkerValue> after = PlayerFootPlantMarkerEditor.Read(profile);
            Assert.That(after.Select(marker => marker.Foot), Is.EqualTo(before.Select(marker => marker.Foot)));
            Assert.That(after.Select(marker => marker.NormalizedTime), Is.EqualTo(before.Select(marker => marker.NormalizedTime)));
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void InvalidClipProfileCombinationCannotWritePlantMarker()
        {
            PlayerMotionProfile profile = CreateProfile(1f, 11);
            AnimationClip clip = new AnimationClip();
            Assert.That(PlayerFootPlantMarkerEditor.TryAddForClip(profile, clip, PlayerFoot.Left, 0.25f), Is.False);
            Assert.That(PlayerFootPlantMarkerEditor.Read(profile), Is.Empty);
            UnityEngine.Object.DestroyImmediate(clip);
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void CalibrationHashChangesWhenRemainingSettingsChange()
        {
            PlayerFootCalibration calibration = CreateCalibration();
            string before = calibration.SettingsHash;
            calibration.Configure(null, new Vector3(0.01f, 0f, 0f), Vector3.zero, 0f);
            Assert.That(calibration.SettingsHash, Is.Not.EqualTo(before));
            SerializedObject serialized = new SerializedObject(calibration);
            Assert.That(serialized.FindProperty("contactHeightThreshold"), Is.Null);
            Assert.That(serialized.FindProperty("releaseHeightThreshold"), Is.Null);
            Assert.That(serialized.FindProperty("verticalSpeedThreshold"), Is.Null);
            Assert.That(serialized.FindProperty("horizontalSpeedThreshold"), Is.Null);
            Assert.That(serialized.FindProperty("stableTimeThreshold"), Is.Null);
            UnityEngine.Object.DestroyImmediate(calibration);
        }

        private static PlayerFootCalibration CreateCalibration()
        {
            PlayerFootCalibration calibration = ScriptableObject.CreateInstance<PlayerFootCalibration>();
            calibration.Configure(null, Vector3.zero, Vector3.zero, 0f);
            return calibration;
        }

        private static PlayerMotionProfile CreateProfile(float duration, int sampleCount = 3)
        {
            PlayerMotionProfile profile = ScriptableObject.CreateInstance<PlayerMotionProfile>();
            profile.SetBakedData(duration, 60, CreatePositions(sampleCount), CreateFloats(sampleCount), CreateFloats(sampleCount), "clip", 1L, "model", "dependency");
            return profile;
        }

        private static Vector2[] CreatePositions(int count = 3)
        {
            return Enumerable.Range(0, count).Select(index => new Vector2(index, 0f)).ToArray();
        }

        private static float[] CreateFloats(int count = 3)
        {
            return Enumerable.Range(0, count).Select(index => index / (float)Math.Max(1, count - 1)).ToArray();
        }

        private static PlayerFootMotionBakeData CreateFootData()
        {
            return new PlayerFootMotionBakeData
            {
                SoleHeight = CreateFloats(11),
                VerticalSpeed = CreateFloats(11),
                HorizontalSpeed = CreateFloats(11)
            };
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
