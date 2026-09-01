using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectTools.AnimationPreview
{
    /// <summary>
    /// 最终对外输出数据结构
    /// </summary>
    internal struct PlayerFootPlantDetection
    {
        public PlayerFootPlantDetection(PlayerFoot foot, float normalizedTime, float confidence)
        {
            Foot = foot;
            //记录时间点
            NormalizedTime = normalizedTime;
            //置信度
            Confidence = confidence;
        }

        public PlayerFoot Foot { get; }
        public float NormalizedTime { get; }
        public float Confidence { get; }
    }
    /// <summary>
    /// 自动标记点算法流程
    /// </summary>
    internal static class PlayerFootPlantDetector
    {
        //ContactScore达到0.72时进度接地候选
        internal const float EnterThreshold = 0.72f;
        //分数跌出0.55离开区间
        internal const float ExitThreshold = 0.55f;
        //低于0.60的值为不可信
        internal const float LowConfidenceThreshold = 0.60f;
        private const float PercentileLow = 0.10f;
        private const float PercentileHigh = 0.90f;
        /// <summary>
        /// 每帧处理数据
        /// </summary>
        private class ScoreSeries
        {
            //是否接触的一个连续区间，趋近与1时越像接触
            public float[] Values;
            //是否处于摆动
            public bool[] SwingEvidence;
            //有效值，由Height，Vertical，Horizontal共同提供
            public float FeatureCoverage;
        }
        /// <summary>
        /// 稳定接地时间，计算采样点而非时间
        /// </summary>
        private struct ContactWindow
        {
            public int Start;
            public int End;
            //确认在采样点前存在一段稳定的摆动
            public bool HadStableSwingBefore;
        }
        /// <summary>
        /// 被推导出的候选标记点
        /// </summary>
        private class DetectionCandidate
        {
            public PlayerFoot Foot;
            public float NormalizedTime;
            public float Confidence;
        }
        /// <summary>
        /// 自动检测落脚点的组织逻辑
        /// </summary>
        internal static List<PlayerFootPlantDetection> Detect(PlayerFootMotionBakeData leftFoot, PlayerFootMotionBakeData rightFoot, float duration, int sampleCount, PlayerFootPlantDetectionMode mode)
        {
            //数据校验
            ValidateInput(leftFoot, sampleCount, nameof(leftFoot));
            ValidateInput(rightFoot, sampleCount, nameof(rightFoot));
            if (!IsFinite(duration) || duration <= 0f) throw new ArgumentOutOfRangeException(nameof(duration));
            //两个采样点的实际相隔时间
            float deltaTime = duration / (sampleCount - 1);
            //满足状态稳定成立的最小采样点，最小设定为2（消除噪声）
            int stableSamples = Mathf.Max(2, Mathf.CeilToInt(0.03f / deltaTime));
            //允许出现多少个低分的采样点，最小设定为1（消除噪声）
            int gapSamples = Mathf.Max(1, Mathf.RoundToInt(0.02f / deltaTime));
            //如果是loop则去除开始和结束的重复采样点，如果不是则正常采样
            int uniqueCount = mode == PlayerFootPlantDetectionMode.Loop ? sampleCount - 1 : sampleCount;
            List<DetectionCandidate> candidates = new List<DetectionCandidate>();
            DetectFoot(leftFoot, PlayerFoot.Left, mode, uniqueCount, sampleCount, stableSamples, gapSamples, candidates);
            DetectFoot(rightFoot, PlayerFoot.Right, mode, uniqueCount, sampleCount, stableSamples, gapSamples, candidates);
            candidates.Sort(CompareCandidates);
            //将疑似点最后处理
            if (mode == PlayerFootPlantDetectionMode.Loop) EnforceLoopAlternation(candidates);
            List<PlayerFootPlantDetection> result = new List<PlayerFootPlantDetection>(candidates.Count);
            //加入最终数据
            for (int index = 0; index < candidates.Count; index++)
            {
                result.Add(new PlayerFootPlantDetection(candidates[index].Foot, candidates[index].NormalizedTime, candidates[index].Confidence));
            }
            return result;
        }

        private static void DetectFoot(PlayerFootMotionBakeData data, PlayerFoot foot, PlayerFootPlantDetectionMode mode, int uniqueCount, int sampleCount, int stableSamples, int gapSamples, ICollection<DetectionCandidate> output)
        {
            bool turn = mode == PlayerFootPlantDetectionMode.Turn;
            //turn动画独有一组判断数据
            float heightWeight = turn ? 0.55f : 0.50f;
            float verticalWeight = turn ? 0.35f : 0.30f;
            float horizontalWeight = turn ? 0.10f : 0.20f;
            ScoreSeries scores = BuildScores(data, uniqueCount, heightWeight, verticalWeight, horizontalWeight);
            if (scores.FeatureCoverage <= 0f) return;
            //Loop的头尾特殊处理
            if (mode == PlayerFootPlantDetectionMode.Loop)
            {
                //复制三份，第二圈刚好首尾相接，可处理重复点
                float[] repeated = new float[uniqueCount * 3];
                for (int index = 0; index < repeated.Length; index++)
                {
                    //循环索引填入
                    repeated[index] = scores.Values[index % uniqueCount];
                }
                bool[] repeatedSwing = new bool[uniqueCount * 3];
                for (int index = 0; index < repeatedSwing.Length; index++)
                {
                    repeatedSwing[index] = scores.SwingEvidence[index % uniqueCount];
                }
                List<ContactWindow> windows = FindContactWindows(repeated, repeatedSwing, stableSamples, gapSamples);
                for (int index = 0; index < windows.Count; index++)
                {
                    ContactWindow window = windows[index];
                    //排除掉接触前无稳定swing的，第一周期结尾前的，第二周期结尾后的，只拿到第二周期的窗口
                    if (!window.HadStableSwingBefore || window.Start < uniqueCount || window.Start >= uniqueCount * 2) continue;
                    DetectionCandidate candidate = BuildCandidate(foot, repeated, window, scores.FeatureCoverage, stableSamples, uniqueCount, uniqueCount);
                    //半个采样点对应的时间
                    float halfSample = 0.5f / uniqueCount;
                    if (candidate.NormalizedTime <= 0f || candidate.NormalizedTime >= 1f)
                    {
                        //不把采样点放在循环接缝上避免歧义
                        candidate.NormalizedTime = Mathf.Clamp(candidate.NormalizedTime, halfSample, 1f - halfSample);
                        candidate.Confidence *= 0.85f;
                    }
                    output.Add(candidate);
                }
                return;
            }
            List<ContactWindow> linearWindows = FindContactWindows(scores.Values, scores.SwingEvidence, stableSamples, gapSamples);
            int denominator = sampleCount - 1;
            for (int index = 0; index < linearWindows.Count; index++)
            {
                ContactWindow window = linearWindows[index];
                if (!window.HadStableSwingBefore) continue;
                output.Add(BuildCandidate(foot, scores.Values, window, scores.FeatureCoverage, stableSamples, denominator, 0));
            }
        }
        /// <summary>
        /// 利用原始采样数据判断摆动，接地的信任度
        /// </summary>
        private static ScoreSeries BuildScores(PlayerFootMotionBakeData data, int count, float heightWeight, float verticalWeight, float horizontalWeight)
        {
            bool heightInformative = TryNormalize(data.SoleHeight, count, false, out float[] height);
            bool verticalInformative = TryNormalize(data.VerticalSpeed, count, true, out float[] vertical);
            bool horizontalInformative = TryNormalize(data.HorizontalSpeed, count, false, out float[] horizontal);
            //拿到三者的特征权重综合用于计算加权结果
            float informativeWeight = (heightInformative ? heightWeight : 0f) + (verticalInformative ? verticalWeight : 0f) + (horizontalInformative ? horizontalWeight : 0f);
            float[] scores = new float[count];
            bool[] swingEvidence = new bool[count];
            if (informativeWeight > 0f)
            {
                for (int index = 0; index < count; index++)
                {
                    //拿到一组的惩罚值（越大越不像是接地）
                    float penalty = (heightInformative ? height[index] * heightWeight : 0f) + (verticalInformative ? vertical[index] * verticalWeight : 0f) + (horizontalInformative ? horizontal[index] * horizontalWeight : 0f);
                    //计算得分百分比情况，接近1越可能接地
                    float score = 1f - Mathf.Clamp01(penalty / informativeWeight);
                    //如果有一项表现差则直接拉低评分而不会加权后导致误判为接地的情况
                    if (heightInformative) score = Mathf.Min(score, 1f - height[index]);
                    if (verticalInformative) score = Mathf.Min(score, 1f - vertical[index]);
                    if (horizontalInformative && horizontalWeight >= 0.20f) score = Mathf.Min(score, 1f - horizontal[index]);
                    scores[index] = score;
                    swingEvidence[index] = heightInformative ? height[index] >= 0.50f : score < ExitThreshold;
                }
            }
            return new ScoreSeries { Values = scores, SwingEvidence = swingEvidence, FeatureCoverage = informativeWeight / (heightWeight + verticalWeight + horizontalWeight) };
        }
        /// <summary>
        /// 将不同数据归1化处理
        /// </summary>
        private static bool TryNormalize(float[] source, int count, bool absolute, out float[] normalized)
        {
            float[] values = new float[count];
            //按需取绝对值
            for (int index = 0; index < count; index++)
            {
                values[index] = absolute ? Mathf.Abs(source[index]) : source[index];
            }
            //克隆数据操作
            float[] sorted = (float[])values.Clone();
            Array.Sort(sorted);
            //拿到最高/低点的区间
            float low = Percentile(sorted, PercentileLow);
            float high = Percentile(sorted, PercentileHigh);
            //有效数据区间宽度
            float span = high - low;
            float numericalScale = Mathf.Max(1f, Mathf.Abs(low), Mathf.Abs(high));
            normalized = new float[count];
            //检查数据区间的变化程度是否小到可以忽略
            if (span <= numericalScale * 0.00001f) return false;
            //真正的归一化操作
            for (int index = 0; index < count; index++)
            {
                normalized[index] = Mathf.Clamp01((values[index] - low) / span);
            }
            return true;
        }
        /// <summary>
        /// 用排好序的数据找到x百分位的值
        /// </summary>
        private static float Percentile(float[] sorted, float percentile)
        {
            float position = (sorted.Length - 1) * percentile;
            //向下取整数并转为int
            int lower = Mathf.FloorToInt(position);
            //拿到lower的上一位
            int upper = Mathf.Min(lower + 1, sorted.Length - 1);
            //Mathf.LerpUnclamped在AB之间做线性插值，在这里意味着精确的找到两点间数值正确的值
            return Mathf.LerpUnclamped(sorted[lower], sorted[upper], position - lower);
        }
        /// <summary>
        /// 寻找一整段的稳定区间
        /// </summary>
        private static List<ContactWindow> FindContactWindows(float[] scores, bool[] swingEvidence, int stableSamples, int gapSamples)
        {
            //所有的接地区间
            List<ContactWindow> windows = new List<ContactWindow>();
            //接触状态
            bool contact = false;
            //疑似接触窗口的起始帧
            int candidateStart = -1;
            //接触窗口的长度
            int candidateLength = 0;
            //确认了开始帧，会回溯到疑似开始帧开始计算
            int windowStart = -1;
            //判断退出帧
            int lowStart = -1;
            int lowLength = 0;
            //多少帧在摆动
            int swingSamples = 0;
            //确认稳定摆动
            bool swingConfirmed = false;
            //最终事实
            bool hadStableSwing = false;
            for (int index = 0; index < scores.Length; index++)
            {
                float score = scores[index];
                if (!contact)
                {
                    //如果摆动确认足够久则记录正在摆动
                    if (swingEvidence[index])
                    {
                        swingSamples++;
                        if (swingSamples >= stableSamples)
                        {
                            swingConfirmed = true;
                        }
                    }
                    else if (!swingConfirmed) swingSamples = 0;
                    if (score >= EnterThreshold)
                    {
                        //同样记录疑似开始的时间
                        if (candidateStart < 0)
                        {
                            candidateStart = index;
                        }
                        candidateLength++;
                        //确认开始接地
                        if (candidateLength >= stableSamples)
                        {
                            contact = true;
                            windowStart = candidateStart;
                            hadStableSwing = swingConfirmed;
                            swingConfirmed = false;
                            swingSamples = 0;
                            candidateStart = -1;
                            candidateLength = 0;
                            lowStart = -1;
                            lowLength = 0;
                        }
                    }
                    else
                    {
                        candidateStart = -1;
                        candidateLength = 0;
                    }
                    continue;
                }
                //记录退出时间
                if (score < ExitThreshold)
                {
                    if (lowStart < 0) lowStart = index;
                    lowLength++;
                    if (lowLength > gapSamples)
                    {
                        windows.Add(new ContactWindow { Start = windowStart, End = lowStart - 1, HadStableSwingBefore = hadStableSwing });
                        contact = false;
                        swingSamples = CountTrailingSwingSamples(swingEvidence, index);
                        swingConfirmed = swingSamples >= stableSamples;
                        windowStart = -1;
                        lowStart = -1;
                        lowLength = 0;
                    }
                }
                else
                {
                    lowStart = -1;
                    lowLength = 0;
                }
            }
            //最终确认接地则添加接地窗口
            if (contact) windows.Add(new ContactWindow { Start = windowStart, End = scores.Length - 1, HadStableSwingBefore = hadStableSwing });
            return windows;
        }

        private static int CountTrailingSwingSamples(bool[] swingEvidence, int index)
        {
            int count = 0;
            while (index >= 0 && swingEvidence[index]) { count++; index--; }
            return count;
        }
        /// <summary>
        /// 置信度计算，标记疑似落脚点
        /// </summary>
        private static DetectionCandidate BuildCandidate(PlayerFoot foot, float[] scores, ContactWindow window, float featureCoverage, int stableSamples, int denominator, int indexOffset)
        {
            float startPosition = window.Start;
            if (window.Start > 0)
            {
                float previous = scores[window.Start - 1];
                float current = scores[window.Start];
                if (previous < EnterThreshold && current > previous) startPosition = window.Start - 1f + (EnterThreshold - previous) / (current - previous);
            }
            //整个窗口中的接地分数平均值
            float contactMean = Average(scores, window.Start, window.End);
            int contactLength = window.End - window.Start + 1;
            int swingFrom = Mathf.Max(0, window.Start - Mathf.Max(2, contactLength));
            //contact前的接地分平均值，用于判断摆动
            float swingMean = window.Start > swingFrom ? Average(scores, swingFrom, window.Start - 1) : ExitThreshold;
            //接触区域和swing区域的区分度
            float separation = Mathf.Clamp01((contactMean - swingMean) / (1f - ExitThreshold));
            //平均分较阈值高度
            float entryMargin = Mathf.Clamp01((contactMean - EnterThreshold) / (1f - EnterThreshold));
            //接地的持续长度换位0~1的稳定评分
            float stability = Mathf.Clamp01(contactLength / (stableSamples * 4f));
            //依据摆动和接地差距加权分数，平均分较阈值高度加权分数，以及接地稳定度的加权分数
            float confidence = Mathf.Clamp01(separation * 0.40f + entryMargin * 0.30f + stability * 0.20f + featureCoverage * 0.10f);
            return new DetectionCandidate { Foot = foot, NormalizedTime = Mathf.Clamp01((startPosition - indexOffset) / denominator), Confidence = confidence };
        }

        private static float Average(float[] values, int from, int to)
        {
            float sum = 0f;
            for (int index = from; index <= to; index++) sum += values[index];
            return sum / (to - from + 1);
        }
        /// <summary>
        /// 当动画资源是Loop时尽量保证左右脚交替
        /// </summary>
        private static void EnforceLoopAlternation(List<DetectionCandidate> candidates)
        {
            bool changed = true;
            while (changed && candidates.Count > 1)
            {
                changed = false;
                for (int index = 0; index < candidates.Count; index++)
                {
                    int nextIndex = (index + 1) % candidates.Count;
                    if (candidates[index].Foot != candidates[nextIndex].Foot) continue;
                    int removeIndex = candidates[index].Confidence < candidates[nextIndex].Confidence ? index : nextIndex;
                    int keepIndex = removeIndex == index ? nextIndex : index;
                    candidates[keepIndex].Confidence *= 0.85f;
                    candidates.RemoveAt(removeIndex);
                    changed = true;
                    break;
                }
            }
        }

        private static int CompareCandidates(DetectionCandidate left, DetectionCandidate right)
        {
            int timeComparison = left.NormalizedTime.CompareTo(right.NormalizedTime);
            return timeComparison != 0 ? timeComparison : ((int)left.Foot).CompareTo((int)right.Foot);
        }

        private static void ValidateInput(PlayerFootMotionBakeData data, int sampleCount, string parameterName)
        {
            if (data == null) throw new ArgumentNullException(parameterName);
            if (sampleCount < 2) throw new ArgumentOutOfRangeException(nameof(sampleCount));
            if (data.SoleHeight == null || data.VerticalSpeed == null || data.HorizontalSpeed == null || data.SoleHeight.Length != sampleCount || data.VerticalSpeed.Length != sampleCount || data.HorizontalSpeed.Length != sampleCount) throw new ArgumentException("Foot Channel 数量与 Motion SampleCount 不一致。", parameterName);
            for (int index = 0; index < sampleCount; index++)
            {
                if (!IsFinite(data.SoleHeight[index]) || !IsFinite(data.VerticalSpeed[index]) || !IsFinite(data.HorizontalSpeed[index])) throw new ArgumentException("Foot Channel 包含无效数值。", parameterName);
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
