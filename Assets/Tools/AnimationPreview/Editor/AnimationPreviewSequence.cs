using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectTools.AnimationPreview
{
    /// <summary>
    /// 一个可在预览窗口中顺序播放的动画条目。Source 保存原始来源，当前由 Clip 负责播放，后续可在不改变序列资产结构的前提下扩展为其他动画来源。
    /// </summary>
    [Serializable]
    public class AnimationPreviewSequenceEntry
    {
        [SerializeField, Tooltip("可选的原始动画来源，当前预览使用 Clip；保留它以便后续接入其他定义资产。")] private UnityEngine.Object source;
        [SerializeField] private AnimationClip clip;
        [SerializeField, Min(0f), Tooltip("该条目获得完整权重的时间点。前一条目有 BlendDuration 时，本条目会在此时间点之前开始参与混合。")] private float startTime;
        [SerializeField, Min(0f), Tooltip("条目时长。最后一个持续播放的条目可设为 Mathf.Infinity。")] private float duration = 1f;
        [SerializeField, Min(0f), Tooltip("在本条目结尾向下一条目交接的混合时长。")] private float blendDuration;

        public UnityEngine.Object Source => source;
        public AnimationClip Clip => clip;
        public float StartTime => startTime;
        public float Duration => duration;
        public float BlendDuration => blendDuration;

        public AnimationPreviewSequenceEntry()
        {
        }

        public AnimationPreviewSequenceEntry(UnityEngine.Object sourceAsset, AnimationClip animationClip, float entryStartTime, float entryDuration, float entryBlendDuration)
        {
            source = sourceAsset;
            clip = animationClip;
            startTime = entryStartTime;
            duration = entryDuration;
            blendDuration = entryBlendDuration;
        }
    }

    [CreateAssetMenu(fileName = "AnimationPreviewSequence", menuName = "Animation/Preview Sequence")]
    public class AnimationPreviewSequence : ScriptableObject
    {
        [SerializeField] private List<AnimationPreviewSequenceEntry> entries = new List<AnimationPreviewSequenceEntry>();

        public IReadOnlyList<AnimationPreviewSequenceEntry> Entries => entries;

        internal void SetEntries(IEnumerable<AnimationPreviewSequenceEntry> values)
        {
            entries.Clear();
            if (values != null) entries.AddRange(values);
        }
    }
}
