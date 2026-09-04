using System;
using Animancer;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 将 Gameplay、Motion 和 Simulation 相位事实表现为 Pose；不生产运动或脚步相位
/// </summary>
public sealed class PlayerAnimationController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private PlayerAnimationSet animationSet;

    private AnimancerState boundaryState;
    private AnimancerState exitHandoffLoopState;
    private AnimancerState entrySourceLoopState;
    private AnimancerState stableLoopState;
    private AnimancerState hardLandingState;
    private PlayerMotionAnimationBinding activeBinding;
    private ulong presentedMotionInstanceId;
    private ulong presentationSequence;
    private Type gameplayStateType;

    public float DebugBoundaryPhase { get; private set; }

    private void Awake()
    {
        if (animancer == null) animancer = GetComponent<AnimancerComponent>();
    }

    public void InitializeManualEvaluation()
    {
        animancer.Graph.UpdateMode = DirectorUpdateMode.Manual;
    }

    public void Present(Type currentGameplayStateType, PlayerStateTransition? transition, PlayerMotionSnapshot motion, PlayerLocomotionPhaseSnapshot locomotionPhase, float stateProgress, PlayerLandingPresentationKey? landingPresentation)
    {
        gameplayStateType = currentGameplayStateType;
        bool newMotion = motion.ActiveDefinition != null && motion.InstanceId != presentedMotionInstanceId;
        bool motionCancelled = motion.JustCancelled && motion.InstanceId == presentedMotionInstanceId;
        if (newMotion) PlayMotion(motion, locomotionPhase);
        else if (motionCancelled) ClearBoundary();
        if (transition.HasValue && !newMotion) PlayStateTransition(transition.Value, locomotionPhase, landingPresentation);
        else if (!newMotion && !motionCancelled && motion.ActiveDefinition != null && motion.InstanceId == presentedMotionInstanceId) UpdateBoundaryMotion(motion, locomotionPhase);
        else if (!newMotion && !transition.HasValue && motionCancelled) PlayStableLoop(gameplayStateType, locomotionPhase);
        ApplyLoopPhase(locomotionPhase);
        if (gameplayStateType == typeof(PlayerHardLandingState) && hardLandingState != null)
        {
            hardLandingState.Speed = 0f;
            hardLandingState.NormalizedTime = stateProgress;
        }
    }

    public void EvaluateGraph(float deltaTime)
    {
        animancer.Evaluate(Mathf.Max(0f, deltaTime));
    }

    /// <summary>
    /// 边界 Motion 始终由 MotionSnapshot.Progress 手动采样
    /// </summary>
    private void PlayMotion(PlayerMotionSnapshot motion, PlayerLocomotionPhaseSnapshot locomotionPhase)
    {
        ++presentationSequence;
        presentedMotionInstanceId = motion.InstanceId;
        AnimancerState sourceLoop = motion.HasEntrySource && motion.EntryHandoffActive ? stableLoopState : null;
        ClearEntrySourceLoop();
        entrySourceLoopState = sourceLoop;
        boundaryState = null;
        exitHandoffLoopState = null;
        stableLoopState = null;
        activeBinding = null;
        if (animationSet == null || !animationSet.TryGetBinding(motion.ActiveDefinition, motion.ActiveProfile, out activeBinding, out ClipTransition transition))
        {
            ClearEntrySourceLoop();
            PlayStableLoop(gameplayStateType, locomotionPhase);
            return;
        }
        float fadeDuration = motion.EntryHandoffActive && motion.HasEntrySource ? 0f : transition.FadeDuration;
        boundaryState = animancer.Play(transition, fadeDuration, FadeMode.FixedDuration);
        boundaryState.Speed = 0f;
        boundaryState.IsPlaying = false;
        boundaryState.NormalizedTime = motion.Progress;
        DebugBoundaryPhase = motion.Progress;
        if (motion.EntryHandoffActive && motion.HasEntrySource) ApplyEntryHandoffPose(motion);
    }

    private void UpdateBoundaryMotion(PlayerMotionSnapshot motion, PlayerLocomotionPhaseSnapshot locomotionPhase)
    {
        if (boundaryState == null || activeBinding == null) return;
        boundaryState.Speed = 0f;
        boundaryState.IsPlaying = false;
        boundaryState.NormalizedTime = motion.Progress;
        DebugBoundaryPhase = motion.Progress;
        if (motion.HasEntrySource && !motion.EntryHandoffActive) ClearEntrySourceLoop();
        if (motion.EntryHandoffActive && motion.HasEntrySource)
        {
            ApplyEntryHandoffPose(motion);
            return;
        }
        boundaryState.Weight = 1f;
        if (motion.ExitHandoffActive || motion.JustCompleted)
        {
            EnsureExitHandoffLoop(locomotionPhase);
            float loopWeight = motion.JustCompleted ? 1f : activeBinding.EvaluateExitPoseWeight(motion.ExitHandoffProgress);
            boundaryState.Weight = 1f - loopWeight;
            if (exitHandoffLoopState != null) exitHandoffLoopState.Weight = loopWeight;
        }
        if (motion.JustCancelled && !motion.IsActive)
        {
            ClearBoundary();
            PlayStableLoop(gameplayStateType, locomotionPhase);
        }
        else if (motion.JustCompleted)
        {
            ClearBoundary(false);
        }
    }

    private void ApplyEntryHandoffPose(PlayerMotionSnapshot motion)
    {
        float targetPoseWeight = activeBinding.EvaluateEntryPoseWeight(motion.EntryHandoffProgress);
        if (entrySourceLoopState == null)
        {
            boundaryState.Weight = 1f;
            return;
        }
        entrySourceLoopState.Weight = 1f - targetPoseWeight;
        boundaryState.Weight = targetPoseWeight;
    }

    private void EnsureExitHandoffLoop(PlayerLocomotionPhaseSnapshot locomotionPhase)
    {
        if (exitHandoffLoopState != null) return;
        if (!TryResolveLoop(gameplayStateType, locomotionPhase, out PlayerAnimationSelection selection, out bool manualSampling)) return;
        exitHandoffLoopState = animancer.Play(selection.Transition);
        stableLoopState = exitHandoffLoopState;
        if (manualSampling) ApplyLoopSample(exitHandoffLoopState, locomotionPhase);
        boundaryState.Weight = 1f;
        exitHandoffLoopState.Weight = 0f;
    }

    private void PlayStateTransition(PlayerStateTransition transition, PlayerLocomotionPhaseSnapshot locomotionPhase, PlayerLandingPresentationKey? landingPresentation)
    {
        ++presentationSequence;
        ClearBoundary();
        if (transition.CurrentStateType == typeof(PlayerHardLandingState))
        {
            hardLandingState = null;
            if (animationSet == null || !animationSet.TryResolveLandingPresentation(PlayerLandingPresentationKey.HardLand, out ClipTransition hardLandingTransition))
            {
                PlayStableLoop(typeof(PlayerHardLandingState), locomotionPhase);
                return;
            }
            hardLandingState = animancer.Play(hardLandingTransition);
            hardLandingState.Speed = 0f;
            hardLandingState.NormalizedTime = 0f;
            return;
        }
        if ((transition.PreviousStateType == typeof(PlayerWalkState) && transition.CurrentStateType == typeof(PlayerRunState)) || (transition.PreviousStateType == typeof(PlayerRunState) && transition.CurrentStateType == typeof(PlayerWalkState)))
        {
            PlayStableLoopWithFade(transition.CurrentStateType, locomotionPhase);
            return;
        }
        if (transition.CurrentStateType == typeof(PlayerAirState))
        {
            if (transition.Reason == PlayerStateTransitionReason.Jumped && animationSet != null && animationSet.TryResolveCue(PlayerAnimationCue.JumpStart, out ClipTransition jumpStart)) PlayPresentationEdge(jumpStart, typeof(PlayerAirState), locomotionPhase, presentationSequence);
            else PlayStableLoop(typeof(PlayerAirState), locomotionPhase);
            return;
        }
        if (transition.PreviousStateType == typeof(PlayerAirState) && IsGroundState(transition.CurrentStateType) && landingPresentation.HasValue)
        {
            if (IsLandingMotion(landingPresentation.Value))
            {
                PlayStableLoop(transition.CurrentStateType, locomotionPhase);
                return;
            }
            if (animationSet != null && animationSet.TryResolveLandingPresentation(landingPresentation.Value, out ClipTransition landing))
            {
                PlayPresentationEdge(landing, transition.CurrentStateType, locomotionPhase, presentationSequence);
                return;
            }
            PlayStableLoop(transition.CurrentStateType, locomotionPhase);
            return;
        }
        PlayStableLoop(transition.CurrentStateType, locomotionPhase);
    }

    private void PlayPresentationEdge(ClipTransition edge, Type targetLoopStateType, PlayerLocomotionPhaseSnapshot locomotionPhase, ulong sequence)
    {
        if (edge == null || edge.Clip == null)
        {
            PlayStableLoop(targetLoopStateType, locomotionPhase);
            return;
        }
        AnimancerState state = animancer.Play(edge);
        state.Events(this).OnEnd = () =>
        {
            if (sequence == presentationSequence) PlayStableLoop(targetLoopStateType, locomotionPhase);
        };
    }

    private void PlayStableLoop(Type stateType, PlayerLocomotionPhaseSnapshot locomotionPhase)
    {
        if (!TryResolveLoop(stateType, locomotionPhase, out PlayerAnimationSelection selection, out bool manualSampling)) return;
        stableLoopState = animancer.Play(selection.Transition);
        if (manualSampling) ApplyLoopSample(stableLoopState, locomotionPhase);
    }

    private void PlayStableLoopWithFade(Type stateType, PlayerLocomotionPhaseSnapshot locomotionPhase)
    {
        if (!TryResolveLoop(stateType, locomotionPhase, out PlayerAnimationSelection selection, out bool manualSampling)) return;
        stableLoopState = animancer.Play(selection.Transition, selection.Transition.FadeDuration, FadeMode.FixedDuration);
        if (manualSampling) ApplyLoopSample(stableLoopState, locomotionPhase);
    }

    private bool TryResolveLoop(Type stateType, PlayerLocomotionPhaseSnapshot locomotionPhase, out PlayerAnimationSelection selection, out bool manualSampling)
    {
        PlayerLocomotionMode stateMode = ResolveLocomotionMode(stateType);
        //是否是受Simulation控制的Loop
        manualSampling = locomotionPhase.HasLoop && PlayerLocomotionCycleDefinition.IsGroundLoopMode(stateMode) && locomotionPhase.Mode == stateMode;
        //决定使用哪个mode查询动画
        PlayerLocomotionMode resolveMode = manualSampling ? locomotionPhase.Mode : stateMode;
        //决定使用哪个脚步动画
        PlayerFoot resolveFoot = manualSampling ? locomotionPhase.VariantFoot : PlayerFoot.Unknown;
        if (animationSet != null && animationSet.TryResolveLoop(resolveMode, resolveFoot, out selection)) return true;
        selection = default;
        return false;
    }

    private void ApplyLoopPhase(PlayerLocomotionPhaseSnapshot locomotionPhase)
    {
        if (!locomotionPhase.HasLoop) return;
        if (stableLoopState != null) ApplyLoopSample(stableLoopState, locomotionPhase);
        if (exitHandoffLoopState != null && exitHandoffLoopState != stableLoopState) ApplyLoopSample(exitHandoffLoopState, locomotionPhase);
        if (entrySourceLoopState != null && entrySourceLoopState != stableLoopState) ApplyLoopSample(entrySourceLoopState, locomotionPhase);
    }
    //从零状态开始播放，动画如何播放由外部数据提供，实际推进动画播放的位置
    private static void ApplyLoopSample(AnimancerState state, PlayerLocomotionPhaseSnapshot locomotionPhase)
    {
        state.Speed = 0f;
        state.IsPlaying = false;
        state.NormalizedTime = locomotionPhase.NormalizedTime;
    }
    /// <summary>
    /// 将状态转译为播放语义
    /// </summary>
    private static PlayerLocomotionMode ResolveLocomotionMode(Type stateType)
    {
        if (stateType == typeof(PlayerWalkState)) return PlayerLocomotionMode.Walk;
        if (stateType == typeof(PlayerRunState)) return PlayerLocomotionMode.Run;
        if (stateType == typeof(PlayerFastRunState)) return PlayerLocomotionMode.FastRun;
        if (stateType == typeof(PlayerAirState)) return PlayerLocomotionMode.Air;
        if (stateType == typeof(PlayerHardLandingState)) return PlayerLocomotionMode.HardLanding;
        return PlayerLocomotionMode.Idle;
    }

    private static bool IsGroundState(Type stateType)
    {
        return stateType == typeof(PlayerIdleState) || stateType == typeof(PlayerWalkState) || stateType == typeof(PlayerRunState) || stateType == typeof(PlayerFastRunState);
    }

    private static bool IsLandingMotion(PlayerLandingPresentationKey presentation)
    {
        return presentation == PlayerLandingPresentationKey.LandWalk || presentation == PlayerLandingPresentationKey.LandRun || presentation == PlayerLandingPresentationKey.LandRoll;
    }

    private void ClearBoundary(bool clearLoop = true)
    {
        boundaryState = null;
        activeBinding = null;
        DebugBoundaryPhase = 0f;
        ClearEntrySourceLoop();
        if (clearLoop)
        {
            exitHandoffLoopState = null;
            stableLoopState = null;
        }
    }

    private void ClearEntrySourceLoop()
    {
        if (entrySourceLoopState == null) return;
        AnimancerState sourceLoop = entrySourceLoopState;
        entrySourceLoopState = null;
        if (stableLoopState == sourceLoop) stableLoopState = null;
        if (exitHandoffLoopState == sourceLoop) exitHandoffLoopState = null;
        sourceLoop.Weight = 0f;
        sourceLoop.IsPlaying = false;
        sourceLoop.Destroy();
    }
}
