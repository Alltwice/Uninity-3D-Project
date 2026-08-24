using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectTools.AnimationPreview
{
    public static class PlayerMotionBaker
    {
        /// <summary>
        /// 预留的快速Bake接口，用于直接获取模型和动画，不直接执行bake
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
            if (target == null) throw new System.ArgumentNullException(nameof(target));
            PlayerFootCalibration calibration = FindCalibration(session.ModelAsset);
            if (calibration == null) throw new System.InvalidOperationException("当前模型缺少 PlayerFootCalibration，请先创建并配置左右 Foot 校准资源。");
            session.SetFootCalibration(calibration);
            PlayerMotionBakeResult result = session.SampleMotion(sampleRate, calibration);
            AnimationClip clip = session.Clip;
            string clipPath = AssetDatabase.GetAssetPath(clip);
            string modelPath = AssetDatabase.GetAssetPath(session.ModelAsset);
            //获取其身份，后者为子asset的id
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string clipGuid, out long clipLocalId);
            //获取模型身份
            string modelGuid = AssetDatabase.AssetPathToGUID(modelPath);
            string calibrationPath = AssetDatabase.GetAssetPath(calibration);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(calibration, out string calibrationGuid, out _);
            string calibrationHash = calibration.SettingsHash;
            string dependencyHash = ComputeDependencyHash(clipPath, modelPath, calibrationPath, calibrationHash);
            //写为永久SO，target已经是so文件
            PlayerFootMotionBakeData leftFoot = DetectFootMotion(result.LeftFootPositions, result.SampleRate, calibration);
            PlayerFootMotionBakeData rightFoot = DetectFootMotion(result.RightFootPositions, result.SampleRate, calibration);
            target.SetBakedData(result.Duration, result.SampleRate, result.PlanarPosition, result.TravelDistance, 
                result.Yaw, clipGuid, clipLocalId, modelGuid, dependencyHash, leftFoot, rightFoot, calibrationGuid, calibrationHash);
            //数据被更改
            EditorUtility.SetDirty(target);
            //修改先前被保存的so文件
            AssetDatabase.SaveAssetIfDirty(target);
        }

        public static bool Validate(PlayerMotionProfile profile, ICollection<string> messages)
        {
            if (profile == null) { messages?.Add("未选择 PlayerMotionProfile。"); return false; }
            bool valid = profile.Validate(messages);
            PlayerMotionProfileMetadata metadata = profile.EditorMetadata;
            if (metadata == null) { messages?.Add(profile.name + ": 缺少 Bake 元数据。"); return false; }
            string clipPath = AssetDatabase.GUIDToAssetPath(metadata.SourceClipGuid);
            string modelPath = AssetDatabase.GUIDToAssetPath(metadata.ModelGuid);
            if (metadata.BakeVersion != PlayerMotionProfile.CurrentBakeVersion) { messages?.Add(profile.name + ": Bake Version 已过期。"); valid = false; }
            if (metadata.SampleRate != profile.SampleRate) { messages?.Add(profile.name + ": Metadata SampleRate 与 Runtime Data 不一致，需要 Rebake。"); valid = false; }
            if (string.IsNullOrEmpty(clipPath) || string.IsNullOrEmpty(modelPath)) { messages?.Add(profile.name + ": Source Clip 或 Model 已丢失。"); return false; }
            if (profile.HasFootData)
            {
                string calibrationPath = AssetDatabase.GUIDToAssetPath(metadata.FootCalibrationGuid);
                PlayerFootCalibration calibration = string.IsNullOrEmpty(calibrationPath) ? null : AssetDatabase.LoadAssetAtPath<PlayerFootCalibration>(calibrationPath);
                if (calibration == null) { messages?.Add(profile.name + ": Foot Calibration 已丢失，需要重烘焙。"); return false; }
                if (metadata.FootCalibrationHash != calibration.SettingsHash) { messages?.Add(profile.name + ": Foot Calibration 已变化，需要重烘焙。"); valid = false; }
                string footCurrentHash = ComputeDependencyHash(clipPath, modelPath, calibrationPath, calibration.SettingsHash);
                if (footCurrentHash != metadata.SourceDependencyHash) { messages?.Add(profile.name + ": Source Clip/Avatar/Foot Calibration 已变化，需要 Rebake。"); valid = false; }
                return valid;
            }
            string currentHash = Hash128.Compute(AssetDatabase.GetAssetDependencyHash(clipPath) + ":" + AssetDatabase.GetAssetDependencyHash(modelPath)).ToString();
            if (currentHash != metadata.SourceDependencyHash) { messages?.Add(profile.name + ": Source Clip/Avatar 已变化，需要 Rebake。"); valid = false; }
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
            if (profile == null) return null;
            string path = AssetDatabase.GUIDToAssetPath(profile.EditorMetadata.SourceClipGuid);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(clip =>
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out _, out long localId);
                return localId == profile.EditorMetadata.SourceClipLocalId;
            });
        }

        internal static PlayerFootMotionBakeData DetectFootMotion(Vector3[] positions, int sampleRate, PlayerFootCalibration calibration)
        {
            if (positions == null || positions.Length < 2) throw new System.InvalidOperationException("脚底轨迹采样数量不足。");
            if (calibration == null) throw new System.ArgumentNullException(nameof(calibration));
            int count = positions.Length;
            //采集速率
            float deltaTime = 1f / Mathf.Max(1, sampleRate);
            float[] rawVertical = new float[count];
            float[] rawHorizontal = new float[count];
            //离地面高度
            float[] heights = new float[count];
            float[] vertical = new float[count];
            float[] horizontal = new float[count];
            float[] stable = new float[count];
            PlayerFootContactMarker[] markers = new PlayerFootContactMarker[count];
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
            bool contact = false;
            float stableTime = 0f;
            for (int index = 0; index < count; index++)
            {
                //判断接地条件
                bool candidate = heights[index] <= calibration.ContactHeightThreshold && Mathf.Abs(vertical[index]) <= calibration.VerticalSpeedThreshold && horizontal[index] <= calibration.HorizontalSpeedThreshold;
                bool plant = false;
                bool lift = false;
                //若没接地进行接地判断和接地时间判断
                if (!contact)
                {
                    stableTime = candidate ? stableTime + deltaTime : 0f;
                    if (candidate && stableTime >= calibration.StableTimeThreshold)
                    {
                        contact = true;
                        plant = true;
                    }
                }
                //不满足接地条件就再空中
                else if (heights[index] >= calibration.ReleaseHeightThreshold || Mathf.Abs(vertical[index]) > calibration.VerticalSpeedThreshold || horizontal[index] > calibration.HorizontalSpeedThreshold)
                {
                    contact = false;
                    stableTime = 0f;
                    lift = true;
                }
                stable[index] = stableTime;
                markers[index] = new PlayerFootContactMarker(contact, plant, lift);
            }
            return new PlayerFootMotionBakeData
            {
                SoleHeight = heights,
                VerticalSpeed = vertical,
                HorizontalSpeed = horizontal,
                StableTime = stable,
                AutoMarkers = markers
            };
        }

        private static string ComputeDependencyHash(string clipPath, string modelPath, string calibrationPath, string calibrationHash)
        {
            return Hash128.Compute(AssetDatabase.GetAssetDependencyHash(clipPath) + ":" + AssetDatabase.GetAssetDependencyHash(modelPath) + ":" + AssetDatabase.GetAssetDependencyHash(calibrationPath) + ":" + calibrationHash).ToString();
        }
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
