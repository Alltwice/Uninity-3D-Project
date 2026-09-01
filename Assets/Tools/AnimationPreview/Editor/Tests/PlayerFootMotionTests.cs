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
        public void SampleFootMotionUsesActualClipDuration()
        {
            PlayerFootCalibration calibration = CreateCalibration();
            Vector3[] positions = { Vector3.zero, Vector3.right, Vector3.right * 2f };
            PlayerFootMotionBakeData data = PlayerMotionBaker.SampleFootMotion(positions, 2f, calibration);
            Assert.That(data.HorizontalSpeed, Is.All.EqualTo(1f).Within(0.0001f));
            UnityEngine.Object.DestroyImmediate(calibration);
        }

        [Test]
        public void NewProfileDefaultsToManualOverrideForAssetCompatibility()
        {
            PlayerMotionProfile profile = ScriptableObject.CreateInstance<PlayerMotionProfile>();
            Assert.That(profile.PlantMarkerMode, Is.EqualTo(PlayerPlantMarkerMode.ManualOverride));
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void AutoMarkerReplacementStoresConfidenceAndDetectionVersion()
        {
            PlayerMotionProfile profile = CreateProfile(1f, 11);
            profile.SetPlantAuthoringSettings(PlayerFootPlantDetectionMode.Start, PlayerPlantMarkerMode.Auto);
            profile.ReplacePlantMarkers(new[] { new PlayerFootPlantMarker(PlayerFoot.Left, 0.4f, 0.75f) }, PlayerMotionProfile.CurrentFootPlantDetectionVersion);
            Assert.That(profile.PlantMarkers.Count, Is.EqualTo(1));
            Assert.That(profile.PlantMarkers[0].Confidence, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(profile.FootPlantDetectionVersion, Is.EqualTo(PlayerMotionProfile.CurrentFootPlantDetectionVersion));
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void AutoValidationReportsLowConfidenceAsWarning()
        {
            PlayerMotionProfile profile = CreateProfile(1f, 11);
            profile.SetPlantAuthoringSettings(PlayerFootPlantDetectionMode.Start, PlayerPlantMarkerMode.Auto);
            profile.SetBakedData(1f, 60, CreatePositions(11), CreateFloats(11), CreateFloats(11), "missingClip", 1L, "missingModel", "dependency", CreateFootData(), CreateFootData(), "missingCalibration", "settings");
            profile.ReplacePlantMarkers(new[] { new PlayerFootPlantMarker(PlayerFoot.Left, 0.4f, 0.5f) }, PlayerMotionProfile.CurrentFootPlantDetectionVersion);
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            Assert.That(PlayerMotionBaker.Validate(profile, errors, warnings), Is.False);
            Assert.That(warnings.Any(message => message.Contains("Confidence")), Is.True);
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void ManualValidationIgnoresLegacyMarkerConfidence()
        {
            PlayerMotionProfile profile = CreateProfile(1f, 11);
            profile.ReplacePlantMarkers(new[] { new PlayerFootPlantMarker(PlayerFoot.Left, 0.4f, 0f) }, 0);
            List<string> warnings = new List<string>();
            PlayerMotionBaker.Validate(profile, new List<string>(), warnings);
            Assert.That(warnings, Is.Empty);
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void StartDetectionSuppressesInitialContactAndEmitsSwingToContact()
        {
            float[] leftHeight = { 0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f, 0f };
            List<PlayerFootPlantDetection> detections = PlayerFootPlantDetector.Detect(CreateDetectorFootData(leftHeight), CreateDetectorFootData(Constant(leftHeight.Length, 1f)), 0.28f, leftHeight.Length, PlayerFootPlantDetectionMode.Start);
            Assert.That(detections.Count, Is.EqualTo(1));
            Assert.That(detections[0].Foot, Is.EqualTo(PlayerFoot.Left));
            Assert.That(detections[0].NormalizedTime, Is.GreaterThan(0.5f));
        }

        [Test]
        public void StopDetectionKeepsTerminalContactWindow()
        {
            float[] leftHeight = { 1f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f };
            List<PlayerFootPlantDetection> detections = PlayerFootPlantDetector.Detect(CreateDetectorFootData(leftHeight), CreateDetectorFootData(Constant(leftHeight.Length, 1f)), 0.18f, leftHeight.Length, PlayerFootPlantDetectionMode.Stop);
            Assert.That(detections.Count, Is.EqualTo(1));
            Assert.That(detections[0].Foot, Is.EqualTo(PlayerFoot.Left));
        }

        [Test]
        public void LoopDetectionMergesSeamAndProducesAlternatingFeet()
        {
            float[] leftHeight = { 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f, 0f };
            float[] rightHeight = { 1f, 1f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
            List<PlayerFootPlantDetection> detections = PlayerFootPlantDetector.Detect(CreateDetectorFootData(leftHeight), CreateDetectorFootData(rightHeight), 0.4f, leftHeight.Length, PlayerFootPlantDetectionMode.Loop);
            Assert.That(detections.Count, Is.EqualTo(2));
            Assert.That(detections[0].Foot, Is.Not.EqualTo(detections[1].Foot));
            Assert.That(detections.All(detection => detection.NormalizedTime > 0f && detection.NormalizedTime < 1f), Is.True);
        }

        [Test]
        public void TurnDetectionReducesHorizontalSpeedPenalty()
        {
            float[] height = { 1f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f };
            float[] horizontal = { 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f };
            PlayerFootMotionBakeData left = CreateDetectorFootData(height, null, horizontal);
            PlayerFootMotionBakeData right = CreateDetectorFootData(Constant(height.Length, 1f));
            Assert.That(PlayerFootPlantDetector.Detect(left, right, 0.18f, height.Length, PlayerFootPlantDetectionMode.Start), Is.Empty);
            Assert.That(PlayerFootPlantDetector.Detect(left, right, 0.18f, height.Length, PlayerFootPlantDetectionMode.Turn).Count, Is.EqualTo(1));
        }

        [Test]
        public void VerticalSpeedSignDoesNotChangeDetection()
        {
            float[] height = { 1f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f };
            float[] vertical = { 1f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f };
            PlayerFootMotionBakeData right = CreateDetectorFootData(Constant(height.Length, 1f));
            List<PlayerFootPlantDetection> positive = PlayerFootPlantDetector.Detect(CreateDetectorFootData(height, vertical), right, 0.18f, height.Length, PlayerFootPlantDetectionMode.Start);
            List<PlayerFootPlantDetection> negative = PlayerFootPlantDetector.Detect(CreateDetectorFootData(height, vertical.Select(value => -value).ToArray()), right, 0.18f, height.Length, PlayerFootPlantDetectionMode.Start);
            Assert.That(negative.Select(value => value.NormalizedTime), Is.EqualTo(positive.Select(value => value.NormalizedTime)).Within(0.0001f));
        }

        [Test]
        public void ShortContactScoreGapDoesNotCreateDuplicatePlant()
        {
            float[] height = { 1f, 1f, 1f, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f };
            List<PlayerFootPlantDetection> detections = PlayerFootPlantDetector.Detect(CreateDetectorFootData(height), CreateDetectorFootData(Constant(height.Length, 1f)), 0.22f, height.Length, PlayerFootPlantDetectionMode.Start);
            Assert.That(detections.Count, Is.EqualTo(1));
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
            Assert.That(runtime.Snapshot.EntryLastPlantFoot, Is.EqualTo(PlayerFoot.Left));
            Assert.That(frame.CurrentProgress, Is.EqualTo(0.5f).Within(0.001f));

            UnityEngine.Object.DestroyImmediate(defaultProfile);
            UnityEngine.Object.DestroyImmediate(leftProfile);
            UnityEngine.Object.DestroyImmediate(rightProfile);
            UnityEngine.Object.DestroyImmediate(definition);
        }

        [Test]
        public void ConfiguredLoopProfilesSatisfyPhaseContractAcrossCycle()
        {
            HashSet<PlayerMotionProfile> profiles = new HashSet<PlayerMotionProfile>();
            string[] profileGuids = AssetDatabase.FindAssets("t:PlayerMotionProfile", new[] { "Assets/Settings/Player/Motion/Profiles" });
            foreach (string profileGuid in profileGuids)
            {
                string profilePath = AssetDatabase.GUIDToAssetPath(profileGuid);
                if (!profilePath.Contains("Loop")) continue;
                PlayerMotionProfile profile = AssetDatabase.LoadAssetAtPath<PlayerMotionProfile>(profilePath);
                Assert.That(profile, Is.Not.Null, profilePath);
                profiles.Add(profile);
            }
            Assert.That(profiles.Count, Is.EqualTo(6));
            foreach (PlayerMotionProfile profile in profiles)
            {
                Assert.That(profile.ValidateLoopPhase(new List<string>()), Is.True, profile.name);
                IReadOnlyList<PlayerFootPlantMarker> markers = profile.PlantMarkers;
                for (int index = 0; index < markers.Count; index++)
                {
                    Assert.That(profile.TryEvaluateLoopPhase(markers[index].NormalizedTime, 1f, out PlayerLocomotionPhaseSnapshot markerSnapshot), Is.True, profile.name);
                    Assert.That(markerSnapshot.LastPlantFoot, Is.EqualTo(markers[index].Foot), profile.name);
                    Assert.That(markerSnapshot.StepProgress, Is.EqualTo(0f).Within(0.0001f), profile.name);
                    int previousIndex = (index + markers.Count - 1) % markers.Count;
                    float previousTime = index == 0 ? markers[previousIndex].NormalizedTime - 1f : markers[previousIndex].NormalizedTime;
                    float midpoint = (previousTime + markers[index].NormalizedTime) * 0.5f;
                    Assert.That(profile.TryEvaluateLoopPhase(midpoint, 1f, out PlayerLocomotionPhaseSnapshot midpointSnapshot), Is.True, profile.name);
                    Assert.That(midpointSnapshot.HasPhase, Is.True, profile.name);
                    Assert.That(midpointSnapshot.NextPlantFoot, Is.EqualTo(markers[index].Foot), profile.name);
                    Assert.That(midpointSnapshot.StepProgress, Is.GreaterThan(0f).And.LessThan(1f), profile.name);
                }
            }
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
            Assert.That(markers.All(marker => Mathf.Approximately(marker.Confidence, 1f)), Is.True);
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

        [Test, Ignore("现有人工 Marker 含 Stop 尾部裁剪和 Turn 入脚语义；补充 Root 停止时刻与 EntryFoot 输入后重新启用。")]
        public void ExistingLabeledProfilesReachAutoDetectionAcceptance()
        {
            int truePositive = 0;
            int falsePositive = 0;
            int falseNegative = 0;
            int highConfidenceFalsePositive = 0;
            List<float> normalizedErrors = new List<float>();
            List<string> profileDiagnostics = new List<string>();
            string[] profileGuids = AssetDatabase.FindAssets("t:PlayerMotionProfile", new[] { "Assets/Settings/Player/Motion/Profiles" });
            foreach (string profileGuid in profileGuids)
            {
                PlayerMotionProfile profile = AssetDatabase.LoadAssetAtPath<PlayerMotionProfile>(AssetDatabase.GUIDToAssetPath(profileGuid));
                if (profile == null || !profile.HasFootData || !profile.HasPlantMarkers || !TryResolveDetectionMode(profile.name, out PlayerFootPlantDetectionMode mode)) continue;
                PlayerFootMotionBakeData left = CopyFootData(profile.LeftFoot);
                PlayerFootMotionBakeData right = CopyFootData(profile.RightFoot);
                List<PlayerFootPlantDetection> detected = PlayerFootPlantDetector.Detect(left, right, profile.Duration, profile.SampleCount, mode);
                profileDiagnostics.Add(profile.name + " Manual=[" + string.Join(", ", profile.PlantMarkers.Select(marker => $"{marker.Foot}:{marker.NormalizedTime:F3}")) + "] Auto=[" + string.Join(", ", detected.Select(marker => $"{marker.Foot}:{marker.NormalizedTime:F3}/{marker.Confidence:F2}")) + "]");
                bool[] matched = new bool[detected.Count];
                float sampleInterval = 1f / (profile.SampleCount - 1);
                for (int markerIndex = 0; markerIndex < profile.PlantMarkers.Count; markerIndex++)
                {
                    PlayerFootPlantMarker marker = profile.PlantMarkers[markerIndex];
                    int nearestIndex = -1;
                    float nearestDistance = float.PositiveInfinity;
                    for (int detectedIndex = 0; detectedIndex < detected.Count; detectedIndex++)
                    {
                        float distance = Mathf.Abs(marker.NormalizedTime - detected[detectedIndex].NormalizedTime);
                        if (mode == PlayerFootPlantDetectionMode.Loop) distance = Mathf.Min(distance, 1f - distance);
                        if (matched[detectedIndex] || marker.Foot != detected[detectedIndex].Foot || distance > sampleInterval * 2f || distance >= nearestDistance) continue;
                        nearestIndex = detectedIndex;
                        nearestDistance = distance;
                    }
                    if (nearestIndex < 0) { falseNegative++; continue; }
                    matched[nearestIndex] = true;
                    truePositive++;
                    normalizedErrors.Add(nearestDistance / sampleInterval);
                }
                for (int detectedIndex = 0; detectedIndex < detected.Count; detectedIndex++)
                {
                    if (matched[detectedIndex]) continue;
                    falsePositive++;
                    if (detected[detectedIndex].Confidence >= PlayerFootPlantDetector.LowConfidenceThreshold) highConfidenceFalsePositive++;
                }
            }
            float precision = truePositive / (float)Mathf.Max(1, truePositive + falsePositive);
            float recall = truePositive / (float)Mathf.Max(1, truePositive + falseNegative);
            float f1 = 2f * precision * recall / Mathf.Max(0.0001f, precision + recall);
            normalizedErrors.Sort();
            float medianError = normalizedErrors.Count == 0 ? float.PositiveInfinity : normalizedErrors[normalizedErrors.Count / 2];
            string metrics = $"TP={truePositive} FP={falsePositive} FN={falseNegative} Precision={precision:F3} Recall={recall:F3} F1={f1:F3} MedianSamples={medianError:F3} HighConfidenceFP={highConfidenceFalsePositive}\n" + string.Join("\n", profileDiagnostics);
            Assert.That(precision, Is.GreaterThanOrEqualTo(0.85f), metrics);
            Assert.That(recall, Is.GreaterThanOrEqualTo(0.85f), metrics);
            Assert.That(f1, Is.GreaterThanOrEqualTo(0.85f), metrics);
            Assert.That(medianError, Is.LessThanOrEqualTo(1f), metrics);
            Assert.That(highConfidenceFalsePositive, Is.Zero, metrics);
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

        private static PlayerFootMotionBakeData CreateDetectorFootData(float[] heights, float[] vertical = null, float[] horizontal = null)
        {
            return new PlayerFootMotionBakeData
            {
                SoleHeight = heights,
                VerticalSpeed = vertical ?? Constant(heights.Length, 0f),
                HorizontalSpeed = horizontal ?? Constant(heights.Length, 0f)
            };
        }

        private static float[] Constant(int count, float value)
        {
            return Enumerable.Repeat(value, count).ToArray();
        }

        private static PlayerFootMotionBakeData CopyFootData(PlayerFootMotionChannel channel)
        {
            return new PlayerFootMotionBakeData
            {
                SoleHeight = channel.SoleHeight.ToArray(),
                VerticalSpeed = channel.VerticalSpeed.ToArray(),
                HorizontalSpeed = channel.HorizontalSpeed.ToArray()
            };
        }

        private static bool TryResolveDetectionMode(string profileName, out PlayerFootPlantDetectionMode mode)
        {
            if (profileName.Contains("Loop")) { mode = PlayerFootPlantDetectionMode.Loop; return true; }
            if (profileName.Contains("Start")) { mode = PlayerFootPlantDetectionMode.Start; return true; }
            if (profileName.Contains("Stop")) { mode = PlayerFootPlantDetectionMode.Stop; return true; }
            if (profileName.Contains("Turn")) { mode = PlayerFootPlantDetectionMode.Turn; return true; }
            mode = default;
            return false;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
