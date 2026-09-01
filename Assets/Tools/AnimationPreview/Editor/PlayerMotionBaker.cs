using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectTools.AnimationPreview
{
    public static class PlayerMotionBaker
    {
        /// <summary>
        /// 使用模型和动画资源完成一次单 Profile Bake。
        /// </summary>
        public static PlayerMotionProfile Bake(GameObject modelAsset, AnimationClip clip, int sampleRate, PlayerMotionProfile target)
        {
            using AnimationPreviewSession session = new AnimationPreviewSession();
            if (!session.SetModel(modelAsset)) throw new System.InvalidOperationException(session.ModelError);
            string clipPath = AssetDatabase.GetAssetPath(clip);
            AnimationPreviewClipEntry entry = new AnimationPreviewClipEntry(clip, clipPath);
            if (!session.SetClip(entry)) throw new System.InvalidOperationException(session.CompatibilityMessage ?? "AnimationClip 与 Model/Avatar 不兼容。");
            Bake(session, sampleRate, target);
            return target;
        }

        internal static void Bake(AnimationPreviewSession session, int sampleRate, PlayerMotionProfile target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            PlayerFootCalibration calibration = FindCalibration(session.ModelAsset);
            if (calibration == null) throw new InvalidOperationException("当前模型缺少 PlayerFootCalibration，请先创建并配置左右 Foot 校准资源。");
            PlayerMotionBakePayload payload = Build(session, sampleRate, target.FootPlantDetectionMode, calibration);
            Apply(target, payload, target.PlantMarkerMode);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }

        /// <summary>
        /// 只采样并生成可暂存的 Bake Payload，不写入任何 Profile 资产。
        /// </summary>
        internal static PlayerMotionBakePayload Build(AnimationPreviewSession session, int sampleRate, PlayerFootPlantDetectionMode detectionMode, PlayerFootCalibration calibration)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (calibration == null) throw new InvalidOperationException("当前模型缺少 PlayerFootCalibration，请先创建并配置左右 Foot 校准资源。");
            session.SetFootCalibration(calibration);
            PlayerMotionBakeResult result = session.SampleMotion(sampleRate, calibration);
            AnimationClip clip = session.Clip;
            string clipPath = AssetDatabase.GetAssetPath(clip);
            string modelPath = AssetDatabase.GetAssetPath(session.ModelAsset);
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string clipGuid, out long clipLocalId)) throw new InvalidOperationException("无法解析源 AnimationClip 的 GUID 或 Local ID。");
            string modelGuid = AssetDatabase.AssetPathToGUID(modelPath);
            string calibrationPath = AssetDatabase.GetAssetPath(calibration);
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(calibration, out string calibrationGuid, out _)) throw new InvalidOperationException("无法解析 PlayerFootCalibration 的 GUID。");
            string calibrationHash = calibration.SettingsHash;
            string dependencyHash = ComputeDependencyHash(clipPath, modelPath, calibrationPath, calibrationHash);
            PlayerFootMotionBakeData leftFoot = SampleFootMotion(result.LeftFootPositions, result.Duration, calibration);
            PlayerFootMotionBakeData rightFoot = SampleFootMotion(result.RightFootPositions, result.Duration, calibration);
            List<PlayerFootPlantDetection> detections = PlayerFootPlantDetector.Detect(leftFoot, rightFoot, result.Duration, result.PlanarPosition.Length, detectionMode);
            List<PlayerFootPlantMarker> markers = detections.Select(detection => new PlayerFootPlantMarker(detection.Foot, detection.NormalizedTime, detection.Confidence)).ToList();
            return new PlayerMotionBakePayload(result.Duration, result.SampleRate, result.PlanarPosition, result.TravelDistance, result.Yaw, leftFoot, rightFoot, clipGuid, clipLocalId, modelGuid, dependencyHash, calibrationGuid, calibrationHash, detectionMode, markers);
        }

        /// <summary>
        /// 将 Payload 应用到 Profile 对象；调用方负责决定何时标脏和保存资产。
        /// </summary>
        internal static void Apply(PlayerMotionProfile target, PlayerMotionBakePayload payload, PlayerPlantMarkerMode markerMode)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            target.SetBakedData(payload.Duration, payload.SampleRate, payload.PlanarPosition, payload.TravelDistance, payload.Yaw, payload.SourceClipGuid, payload.SourceClipLocalId, payload.ModelGuid, payload.SourceDependencyHash, payload.LeftFoot, payload.RightFoot, payload.FootCalibrationGuid, payload.FootCalibrationHash);
            target.SetPlantAuthoringSettings(payload.DetectionMode, markerMode);
            if (markerMode == PlayerPlantMarkerMode.Auto) target.ReplacePlantMarkers(payload.AutoPlantMarkers, PlayerMotionProfile.CurrentFootPlantDetectionVersion);
        }

        public static bool Validate(PlayerMotionProfile profile, ICollection<string> messages)
        {
            return Validate(profile, messages, null);
        }

        public static bool Validate(PlayerMotionProfile profile, ICollection<string> errors, ICollection<string> warnings)
        {
            if (profile == null) { errors?.Add("未选择 PlayerMotionProfile。"); return false; }
            bool valid = profile.Validate(errors);
            valid &= ValidatePlantDetection(profile, errors, warnings);
            PlayerMotionProfileMetadata metadata = profile.EditorMetadata;
            if (metadata == null) { errors?.Add(profile.name + ": 缺少 Bake 元数据。"); return false; }
            string clipPath = AssetDatabase.GUIDToAssetPath(metadata.SourceClipGuid);
            string modelPath = AssetDatabase.GUIDToAssetPath(metadata.ModelGuid);
            if (metadata.BakeVersion != PlayerMotionProfile.CurrentBakeVersion) { errors?.Add(profile.name + ": Bake Version 已过期。"); valid = false; }
            if (metadata.SampleRate != profile.SampleRate) { errors?.Add(profile.name + ": Metadata SampleRate 与 Runtime Data 不一致，需要 Rebake。"); valid = false; }
            if (string.IsNullOrEmpty(clipPath) || string.IsNullOrEmpty(modelPath)) { errors?.Add(profile.name + ": Source Clip 或 Model 已丢失。"); return false; }
            if (profile.HasFootData)
            {
                string calibrationPath = AssetDatabase.GUIDToAssetPath(metadata.FootCalibrationGuid);
                PlayerFootCalibration calibration = string.IsNullOrEmpty(calibrationPath) ? null : AssetDatabase.LoadAssetAtPath<PlayerFootCalibration>(calibrationPath);
                if (calibration == null) { errors?.Add(profile.name + ": Foot Calibration 已丢失，需要重烘焙。"); return false; }
                if (metadata.FootCalibrationHash != calibration.SettingsHash) { errors?.Add(profile.name + ": Foot Calibration 已变化，需要重烘焙。"); valid = false; }
                string footCurrentHash = ComputeDependencyHash(clipPath, modelPath, calibrationPath, calibration.SettingsHash);
                if (footCurrentHash != metadata.SourceDependencyHash) { errors?.Add(profile.name + ": Source Clip/Avatar/Foot Calibration 已变化，需要 Rebake。"); valid = false; }
                return valid;
            }
            string currentHash = Hash128.Compute(AssetDatabase.GetAssetDependencyHash(clipPath) + ":" + AssetDatabase.GetAssetDependencyHash(modelPath)).ToString();
            if (currentHash != metadata.SourceDependencyHash) { errors?.Add(profile.name + ": Source Clip/Avatar 已变化，需要 Rebake。"); valid = false; }
            return valid;
        }
        /// <summary>
        /// 拿到模型足部数据
        /// </summary>
        public static PlayerFootCalibration FindCalibration(GameObject modelAsset)
        {
            if (modelAsset == null) return null;
            string modelPath = AssetDatabase.GetAssetPath(modelAsset);
            //搜索类型为PlayerFootCalibration的资源
            foreach (string guid in AssetDatabase.FindAssets("t:PlayerFootCalibration"))
            {
                PlayerFootCalibration calibration = AssetDatabase.LoadAssetAtPath<PlayerFootCalibration>(AssetDatabase.GUIDToAssetPath(guid));
                if (calibration == null) continue;
                if (calibration.ModelAsset == modelAsset) return calibration;
                if (calibration.ModelAsset != null && AssetDatabase.GetAssetPath(calibration.ModelAsset) == modelPath) return calibration;
            }
            return null;
        }

        public static AnimationClip ResolveSourceClip(PlayerMotionProfile profile)
        {
            PlayerMotionProfileMetadata metadata = profile?.EditorMetadata;
            if (metadata == null || string.IsNullOrEmpty(metadata.SourceClipGuid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(metadata.SourceClipGuid);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(clip =>
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out _, out long localId);
                return localId == metadata.SourceClipLocalId;
            });
        }

        internal static PlayerFootMotionBakeData SampleFootMotion(Vector3[] positions, int sampleRate, PlayerFootCalibration calibration)
        {
            if (positions == null || positions.Length < 2) throw new System.InvalidOperationException("脚底轨迹采样数量不足。");
            return SampleFootMotion(positions, (positions.Length - 1) / (float)Mathf.Max(1, sampleRate), calibration);
        }

        internal static PlayerFootMotionBakeData SampleFootMotion(Vector3[] positions, float duration, PlayerFootCalibration calibration)
        {
            if (positions == null || positions.Length < 2) throw new System.InvalidOperationException("脚底轨迹采样数量不足。");
            if (calibration == null) throw new System.ArgumentNullException(nameof(calibration));
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f) throw new System.ArgumentOutOfRangeException(nameof(duration));
            int count = positions.Length;
            float deltaTime = duration / (count - 1);
            float[] rawVertical = new float[count];
            float[] rawHorizontal = new float[count];
            //离地面高度
            float[] heights = new float[count];
            float[] vertical = new float[count];
            float[] horizontal = new float[count];
            //拿到一组原始数据
            for (int index = 0; index < count; index++)
            {
                //使用中央差分
                Vector3 previous = positions[Mathf.Max(0, index - 1)];
                Vector3 next = positions[Mathf.Min(count - 1, index + 1)];
                float divisor = index == 0 || index == count - 1 ? deltaTime : deltaTime * 2f;
                //速度
                Vector3 velocity = (next - previous) / Mathf.Max(0.0001f, divisor);
                rawVertical[index] = velocity.y;
                rawHorizontal[index] = new Vector2(velocity.x, velocity.z).magnitude;
                heights[index] = positions[index].y - calibration.VirtualGroundHeight;
            }
            //做了两层平滑，拿到可用的速度数据
            for (int index = 0; index < count; index++)
            {
                int from = Mathf.Max(0, index - 1);
                int to = Mathf.Min(count - 1, index + 1);
                //计算采样数量
                int samples = to - from + 1;
                for (int sample = from; sample <= to; sample++)
                {
                    vertical[index] += rawVertical[sample];
                    horizontal[index] += rawHorizontal[sample];
                }
                //得到每个采样点的平均速度
                vertical[index] /= samples;
                horizontal[index] /= samples;
            }
            return new PlayerFootMotionBakeData
            {
                SoleHeight = heights,
                VerticalSpeed = vertical,
                HorizontalSpeed = horizontal
            };
        }

        private static string ComputeDependencyHash(string clipPath, string modelPath, string calibrationPath, string calibrationHash)
        {
            return Hash128.Compute(AssetDatabase.GetAssetDependencyHash(clipPath) + ":" + AssetDatabase.GetAssetDependencyHash(modelPath) + ":" + AssetDatabase.GetAssetDependencyHash(calibrationPath) + ":" + calibrationHash).ToString();
        }

        private static bool ValidatePlantDetection(PlayerMotionProfile profile, ICollection<string> errors, ICollection<string> warnings)
        {
            if (profile.PlantMarkerMode != PlayerPlantMarkerMode.Auto) return true;
            bool valid = true;
            if (!Enum.IsDefined(typeof(PlayerFootPlantDetectionMode), profile.FootPlantDetectionMode)) { errors?.Add(profile.name + ": Foot Plant Detection Mode 无效。"); valid = false; }
            if (!profile.HasFootData) { errors?.Add(profile.name + ": Auto Plant Detection 缺少 Foot Channel，需要 Rebake。"); valid = false; }
            if (profile.FootPlantDetectionVersion != PlayerMotionProfile.CurrentFootPlantDetectionVersion) { errors?.Add(profile.name + ": Foot Plant Detection Version 已过期，需要 Rebake。"); valid = false; }
            if (profile.FootPlantDetectionMode == PlayerFootPlantDetectionMode.Loop) valid &= profile.ValidateLoopPhase(errors);
            else if (!profile.HasPlantMarkers) warnings?.Add(profile.name + ": Auto Plant Detection 未检测到 Plant Marker，请人工检查。");
            IReadOnlyList<PlayerFootPlantMarker> markers = profile.PlantMarkers;
            for (int index = 0; index < markers.Count; index++)
            {
                PlayerFootPlantMarker marker = markers[index];
                if (marker.Confidence < PlayerFootPlantDetector.LowConfidenceThreshold) warnings?.Add($"{profile.name}: {marker.Foot} Plant {marker.NormalizedTime:F3} 的 Confidence 为 {marker.Confidence:F2}，请人工检查。");
            }
            return valid;
        }
    }

    internal sealed class PlayerMotionBakePayload
    {
        public PlayerMotionBakePayload(float duration, int sampleRate, Vector2[] planarPosition, float[] travelDistance, float[] yaw, PlayerFootMotionBakeData leftFoot, PlayerFootMotionBakeData rightFoot, string sourceClipGuid, long sourceClipLocalId, string modelGuid, string sourceDependencyHash, string footCalibrationGuid, string footCalibrationHash, PlayerFootPlantDetectionMode detectionMode, List<PlayerFootPlantMarker> autoPlantMarkers)
        {
            Duration = duration;
            SampleRate = sampleRate;
            PlanarPosition = planarPosition;
            TravelDistance = travelDistance;
            Yaw = yaw;
            LeftFoot = leftFoot;
            RightFoot = rightFoot;
            SourceClipGuid = sourceClipGuid;
            SourceClipLocalId = sourceClipLocalId;
            ModelGuid = modelGuid;
            SourceDependencyHash = sourceDependencyHash;
            FootCalibrationGuid = footCalibrationGuid;
            FootCalibrationHash = footCalibrationHash;
            DetectionMode = detectionMode;
            AutoPlantMarkers = autoPlantMarkers;
        }

        public float Duration { get; }
        public int SampleRate { get; }
        public Vector2[] PlanarPosition { get; }
        public float[] TravelDistance { get; }
        public float[] Yaw { get; }
        public PlayerFootMotionBakeData LeftFoot { get; }
        public PlayerFootMotionBakeData RightFoot { get; }
        public string SourceClipGuid { get; }
        public long SourceClipLocalId { get; }
        public string ModelGuid { get; }
        public string SourceDependencyHash { get; }
        public string FootCalibrationGuid { get; }
        public string FootCalibrationHash { get; }
        public PlayerFootPlantDetectionMode DetectionMode { get; }
        public IReadOnlyList<PlayerFootPlantMarker> AutoPlantMarkers { get; }
    }

    internal class PlayerMotionBakeResult
    {
        public PlayerMotionBakeResult(float duration, int sampleRate, Vector2[] planarPosition, float[] travelDistance, float[] yaw, Vector3[] leftFootPositions, Vector3[] rightFootPositions)
        {
            Duration = duration;
            SampleRate = sampleRate;
            PlanarPosition = planarPosition;
            TravelDistance = travelDistance;
            Yaw = yaw;
            LeftFootPositions = leftFootPositions;
            RightFootPositions = rightFootPositions;
        }

        public float Duration { get; }
        public int SampleRate { get; }
        public Vector2[] PlanarPosition { get; }
        public float[] TravelDistance { get; }
        public float[] Yaw { get; }
        public Vector3[] LeftFootPositions { get; }
        public Vector3[] RightFootPositions { get; }
    }
}
