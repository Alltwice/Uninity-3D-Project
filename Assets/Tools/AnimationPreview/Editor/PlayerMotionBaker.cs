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
            PlayerMotionBakeResult result = session.SampleMotion(sampleRate);
            AnimationClip clip = session.Clip;
            string clipPath = AssetDatabase.GetAssetPath(clip);
            string modelPath = AssetDatabase.GetAssetPath(session.ModelAsset);
            //获取其身份，后者为子asset的id
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string clipGuid, out long clipLocalId);
            //获取模型身份
            string modelGuid = AssetDatabase.AssetPathToGUID(modelPath);
            //保存模型动画的hash码
            string dependencyHash = Hash128.Compute(AssetDatabase.GetAssetDependencyHash(clipPath) + ":" + AssetDatabase.GetAssetDependencyHash(modelPath)).ToString();
            //写为永久SO，target已经是so文件
            target.SetBakedData(result.Duration, result.SampleRate, result.PlanarPosition, result.TravelDistance, result.Yaw, clipGuid, clipLocalId, modelGuid, dependencyHash);
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
            string clipPath = AssetDatabase.GUIDToAssetPath(metadata.SourceClipGuid);
            string modelPath = AssetDatabase.GUIDToAssetPath(metadata.ModelGuid);
            if (metadata.BakeVersion != PlayerMotionProfile.CurrentBakeVersion) { messages?.Add(profile.name + ": Bake Version 已过期。"); valid = false; }
            if (metadata.SampleRate != profile.SampleRate) { messages?.Add(profile.name + ": Metadata SampleRate 与 Runtime Data 不一致，需要 Rebake。"); valid = false; }
            if (string.IsNullOrEmpty(clipPath) || string.IsNullOrEmpty(modelPath)) { messages?.Add(profile.name + ": Source Clip 或 Model 已丢失。"); return false; }
            string currentHash = Hash128.Compute(AssetDatabase.GetAssetDependencyHash(clipPath) + ":" + AssetDatabase.GetAssetDependencyHash(modelPath)).ToString();
            if (currentHash != metadata.SourceDependencyHash) { messages?.Add(profile.name + ": Source Clip/Avatar 已变化，需要 Rebake。"); valid = false; }
            return valid;
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
    }

    internal class PlayerMotionBakeResult
    {
        public PlayerMotionBakeResult(float duration, int sampleRate, Vector2[] planarPosition, float[] travelDistance, float[] yaw)
        {
            Duration = duration;
            SampleRate = sampleRate;
            PlanarPosition = planarPosition;
            TravelDistance = travelDistance;
            Yaw = yaw;
        }

        public float Duration { get; }
        public int SampleRate { get; }
        public Vector2[] PlanarPosition { get; }
        public float[] TravelDistance { get; }
        public float[] Yaw { get; }
    }
}
