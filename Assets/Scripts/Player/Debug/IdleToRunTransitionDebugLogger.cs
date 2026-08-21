using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 在实验期间缓存逐帧样本和异常，30 次测试结束后统一生成 CSV，避免采集 I/O 干扰帧时序。
/// </summary>
public class IdleToRunTransitionDebugLogger : MonoBehaviour
{
    private struct AnomalyRecord
    {
        public string EventName;
        public IdleToRunTransitionDebugSample Sample;
    }

    private struct TestRecord
    {
        public int TestId;
        public float IdleWait;
        public float MoveDuration;
        public int MovementStalls;
        public int PoseStalls;
    }

    [Header("异常阈值")]
    [Range(0.01f, 0.95f)] [SerializeField] private float movementStallDropThreshold = 0.3f;
    [Range(0.01f, 0.5f)] [SerializeField] private float stableSpeedTolerance = 0.1f;
    [Range(0.001f, 0.25f)] [SerializeField] private float poseWeightTolerance = 0.05f;
    [Min(0f)] [SerializeField] private float minimumMeasuredSpeed = 0.25f;
    [Range(0.01f, 0.95f)] [SerializeField] private float terminalAuthoredDropThreshold = 0.3f;
    [Min(0f)] [SerializeField] private float frameTimeSpikeThresholdMs = 16.7f;
    [Min(0.01f)] [SerializeField] private float mixerPhaseJumpThreshold = 0.25f;

    private IdleToRunTransitionDebugSample previousSample;
    private bool hasPreviousSample;
    private bool previousPoseAnomaly;
    private bool sessionOpen;
    private int stableSpeedFrames;
    private int movementStallCount;
    private int poseStallCount;
    private int terminalAuthoredDropWithAuthorityCount;
    private int authorityZeroVelocityDropCount;
    private int completedBeforeGroundWeightCount;
    private int velocityUnstableAfterPoseTakeoverCount;
    private int frameTimeSpikeCount;
    private int mixerPhaseJumpCount;
    private int gcCollectionEventCount;
    private int observedStallMarkerCount;
    private int activeTestId;
    private int activeTestMovementStalls;
    private int activeTestPoseStalls;
    private int plannedTests;
    private int randomSeed;
    private float activeIdleWait;
    private float activeMoveDuration;
    private float totalSpeedDropPercent;
    private float maxUnscaledDeltaTimeMs;
    private float maxMainThreadTimeMs;
    private long maxGcAllocatedBytes;
    private readonly List<IdleToRunTransitionDebugSample> frames = new List<IdleToRunTransitionDebugSample>(50000);
    private readonly List<AnomalyRecord> anomalies = new List<AnomalyRecord>();
    private readonly List<TestRecord> tests = new List<TestRecord>(30);
    private readonly Queue<float> recentAuthoredDistances = new Queue<float>();
    private readonly Queue<float> recentAuthoredVelocities = new Queue<float>();

    public string LastOutputDirectory { get; private set; }
    public bool IsSessionOpen => sessionOpen;

    public void StartSession(int requestedTests, int seed)
    {
        FinishSession(0);
        string sessionName = "IdleToRunTransition_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        LastOutputDirectory = Path.Combine(Application.persistentDataPath, "IdleToRunTransitionDebug", sessionName);
        Directory.CreateDirectory(LastOutputDirectory);
        plannedTests = requestedTests;
        randomSeed = seed;
        ResetSessionCounters();
        sessionOpen = true;
    }

    public void BeginTest(int testId, float idleWait, float moveDuration)
    {
        activeTestId = testId;
        activeIdleWait = idleWait;
        activeMoveDuration = moveDuration;
        activeTestMovementStalls = 0;
        activeTestPoseStalls = 0;
        hasPreviousSample = false;
        previousPoseAnomaly = false;
        stableSpeedFrames = 0;
        recentAuthoredDistances.Clear();
        recentAuthoredVelocities.Clear();
    }

    public void Record(IdleToRunTransitionDebugSample sample)
    {
        if (!sessionOpen) return;
        EvaluateSpeed(sample, out bool movementStall, out float speedDropPercent);
        EvaluatePose(sample, out bool poseStall, out string poseDiagnostic);
        string diagnostic = EvaluateLayerDiagnosis(sample, movementStall, poseDiagnostic);
        sample.PotentialMovementStall = movementStall;
        sample.PoseWeightAnomaly = poseStall;
        sample.SpeedDropPercent = speedDropPercent;
        sample.Diagnostic = diagnostic;
        frames.Add(sample);
        if (movementStall) AddAnomaly("MovementStall", sample);
        if (poseStall) AddAnomaly("PoseStall", sample);
        if (sample.ObservedStallMarker) { observedStallMarkerCount++; AddAnomaly("ObservedStall", sample); }
        TrackTerminalAuthoredSample(sample);
        TrackSecondRoundSignals(sample);
        previousSample = sample;
        hasPreviousSample = true;
    }

    public void EndTest(int testId)
    {
        if (!sessionOpen || testId != activeTestId) return;
        tests.Add(new TestRecord { TestId = testId, IdleWait = activeIdleWait, MoveDuration = activeMoveDuration, MovementStalls = activeTestMovementStalls, PoseStalls = activeTestPoseStalls });
    }

    public void FinishSession(int completedTests)
    {
        if (!sessionOpen) return;
        sessionOpen = false;
        using (StreamWriter frameWriter = CreateWriter("frames.csv"))
        using (StreamWriter anomalyWriter = CreateWriter("anomalies.csv"))
        using (StreamWriter summaryWriter = CreateWriter("summary.csv"))
        {
            WriteFrameHeader(frameWriter);
            for (int i = 0; i < frames.Count; i++) WriteFrame(frameWriter, frames[i]);
            WriteAnomalyHeader(anomalyWriter);
            for (int i = 0; i < anomalies.Count; i++) WriteAnomaly(anomalyWriter, anomalies[i]);
            WriteSummary(summaryWriter, completedTests);
        }
        Debug.Log($"IdleToRun transition debug experiment finished. CSV: {LastOutputDirectory}", this);
    }

    private void EvaluateSpeed(IdleToRunTransitionDebugSample sample, out bool movementStall, out float speedDropPercent)
    {
        movementStall = false;
        speedDropPercent = 0f;
        if (!hasPreviousSample || previousSample.TestId != sample.TestId) return;
        float denominator = Mathf.Max(previousSample.ActualSpeed, 0.0001f);
        float relativeChange = Mathf.Abs(sample.ActualSpeed - previousSample.ActualSpeed) / denominator;
        stableSpeedFrames = previousSample.ActualSpeed >= minimumMeasuredSpeed && relativeChange <= stableSpeedTolerance ? stableSpeedFrames + 1 : 0;
        if (!sample.InTransitionWindow || previousSample.ActualSpeed < minimumMeasuredSpeed || sample.ActualSpeed >= previousSample.ActualSpeed * (1f - movementStallDropThreshold)) return;
        movementStall = true;
        speedDropPercent = (previousSample.ActualSpeed - sample.ActualSpeed) / denominator * 100f;
        movementStallCount++;
        activeTestMovementStalls++;
        totalSpeedDropPercent += speedDropPercent;
        if (sample.TranslationAuthority <= 0.001f) authorityZeroVelocityDropCount++;
    }

    private void EvaluatePose(IdleToRunTransitionDebugSample sample, out bool poseStall, out string diagnostic)
    {
        poseStall = false;
        diagnostic = string.Empty;
        if (!sample.InTransitionWindow || !hasPreviousSample || previousSample.TestId != sample.TestId) return;
        bool groundWeightRegressed = sample.GroundLocomotionWeight + poseWeightTolerance < previousSample.GroundLocomotionWeight;
        bool idleWeightIncreased = sample.IdleToRunWeight > previousSample.IdleToRunWeight + poseWeightTolerance;
        bool weightsDoNotComplement = sample.PoseTransitionProgress > 0f && Mathf.Abs(sample.IdleToRunWeight + sample.GroundLocomotionWeight - 1f) > poseWeightTolerance;
        bool completedBeforeGroundWeight = sample.MotionCompleted && sample.GroundLocomotionWeight < 1f - poseWeightTolerance;
        bool anomaly = groundWeightRegressed || idleWeightIncreased || weightsDoNotComplement || completedBeforeGroundWeight;
        if (completedBeforeGroundWeight) completedBeforeGroundWeightCount++;
        if (!anomaly) { previousPoseAnomaly = false; return; }
        if (stableSpeedFrames < 2 || previousPoseAnomaly) return;
        poseStall = true;
        previousPoseAnomaly = true;
        poseStallCount++;
        activeTestPoseStalls++;
        diagnostic = completedBeforeGroundWeight ? "MotionCompletedBeforeGroundWeight" : groundWeightRegressed ? "GroundWeightRegressed" : idleWeightIncreased ? "IdleToRunWeightIncreased" : "PoseWeightsDoNotComplement";
    }

    private string EvaluateLayerDiagnosis(IdleToRunTransitionDebugSample sample, bool movementStall, string poseDiagnostic)
    {
        string diagnostic = movementStall && sample.TranslationAuthority <= 0.001f ? "AuthorityZeroVelocityDrop" : string.Empty;
        if (!string.IsNullOrEmpty(poseDiagnostic)) diagnostic = AppendDiagnostic(diagnostic, poseDiagnostic);
        if (sample.InTransitionWindow && sample.GroundLocomotionWeight >= 1f - poseWeightTolerance && hasPreviousSample && previousSample.ActualSpeed >= minimumMeasuredSpeed)
        {
            float relativeSpeedChange = Mathf.Abs(sample.ActualSpeed - previousSample.ActualSpeed) / Mathf.Max(previousSample.ActualSpeed, 0.0001f);
            if (relativeSpeedChange > stableSpeedTolerance)
            {
                velocityUnstableAfterPoseTakeoverCount++;
                diagnostic = AppendDiagnostic(diagnostic, "VelocityUnstableAfterPoseTakeover");
            }
        }
        return diagnostic;
    }

    private void TrackTerminalAuthoredSample(IdleToRunTransitionDebugSample sample)
    {
        if (sample.MotionName != PlayerMotionId.IdleToRun.ToString()) return;
        float progressDelta = Mathf.Max(0f, sample.Progress - sample.PreviousProgress);
        float authoredVelocity = sample.DeltaTime > 0f ? sample.AuthoredDistance / sample.DeltaTime : 0f;
        bool terminal = sample.MotionCompleted || sample.Progress >= 0.9999f;
        if (!terminal && progressDelta > 0f)
        {
            EnqueueLimited(recentAuthoredDistances, sample.AuthoredDistance, 3);
            EnqueueLimited(recentAuthoredVelocities, authoredVelocity, 3);
            return;
        }
        if (sample.TranslationAuthority <= 0.001f || recentAuthoredDistances.Count == 0) return;
        float averageDistance = Average(recentAuthoredDistances);
        float averageVelocity = Average(recentAuthoredVelocities);
        bool distanceDrop = sample.AuthoredDistance < averageDistance * (1f - terminalAuthoredDropThreshold);
        bool velocityDrop = authoredVelocity < averageVelocity * (1f - terminalAuthoredDropThreshold);
        if (!distanceDrop || !velocityDrop) return;
        terminalAuthoredDropWithAuthorityCount++;
        sample.Diagnostic = "TerminalAuthoredDropWithAuthority";
        AddAnomaly("TerminalAuthoredDrop", sample);
    }

    private void TrackSecondRoundSignals(IdleToRunTransitionDebugSample sample)
    {
        float frameTimeMs = sample.UnscaledDeltaTime * 1000f;
        maxUnscaledDeltaTimeMs = Mathf.Max(maxUnscaledDeltaTimeMs, frameTimeMs);
        maxMainThreadTimeMs = Mathf.Max(maxMainThreadTimeMs, sample.MainThreadTimeMs);
        maxGcAllocatedBytes = Math.Max(maxGcAllocatedBytes, sample.GcAllocatedBytes);
        if (!hasPreviousSample || previousSample.TestId != sample.TestId) return;
        if (sample.InTransitionWindow && frameTimeMs >= frameTimeSpikeThresholdMs && sample.UnscaledDeltaTime > previousSample.UnscaledDeltaTime * 1.5f)
        {
            frameTimeSpikeCount++;
            IdleToRunTransitionDebugSample eventSample = sample;
            eventSample.Diagnostic = "FrameTimeSpike";
            AddAnomaly("FrameTimeSpike", eventSample);
        }
        if (sample.GcCollectionCount0 > previousSample.GcCollectionCount0 || sample.GcCollectionCount1 > previousSample.GcCollectionCount1 || sample.GcCollectionCount2 > previousSample.GcCollectionCount2)
        {
            gcCollectionEventCount++;
            IdleToRunTransitionDebugSample eventSample = sample;
            eventSample.Diagnostic = "GcCollection";
            AddAnomaly("GcCollection", eventSample);
        }
        bool child0Jump = HasMixerPhaseJump(previousSample.GroundChild0, sample.GroundChild0);
        bool child1Jump = HasMixerPhaseJump(previousSample.GroundChild1, sample.GroundChild1);
        if (!sample.InTransitionWindow || !child0Jump && !child1Jump) return;
        mixerPhaseJumpCount++;
        IdleToRunTransitionDebugSample phaseSample = sample;
        phaseSample.Diagnostic = child0Jump && child1Jump ? "GroundChild0And1PhaseJump" : child0Jump ? "GroundChild0PhaseJump" : "GroundChild1PhaseJump";
        AddAnomaly("MixerPhaseJump", phaseSample);
    }

    private bool HasMixerPhaseJump(IdleToRunMixerChildDebugSample previous, IdleToRunMixerChildDebugSample current)
    {
        if (!previous.Valid || !current.Valid || previous.Weight < poseWeightTolerance || current.Weight < poseWeightTolerance) return false;
        float delta = current.NormalizedTime - previous.NormalizedTime;
        return delta < -poseWeightTolerance || delta > mixerPhaseJumpThreshold;
    }

    private void AddAnomaly(string eventName, IdleToRunTransitionDebugSample sample)
    {
        anomalies.Add(new AnomalyRecord { EventName = eventName, Sample = sample });
    }

    private void WriteFrameHeader(StreamWriter writer)
    {
        writer.WriteLine("TestId,Frame,TestFrame,ObservedStallMarkerId,ObservedStallMarker,Time,UnscaledTime,MotionName,Progress,PreviousProgress,DeltaTime,AuthoredX,AuthoredY,AuthoredZ,AuthoredDistance,Authority,MotionCompleted,TranslationMode,CommandX,CommandY,CommandZ,CommandDistance,CommandVelocity,TargetVelocityX,TargetVelocityY,TargetVelocityZ,PredictedVelocityX,PredictedVelocityY,PredictedVelocityZ,ActualDisplacementX,ActualDisplacementY,ActualDisplacementZ,ActualVelocityX,ActualVelocityY,ActualVelocityZ,ActualSpeed,IdleToRunWeight,GroundLocomotionWeight,PoseTransitionProgress,BoundaryClipName,BoundaryNormalizedTime,BoundarySpeed,BoundaryEffectiveSpeed,BoundaryIsPlaying,GroundStateName,GroundNormalizedTime,GroundParameter,GroundSpeed,GroundEffectiveSpeed,GroundIsPlaying,GroundChildCount,GroundSynchronizedChildCount,GroundChild0Valid,GroundChild0ClipName,GroundChild0NormalizedTime,GroundChild0Weight,GroundChild0Speed,GroundChild0EffectiveSpeed,GroundChild0IsPlaying,GroundChild0IsSynchronized,GroundChild1Valid,GroundChild1ClipName,GroundChild1NormalizedTime,GroundChild1Weight,GroundChild1Speed,GroundChild1EffectiveSpeed,GroundChild1IsPlaying,GroundChild1IsSynchronized,AnimatorIsHuman,AnimatorLocalPositionX,AnimatorLocalPositionY,AnimatorLocalPositionZ,AnimatorLocalRotationX,AnimatorLocalRotationY,AnimatorLocalRotationZ,AnimatorLocalRotationW,HipsValid,HipsLocalPositionX,HipsLocalPositionY,HipsLocalPositionZ,HipsLocalRotationX,HipsLocalRotationY,HipsLocalRotationZ,HipsLocalRotationW,HipsLocalVelocityX,HipsLocalVelocityY,HipsLocalVelocityZ,HipsAngularSpeed,LeftFootValid,LeftFootLocalPositionX,LeftFootLocalPositionY,LeftFootLocalPositionZ,LeftFootLocalRotationX,LeftFootLocalRotationY,LeftFootLocalRotationZ,LeftFootLocalRotationW,LeftFootLocalVelocityX,LeftFootLocalVelocityY,LeftFootLocalVelocityZ,LeftFootAngularSpeed,RightFootValid,RightFootLocalPositionX,RightFootLocalPositionY,RightFootLocalPositionZ,RightFootLocalRotationX,RightFootLocalRotationY,RightFootLocalRotationZ,RightFootLocalRotationW,RightFootLocalVelocityX,RightFootLocalVelocityY,RightFootLocalVelocityZ,RightFootAngularSpeed,UnscaledDeltaTime,SmoothDeltaTime,MainThreadTimeMs,GcAllocatedBytes,GcCollectionCount0,GcCollectionCount1,GcCollectionCount2,PotentialMovementStall,PoseWeightAnomaly,SpeedDropPercent,Diagnostic");
    }

    private void WriteFrame(StreamWriter writer, IdleToRunTransitionDebugSample sample)
    {
        writer.WriteLine(string.Join(",", sample.TestId, sample.Frame, sample.TestFrame, sample.ObservedStallMarkerId, B(sample.ObservedStallMarker), F(sample.Time), F(sample.UnscaledTime), Csv(sample.MotionName), F(sample.Progress), F(sample.PreviousProgress), F(sample.DeltaTime), V(sample.AuthoredPlanarDisplacement), F(sample.AuthoredDistance), F(sample.TranslationAuthority), B(sample.MotionCompleted), sample.TranslationMode, V(sample.CommandDisplacement), F(sample.CommandDistance), F(sample.CommandVelocity), V(sample.TargetVelocity), V(sample.PredictedVelocity), V(sample.ActualPlanarDisplacement), V(sample.ActualVelocity), F(sample.ActualSpeed), F(sample.IdleToRunWeight), F(sample.GroundLocomotionWeight), F(sample.PoseTransitionProgress), Csv(sample.BoundaryClipName), F(sample.BoundaryNormalizedTime), F(sample.BoundarySpeed), F(sample.BoundaryEffectiveSpeed), B(sample.BoundaryIsPlaying), Csv(sample.GroundStateName), F(sample.GroundNormalizedTime), F(sample.GroundParameter), F(sample.GroundSpeed), F(sample.GroundEffectiveSpeed), B(sample.GroundIsPlaying), sample.GroundChildCount, sample.GroundSynchronizedChildCount, MixerChild(sample.GroundChild0), MixerChild(sample.GroundChild1), B(sample.AnimatorIsHuman), V(sample.AnimatorLocalPosition), Q(sample.AnimatorLocalRotation), Bone(sample.Hips), Bone(sample.LeftFoot), Bone(sample.RightFoot), F(sample.UnscaledDeltaTime), F(sample.SmoothDeltaTime), F(sample.MainThreadTimeMs), sample.GcAllocatedBytes, sample.GcCollectionCount0, sample.GcCollectionCount1, sample.GcCollectionCount2, B(sample.PotentialMovementStall), B(sample.PoseWeightAnomaly), F(sample.SpeedDropPercent), Csv(sample.Diagnostic)));
    }

    private void WriteAnomalyHeader(StreamWriter writer)
    {
        writer.WriteLine("Event,TestId,Frame,TestFrame,ObservedStallMarkerId,MotionProgress,Authority,TranslationMode,AuthoredDistance,CommandVelocity,PredictedVelocityX,PredictedVelocityY,PredictedVelocityZ,ActualVelocityX,ActualVelocityY,ActualVelocityZ,ActualSpeed,IdleToRunWeight,GroundLocomotionWeight,GroundNormalizedTime,GroundParameter,GroundChild0NormalizedTime,GroundChild0Weight,GroundChild0EffectiveSpeed,GroundChild0IsSynchronized,GroundChild1NormalizedTime,GroundChild1Weight,GroundChild1EffectiveSpeed,GroundChild1IsSynchronized,HipsLocalVelocityX,HipsLocalVelocityY,HipsLocalVelocityZ,HipsAngularSpeed,LeftFootLocalVelocityX,LeftFootLocalVelocityY,LeftFootLocalVelocityZ,LeftFootAngularSpeed,RightFootLocalVelocityX,RightFootLocalVelocityY,RightFootLocalVelocityZ,RightFootAngularSpeed,UnscaledDeltaTime,MainThreadTimeMs,GcAllocatedBytes,GcCollectionCount0,GcCollectionCount1,GcCollectionCount2,SpeedDropPercent,Diagnostic");
    }

    private void WriteAnomaly(StreamWriter writer, AnomalyRecord anomaly)
    {
        IdleToRunTransitionDebugSample sample = anomaly.Sample;
        writer.WriteLine(string.Join(",", anomaly.EventName, sample.TestId, sample.Frame, sample.TestFrame, sample.ObservedStallMarkerId, F(sample.Progress), F(sample.TranslationAuthority), sample.TranslationMode, F(sample.AuthoredDistance), F(sample.CommandVelocity), V(sample.PredictedVelocity), V(sample.ActualVelocity), F(sample.ActualSpeed), F(sample.IdleToRunWeight), F(sample.GroundLocomotionWeight), F(sample.GroundNormalizedTime), F(sample.GroundParameter), MixerChildAnomaly(sample.GroundChild0), MixerChildAnomaly(sample.GroundChild1), BoneVelocity(sample.Hips), BoneVelocity(sample.LeftFoot), BoneVelocity(sample.RightFoot), F(sample.UnscaledDeltaTime), F(sample.MainThreadTimeMs), sample.GcAllocatedBytes, sample.GcCollectionCount0, sample.GcCollectionCount1, sample.GcCollectionCount2, F(sample.SpeedDropPercent), Csv(sample.Diagnostic)));
    }

    private void WriteSummary(StreamWriter writer, int completedTests)
    {
        writer.WriteLine("Scope,TestId,Metric,Value,Conclusion");
        writer.WriteLine($"Session,0,Planned Tests,{plannedTests},");
        writer.WriteLine($"Session,0,Random Seed,{randomSeed},");
        writer.WriteLine($"Session,0,Buffered Frames,{frames.Count},{Csv("逐帧数据在实验期间仅存内存，结束后统一写盘。")}");
        for (int i = 0; i < tests.Count; i++)
        {
            TestRecord test = tests[i];
            writer.WriteLine($"Test,{test.TestId},Idle Wait Seconds,{F(test.IdleWait)},");
            writer.WriteLine($"Test,{test.TestId},Move Input Seconds,{F(test.MoveDuration)},");
            writer.WriteLine($"Test,{test.TestId},Movement Stall Count,{test.MovementStalls},");
            writer.WriteLine($"Test,{test.TestId},Pose Stall Count,{test.PoseStalls},");
        }
        float averageSpeedDrop = movementStallCount > 0 ? totalSpeedDropPercent / movementStallCount : 0f;
        writer.WriteLine($"Overall,0,Total Tests,{completedTests},");
        writer.WriteLine($"Overall,0,Movement Stall Count,{movementStallCount},");
        writer.WriteLine($"Overall,0,Pose Stall Count,{poseStallCount},");
        writer.WriteLine($"Overall,0,Average Speed Drop,{F(averageSpeedDrop)}%,");
        writer.WriteLine($"Observation,0,Observed Stall Marker Count,{observedStallMarkerCount},{Csv("看到卡顿时按配置的观察标记键；分析时回看标记帧之前约 0.5 秒，以覆盖人的反应延迟。")}");
        writer.WriteLine($"Performance,0,Frame Time Spike Count,{frameTimeSpikeCount},{Csv("只统计 handoff window 内超过阈值且较上一帧增长 50% 的帧。")}");
        writer.WriteLine($"Performance,0,Max Unscaled Delta Time Ms,{F(maxUnscaledDeltaTimeMs)},");
        writer.WriteLine($"Performance,0,Max Profiler Main Thread Time Ms,{F(maxMainThreadTimeMs)},{Csv("ProfilerRecorder 数值可能对应最近完成的 Profiler frame，应结合相邻帧分析。")}");
        writer.WriteLine($"Performance,0,Max GC Allocated Bytes In Frame,{maxGcAllocatedBytes},");
        writer.WriteLine($"Performance,0,GC Collection Event Count,{gcCollectionEventCount},");
        writer.WriteLine($"Animation,0,Mixer Child Phase Jump Count,{mixerPhaseJumpCount},{Csv("仅统计 handoff window 内仍有权重的 Ground mixer 子状态相位突跳。")}");
        writer.WriteLine($"Analysis,0,Terminal Authored Drop With Authority,{terminalAuthoredDropWithAuthorityCount},{Csv(terminalAuthoredDropWithAuthorityCount > 0 ? "AuthoredDistance 在 terminal frame 明显降低且 Authority > 0：handoff 没有覆盖 terminal frame。" : "未发现 terminal authored displacement 在仍有 Authority 时明显降低。")}");
        writer.WriteLine($"Analysis,0,Authority Zero Velocity Drop,{authorityZeroVelocityDropCount},{Csv(authorityZeroVelocityDropCount > 0 ? "Authority = 0 后 ActualVelocity 仍显著下降：检查 VelocityDriven 接管。" : "未发现 Authority = 0 同时 ActualVelocity 显著下降。")}");
        writer.WriteLine($"Analysis,0,Stable Velocity Pose Anomaly,{poseStallCount},{Csv(poseStallCount > 0 ? "ActualVelocity 稳定但顶层 Pose 权重异常：指向动画融合层。" : "未发现速度稳定时的顶层 Pose 权重异常。")}");
        writer.WriteLine($"Analysis,0,Motion Complete Before Ground Weight,{completedBeforeGroundWeightCount},{Csv("Motion 已完成但 GroundLocomotion Weight 未到 1 的帧数。")}");
        writer.WriteLine($"Analysis,0,Velocity Unstable After Pose Takeover,{velocityUnstableAfterPoseTakeoverCount},{Csv("GroundLocomotion 已占权但 Motion Velocity 未稳定的帧数。")}");
        if (movementStallCount == 0 && poseStallCount == 0) writer.WriteLine($"Analysis,0,No Top Level Stall,0,{Csv("顶层速度和权重稳定；请继续结合 mixer 子状态相位、骨骼姿态速度和帧性能字段判断。")}");
    }

    private StreamWriter CreateWriter(string fileName)
    {
        return new StreamWriter(Path.Combine(LastOutputDirectory, fileName), false, new UTF8Encoding(true));
    }

    private void ResetSessionCounters()
    {
        movementStallCount = 0;
        poseStallCount = 0;
        terminalAuthoredDropWithAuthorityCount = 0;
        authorityZeroVelocityDropCount = 0;
        completedBeforeGroundWeightCount = 0;
        velocityUnstableAfterPoseTakeoverCount = 0;
        frameTimeSpikeCount = 0;
        mixerPhaseJumpCount = 0;
        gcCollectionEventCount = 0;
        observedStallMarkerCount = 0;
        totalSpeedDropPercent = 0f;
        maxUnscaledDeltaTimeMs = 0f;
        maxMainThreadTimeMs = 0f;
        maxGcAllocatedBytes = 0L;
        activeTestId = 0;
        hasPreviousSample = false;
        previousPoseAnomaly = false;
        stableSpeedFrames = 0;
        frames.Clear();
        anomalies.Clear();
        tests.Clear();
        recentAuthoredDistances.Clear();
        recentAuthoredVelocities.Clear();
    }

    private static void EnqueueLimited(Queue<float> values, float value, int capacity)
    {
        values.Enqueue(value);
        while (values.Count > capacity) values.Dequeue();
    }

    private static float Average(Queue<float> values)
    {
        float sum = 0f;
        foreach (float value in values) sum += value;
        return values.Count == 0 ? 0f : sum / values.Count;
    }

    private static string AppendDiagnostic(string diagnostic, string value) => diagnostic.Length == 0 ? value : diagnostic + "|" + value;

    private static string MixerChild(IdleToRunMixerChildDebugSample sample) => string.Join(",", B(sample.Valid), Csv(sample.ClipName), F(sample.NormalizedTime), F(sample.Weight), F(sample.Speed), F(sample.EffectiveSpeed), B(sample.IsPlaying), B(sample.IsSynchronized));
    private static string MixerChildAnomaly(IdleToRunMixerChildDebugSample sample) => string.Join(",", F(sample.NormalizedTime), F(sample.Weight), F(sample.EffectiveSpeed), B(sample.IsSynchronized));
    private static string Bone(IdleToRunBoneDebugSample sample) => string.Join(",", B(sample.Valid), V(sample.LocalPosition), Q(sample.LocalRotation), V(sample.LocalVelocity), F(sample.AngularSpeed));
    private static string BoneVelocity(IdleToRunBoneDebugSample sample) => string.Join(",", V(sample.LocalVelocity), F(sample.AngularSpeed));
    private static string V(Vector3 value) => string.Join(",", F(value.x), F(value.y), F(value.z));
    private static string Q(Quaternion value) => string.Join(",", F(value.x), F(value.y), F(value.z), F(value.w));
    private static string F(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    private static string B(bool value) => value ? "1" : "0";
    private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

    private void OnDestroy()
    {
        FinishSession(activeTestId);
    }
}
