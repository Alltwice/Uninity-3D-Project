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

    internal static class PlayerFootPlantDetector
    {
        internal const float EnterThreshold = 0.72f;
        internal const float ExitThreshold = 0.55f;
        internal const float LowConfidenceThreshold = 0.60f;
        private const float PercentileLow = 0.10f;
        private const float PercentileHigh = 0.90f;

        private class ScoreSeries
        {
            public float[] Values;
            public bool[] SwingEvidence;
            public float FeatureCoverage;
        }

        private struct ContactWindow
        {
            public int Start;
            public int End;
            public bool HadStableSwingBefore;
        }

        private class DetectionCandidate
        {
            public PlayerFoot Foot;
            public float NormalizedTime;
            public float Confidence;
        }

        internal static List<PlayerFootPlantDetection> Detect(PlayerFootMotionBakeData leftFoot, PlayerFootMotionBakeData rightFoot, float duration, int sampleCount, PlayerFootPlantDetectionMode mode)
        {
            ValidateInput(leftFoot, sampleCount, nameof(leftFoot));
            ValidateInput(rightFoot, sampleCount, nameof(rightFoot));
            if (!IsFinite(duration) || duration <= 0f) throw new ArgumentOutOfRangeException(nameof(duration));
            float deltaTime = duration / (sampleCount - 1);
            int stableSamples = Mathf.Max(2, Mathf.CeilToInt(0.03f / deltaTime));
            int gapSamples = Mathf.Max(1, Mathf.RoundToInt(0.02f / deltaTime));
            int uniqueCount = mode == PlayerFootPlantDetectionMode.Loop ? sampleCount - 1 : sampleCount;
            List<DetectionCandidate> candidates = new List<DetectionCandidate>();
            DetectFoot(leftFoot, PlayerFoot.Left, mode, uniqueCount, sampleCount, stableSamples, gapSamples, candidates);
            DetectFoot(rightFoot, PlayerFoot.Right, mode, uniqueCount, sampleCount, stableSamples, gapSamples, candidates);
            candidates.Sort(CompareCandidates);
            if (mode == PlayerFootPlantDetectionMode.Loop) EnforceLoopAlternation(candidates);
            List<PlayerFootPlantDetection> result = new List<PlayerFootPlantDetection>(candidates.Count);
            for (int index = 0; index < candidates.Count; index++) result.Add(new PlayerFootPlantDetection(candidates[index].Foot, candidates[index].NormalizedTime, candidates[index].Confidence));
            return result;
        }

        private static void DetectFoot(PlayerFootMotionBakeData data, PlayerFoot foot, PlayerFootPlantDetectionMode mode, int uniqueCount, int sampleCount, int stableSamples, int gapSamples, ICollection<DetectionCandidate> output)
        {
            bool turn = mode == PlayerFootPlantDetectionMode.Turn;
            float heightWeight = turn ? 0.55f : 0.50f;
            float verticalWeight = turn ? 0.35f : 0.30f;
            float horizontalWeight = turn ? 0.10f : 0.20f;
            ScoreSeries scores = BuildScores(data, uniqueCount, heightWeight, verticalWeight, horizontalWeight);
            if (scores.FeatureCoverage <= 0f) return;
            if (mode == PlayerFootPlantDetectionMode.Loop)
            {
                float[] repeated = new float[uniqueCount * 3];
                for (int index = 0; index < repeated.Length; index++) repeated[index] = scores.Values[index % uniqueCount];
                bool[] repeatedSwing = new bool[uniqueCount * 3];
                for (int index = 0; index < repeatedSwing.Length; index++) repeatedSwing[index] = scores.SwingEvidence[index % uniqueCount];
                List<ContactWindow> windows = FindContactWindows(repeated, repeatedSwing, stableSamples, gapSamples);
                for (int index = 0; index < windows.Count; index++)
                {
                    ContactWindow window = windows[index];
                    if (!window.HadStableSwingBefore || window.Start < uniqueCount || window.Start >= uniqueCount * 2) continue;
                    DetectionCandidate candidate = BuildCandidate(foot, repeated, window, scores.FeatureCoverage, stableSamples, uniqueCount, uniqueCount);
                    float halfSample = 0.5f / uniqueCount;
                    if (candidate.NormalizedTime <= 0f || candidate.NormalizedTime >= 1f)
                    {
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

        private static ScoreSeries BuildScores(PlayerFootMotionBakeData data, int count, float heightWeight, float verticalWeight, float horizontalWeight)
        {
            bool heightInformative = TryNormalize(data.SoleHeight, count, false, out float[] height);
            bool verticalInformative = TryNormalize(data.VerticalSpeed, count, true, out float[] vertical);
            bool horizontalInformative = TryNormalize(data.HorizontalSpeed, count, false, out float[] horizontal);
            float informativeWeight = (heightInformative ? heightWeight : 0f) + (verticalInformative ? verticalWeight : 0f) + (horizontalInformative ? horizontalWeight : 0f);
            float[] scores = new float[count];
            bool[] swingEvidence = new bool[count];
            if (informativeWeight > 0f)
            {
                for (int index = 0; index < count; index++)
                {
                    float penalty = (heightInformative ? height[index] * heightWeight : 0f) + (verticalInformative ? vertical[index] * verticalWeight : 0f) + (horizontalInformative ? horizontal[index] * horizontalWeight : 0f);
                    float score = 1f - Mathf.Clamp01(penalty / informativeWeight);
                    if (heightInformative) score = Mathf.Min(score, 1f - height[index]);
                    if (verticalInformative) score = Mathf.Min(score, 1f - vertical[index]);
                    if (horizontalInformative && horizontalWeight >= 0.20f) score = Mathf.Min(score, 1f - horizontal[index]);
                    scores[index] = score;
                    swingEvidence[index] = heightInformative ? height[index] >= 0.50f : score < ExitThreshold;
                }
            }
            return new ScoreSeries { Values = scores, SwingEvidence = swingEvidence, FeatureCoverage = informativeWeight / (heightWeight + verticalWeight + horizontalWeight) };
        }

        private static bool TryNormalize(float[] source, int count, bool absolute, out float[] normalized)
        {
            float[] values = new float[count];
            for (int index = 0; index < count; index++) values[index] = absolute ? Mathf.Abs(source[index]) : source[index];
            float[] sorted = (float[])values.Clone();
            Array.Sort(sorted);
            float low = Percentile(sorted, PercentileLow);
            float high = Percentile(sorted, PercentileHigh);
            float span = high - low;
            float numericalScale = Mathf.Max(1f, Mathf.Abs(low), Mathf.Abs(high));
            normalized = new float[count];
            if (span <= numericalScale * 0.00001f) return false;
            for (int index = 0; index < count; index++) normalized[index] = Mathf.Clamp01((values[index] - low) / span);
            return true;
        }

        private static float Percentile(float[] sorted, float percentile)
        {
            float position = (sorted.Length - 1) * percentile;
            int lower = Mathf.FloorToInt(position);
            int upper = Mathf.Min(lower + 1, sorted.Length - 1);
            return Mathf.LerpUnclamped(sorted[lower], sorted[upper], position - lower);
        }

        private static List<ContactWindow> FindContactWindows(float[] scores, bool[] swingEvidence, int stableSamples, int gapSamples)
        {
            List<ContactWindow> windows = new List<ContactWindow>();
            bool contact = false;
            int candidateStart = -1;
            int candidateLength = 0;
            int windowStart = -1;
            int lowStart = -1;
            int lowLength = 0;
            int swingSamples = 0;
            bool swingConfirmed = false;
            bool hadStableSwing = false;
            for (int index = 0; index < scores.Length; index++)
            {
                float score = scores[index];
                if (!contact)
                {
                    if (swingEvidence[index])
                    {
                        swingSamples++;
                        if (swingSamples >= stableSamples) swingConfirmed = true;
                    }
                    else if (!swingConfirmed) swingSamples = 0;
                    if (score >= EnterThreshold)
                    {
                        if (candidateStart < 0) candidateStart = index;
                        candidateLength++;
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
            if (contact) windows.Add(new ContactWindow { Start = windowStart, End = scores.Length - 1, HadStableSwingBefore = hadStableSwing });
            return windows;
        }

        private static int CountTrailingSwingSamples(bool[] swingEvidence, int index)
        {
            int count = 0;
            while (index >= 0 && swingEvidence[index]) { count++; index--; }
            return count;
        }

        private static DetectionCandidate BuildCandidate(PlayerFoot foot, float[] scores, ContactWindow window, float featureCoverage, int stableSamples, int denominator, int indexOffset)
        {
            float startPosition = window.Start;
            if (window.Start > 0)
            {
                float previous = scores[window.Start - 1];
                float current = scores[window.Start];
                if (previous < EnterThreshold && current > previous) startPosition = window.Start - 1f + (EnterThreshold - previous) / (current - previous);
            }
            float contactMean = Average(scores, window.Start, window.End);
            int contactLength = window.End - window.Start + 1;
            int swingFrom = Mathf.Max(0, window.Start - Mathf.Max(2, contactLength));
            float swingMean = window.Start > swingFrom ? Average(scores, swingFrom, window.Start - 1) : ExitThreshold;
            float separation = Mathf.Clamp01((contactMean - swingMean) / (1f - ExitThreshold));
            float entryMargin = Mathf.Clamp01((contactMean - EnterThreshold) / (1f - EnterThreshold));
            float stability = Mathf.Clamp01(contactLength / (stableSamples * 4f));
            float confidence = Mathf.Clamp01(separation * 0.40f + entryMargin * 0.30f + stability * 0.20f + featureCoverage * 0.10f);
            return new DetectionCandidate { Foot = foot, NormalizedTime = Mathf.Clamp01((startPosition - indexOffset) / denominator), Confidence = confidence };
        }

        private static float Average(float[] values, int from, int to)
        {
            float sum = 0f;
            for (int index = from; index <= to; index++) sum += values[index];
            return sum / (to - from + 1);
        }

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
