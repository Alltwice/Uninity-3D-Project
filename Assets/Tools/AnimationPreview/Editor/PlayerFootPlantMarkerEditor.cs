using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectTools.AnimationPreview
{
    /// <summary>
    /// 脚本中的脚步标注工作
    /// </summary>
    internal static class PlayerFootPlantMarkerEditor
    {
        internal struct MarkerValue
        {
            public MarkerValue(PlayerFoot foot, float normalizedTime, float confidence = 1f)
            {
                Foot = foot;
                NormalizedTime = normalizedTime;
                Confidence = confidence;
            }

            public PlayerFoot Foot;
            public float NormalizedTime;
            public float Confidence;
        }

        /// <summary>
        /// 拿到标注点遍历排序
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        internal static List<MarkerValue> Read(PlayerMotionProfile profile)
        {
            List<MarkerValue> values = new List<MarkerValue>();
            if (profile == null) return values;
            IReadOnlyList<PlayerFootPlantMarker> markers = profile.PlantMarkers;
            for (int index = 0; index < markers.Count; index++) values.Add(new MarkerValue(markers[index].Foot, NormalizeTime(markers[index].NormalizedTime), markers[index].Confidence));
            Sort(values);
            return values;
        }

        /// <summary>
        /// 检查动画数据和烘焙数据的id避免写入错误
        /// </summary>
        internal static bool IsProfileForClip(PlayerMotionProfile profile, AnimationClip clip)
        {
            if (profile == null || clip == null || profile.EditorMetadata == null) return false;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string clipGuid, out long clipLocalId)) return false;
            return profile.EditorMetadata.SourceClipGuid == clipGuid && profile.EditorMetadata.SourceClipLocalId == clipLocalId;
        }

        internal static bool TryAddForClip(PlayerMotionProfile profile, AnimationClip clip, PlayerFoot foot, float normalizedTime)
        {
            if (!IsProfileForClip(profile, clip)) return false;
            return TryAdd(profile, foot, normalizedTime);
        }

        /// <summary>拿到并读取Profile，找到其中节点标记，排序后写出 </summary>
        internal static bool TryAdd(PlayerMotionProfile profile, PlayerFoot foot, float normalizedTime)
        {
            if (profile == null || (foot != PlayerFoot.Left && foot != PlayerFoot.Right) || !IsFinite(normalizedTime)) return false;
            normalizedTime = Mathf.Clamp01(normalizedTime);
            List<MarkerValue> values = Read(profile);
            values.Add(new MarkerValue(foot, normalizedTime));
            Sort(values);
            Write(profile, values, "Add Plant Marker");
            return true;
        }

        /// <summary>
        /// 删除标记
        /// </summary>
        internal static bool TryRemoveNearestForClip(PlayerMotionProfile profile, AnimationClip clip, float normalizedTime, out PlayerFoot removedFoot)
        {
            removedFoot = PlayerFoot.Unknown;
            if (!IsProfileForClip(profile, clip)) return false;
            return TryRemoveNearest(profile, normalizedTime, out removedFoot);
        }

        internal static bool TryRemoveNearest(PlayerMotionProfile profile, float normalizedTime, out PlayerFoot removedFoot)
        {
            removedFoot = PlayerFoot.Unknown;
            if (profile == null || !IsFinite(normalizedTime)) return false;
            normalizedTime = Mathf.Clamp01(normalizedTime);
            List<MarkerValue> values = Read(profile);
            float interval = GetSampleInterval(profile);
            int nearestIndex = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < values.Count; index++)
            {
                float distance = Mathf.Abs(values[index].NormalizedTime - normalizedTime);
                if (distance <= interval && distance < nearestDistance)
                {
                    nearestIndex = index;
                    nearestDistance = distance;
                }
            }
            if (nearestIndex < 0) return false;
            removedFoot = values[nearestIndex].Foot;
            values.RemoveAt(nearestIndex);
            Sort(values);
            Write(profile, values, "Remove Plant Marker");
            return true;
        }

        internal static float GetSampleInterval(PlayerMotionProfile profile)
        {
            return profile != null && profile.SampleCount > 1 ? 1f / (profile.SampleCount - 1) : 0f;
        }

        private static void Write(PlayerMotionProfile profile, List<MarkerValue> values, string undoName)
        {
            List<PlayerFootPlantMarker> markers = new List<PlayerFootPlantMarker>(values.Count);
            for (int index = 0; index < values.Count; index++) markers.Add(new PlayerFootPlantMarker(values[index].Foot, values[index].NormalizedTime, values[index].Confidence));
            Undo.RecordObject(profile, undoName);
            profile.ReplacePlantMarkers(markers, 0);
            Save(profile);
        }

        /// <summary>
        /// 确保被标注的数据在一条正向的时间轴上
        /// </summary>
        private static void Sort(List<MarkerValue> values)
        {
            values.Sort((left, right) =>
            {
                int timeComparison = left.NormalizedTime.CompareTo(right.NormalizedTime);
                return timeComparison != 0 ? timeComparison : ((int)left.Foot).CompareTo((int)right.Foot);
            });
        }

        private static float NormalizeTime(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Save(PlayerMotionProfile profile)
        {
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }
    }
}
