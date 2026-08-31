using System;
using Animancer;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 只把 Gameplay/Motion/Motor 事实表现为 Pose，并发布循环相位事实；不拥有 Gameplay 时间或运动权限
/// </summary>
public sealed class PlayerAnimationController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private PlayerAnimationSet animationSet;

    private AnimancerState boundaryState;
    private AnimancerState handoffLoopState;
    private AnimancerState stableLoopState;
    private AnimancerState hardLandingState;
    private PlayerMotionAnimationBinding activeBinding;
    private PlayerMotionProfile stableLoopProfile;
    private ulong presentedMotionInstanceId;
    private ulong presentationSequence;
    private Type gameplayStateType;
    private PlayerFoot currentLastPlantFoot;
    private PlayerLocomotionPhaseSnapshot phaseSnapshot;

    public float DebugBoundaryPhase { get; private set; }
    public PlayerLocomotionPhaseSnapshot PhaseSnapshot => phaseSnapshot;

    private void Awake()
    {
        if (animancer == null) animancer = GetComponent<AnimancerComponent>();
    }

    public void InitializeManualEvaluation()
    {
        animancer.Graph.UpdateMode = DirectorUpdateMode.Manual;
    }

    public void Present(Type currentGameplayStateType, PlayerStateTransition? transition, PlayerMotionSnapshot motion, float stateProgress, PlayerAnimationCue? landingCue)
    {
        gameplayStateType = currentGameplayStateType;
        if (motion.EntryLastPlantFoot != PlayerFoot.Unknown) currentLastPlantFoot = motion.EntryLastPlantFoot;
        UpdateMotionLastPlantFoot(motion);
        bool newMotion = motion.ActiveDefinition != null && motion.InstanceId != presentedMotionInstanceId;
        bool motionCancelled = motion.JustCancelled && motion.InstanceId == presentedMotionInstanceId;
        if (newMotion) PlayMotion(motion);
        else if (motionCancelled) ClearBoundary();
        if (transition.HasValue && !newMotion) PlayStateTransition(transition.Value, landingCue);
        else if (!newMotion && !motionCancelled && motion.ActiveDefinition != null && motion.InstanceId == presentedMotionInstanceId) UpdateBoundaryMotion(motion);
        else if (!newMotion && !transition.HasValue && motionCancelled) PlayStableLoop(gameplayStateType);
        if (gameplayStateType == typeof(PlayerHardLandingState) && hardLandingState != null)
        {
            hardLandingState.Speed = 0f;
            hardLandingState.NormalizedTime = stateProgress;
        }
    }

    public void EvaluateGraph(float deltaTime)
    {
        animancer.Evaluate(Mathf.Max(0f, deltaTime));
        RefreshPhaseSnapshot();
    }
    /// <summary>
    /// 处理烘焙动画播放
    /// </summary>
    private void PlayMotion(PlayerMotionSnapshot motion)
    {
        ++presentationSequence;
        presentedMotionInstanceId = motion.InstanceId;
        boundaryState = null;
        handoffLoopState = null;
        stableLoopState = null;
        stableLoopProfile = null;
        activeBinding = null;
        //没拿到烘焙动画数据就播放普通循环
        if (animationSet == null || !animationSet.TryGetBinding(motion.ActiveDefinition, motion.ActiveProfile, out activeBinding, out ClipTransition transition))
        {
            PlayStableLoop(gameplayStateType);
            return;
        }
        //拿到绑定播放动画，动画不自己播放，其播放进程绑定motionruntime
        boundaryState = animancer.Play(transition, transition.FadeDuration, FadeMode.FixedDuration);
        boundaryState.Speed = 0f;
        boundaryState.IsPlaying = false;
        //依据motion状态判断动画现在播放到哪
        float boundaryProgress = motion.Progress;
        boundaryState.NormalizedTime = boundaryProgress;
        DebugBoundaryPhase = boundaryProgress;
    }

    private void UpdateBoundaryMotion(PlayerMotionSnapshot motion)
    {
        if (boundaryState == null || activeBinding == null) return;
        boundaryState.Speed = 0f;
        boundaryState.IsPlaying = false;
        float boundaryProgress = motion.Progress;
        boundaryState.NormalizedTime = boundaryProgress;
        DebugBoundaryPhase = boundaryProgress;
        if (motion.HandoffActive || motion.JustCompleted)
        {
            EnsureHandoffLoop();
            //依据播放进程调整权重
            float loopWeight = motion.JustCompleted ? 1f : activeBinding.EvaluatePoseFade(motion.HandoffProgress);
            boundaryState.Weight = 1f - loopWeight;
            if (handoffLoopState != null) handoffLoopState.Weight = loopWeight;
        }
        if (motion.JustCancelled && !motion.IsActive)
        {
            ClearBoundary();
            PlayStableLoop(gameplayStateType);
        }
        else if (motion.JustCompleted)
        {
            ClearBoundary(false);
        }
    }
    /// <summary>
    /// 实际做出混合的逻辑
    /// </summary>
    private void EnsureHandoffLoop()
    {
        if (handoffLoopState != null) return;
        PlayerAnimationSelection selection = ResolveLoop(gameplayStateType);
        if (!selection.IsValid) return;
        //提前播放loop并调低其权重
        handoffLoopState = animancer.Play(selection.Transition);
        stableLoopState = handoffLoopState;
        stableLoopProfile = selection.Profile;
        boundaryState.Weight = 1f;
        handoffLoopState.Weight = 0f;
    }
    
    private void PlayStateTransition(PlayerStateTransition transition, PlayerAnimationCue? landingCue)
    {
        ++presentationSequence;
        ClearBoundary();
        if (transition.CurrentStateType == typeof(PlayerHardLandingState))
        {
            hardLandingState = null;
            if (animationSet == null || !animationSet.TryResolveCue(PlayerAnimationCue.HardLanding, out ClipTransition hardLandingTransition))
            {
                PlayStableLoop(typeof(PlayerHardLandingState));
                return;
            }
            hardLandingState = animancer.Play(hardLandingTransition);
            hardLandingState.Speed = 0f;
            hardLandingState.NormalizedTime = 0f;
            return;
        }
        if ((transition.PreviousStateType == typeof(PlayerWalkState) && transition.CurrentStateType == typeof(PlayerRunState)) || (transition.PreviousStateType == typeof(PlayerRunState) && transition.CurrentStateType == typeof(PlayerWalkState)))
        {
            PlayStableLoopWithFade(transition.CurrentStateType);
            return;
        }
        if (transition.CurrentStateType == typeof(PlayerAirState))
        {
            if (transition.Reason == PlayerStateTransitionReason.Jumped && animationSet != null && animationSet.TryResolveCue(PlayerAnimationCue.JumpStart, out ClipTransition jumpStart)) PlayPresentationEdge(jumpStart, typeof(PlayerAirState), presentationSequence);
            else PlayStableLoop(typeof(PlayerAirState));
            return;
        }
        if (transition.PreviousStateType == typeof(PlayerAirState) && IsGroundState(transition.CurrentStateType) && landingCue.HasValue && animationSet != null && animationSet.TryResolveCue(landingCue.Value, out ClipTransition landing))
        {
            PlayPresentationEdge(landing, transition.CurrentStateType, presentationSequence);
            return;
        }
        PlayStableLoop(transition.CurrentStateType);
    }
    /// <summary>
    /// 处理动画边
    /// </summary>
    private void PlayPresentationEdge(ClipTransition edge, Type targetLoopStateType, ulong sequence)
    {
        if (edge == null || edge.Clip == null)
        {
            PlayStableLoop(targetLoopStateType);
            return;
        }
        AnimancerState state = animancer.Play(edge);
        state.Events(this).OnEnd = () =>
        {
            if (sequence == presentationSequence) PlayStableLoop(targetLoopStateType);
        };
    }

    private void PlayStableLoop(Type stateType)
    {
        PlayerAnimationSelection selection = ResolveLoop(stateType);
        if (!selection.IsValid) return;
        stableLoopState = animancer.Play(selection.Transition);
        stableLoopProfile = selection.Profile;
    }

    private void PlayStableLoopWithFade(Type stateType)
    {
        PlayerAnimationSelection selection = ResolveLoop(stateType);
        if (!selection.IsValid) return;
        stableLoopState = animancer.Play(selection.Transition, selection.Transition.FadeDuration, FadeMode.FixedDuration);
        stableLoopProfile = selection.Profile;
    }

    private PlayerAnimationSelection ResolveLoop(Type stateType)
    {
        PlayerLocomotionMode locomotionMode = stateType == typeof(PlayerWalkState) ? PlayerLocomotionMode.Walk : stateType == typeof(PlayerRunState) ? PlayerLocomotionMode.Run : stateType == typeof(PlayerFastRunState) ? PlayerLocomotionMode.FastRun : stateType == typeof(PlayerAirState) ? PlayerLocomotionMode.Air : stateType == typeof(PlayerHardLandingState) ? PlayerLocomotionMode.HardLanding : PlayerLocomotionMode.Idle;
        if (animationSet != null && animationSet.TryResolveLoop(locomotionMode, currentLastPlantFoot, out PlayerAnimationSelection selection)) return selection;
        return default;
    }

    private static bool IsGroundState(Type stateType)
    {
        return stateType == typeof(PlayerIdleState) || stateType == typeof(PlayerWalkState) || stateType == typeof(PlayerRunState) || stateType == typeof(PlayerFastRunState);
    }
    /// <summary>处理motion驱动动画脚步选择</summary>
    private void UpdateMotionLastPlantFoot(PlayerMotionSnapshot motion)
    {
        if (motion.ActiveProfile == null) return;
        PlayerFoot fallback = motion.EntryLastPlantFoot == PlayerFoot.Unknown ? currentLastPlantFoot : motion.EntryLastPlantFoot;
        currentLastPlantFoot = motion.ActiveProfile.ResolveLastPlantFoot(motion.Progress, fallback);
    }
    /// <summary>
    /// 拿到脚步相位数据
    /// </summary>
    private void RefreshPhaseSnapshot()
    {
        if (stableLoopState == null)
        {
            phaseSnapshot = new PlayerLocomotionPhaseSnapshot(false, false, null, 0f, 0f, currentLastPlantFoot, PlayerFoot.Unknown, 0f, 0f);
            return;
        }
        float normalizedTime = Mathf.Repeat(stableLoopState.NormalizedTime, 1f);
        float effectiveSpeed = stableLoopState.EffectiveSpeed;
        if (stableLoopProfile != null && stableLoopProfile.TryEvaluateLoopPhase(normalizedTime, effectiveSpeed, out PlayerLocomotionPhaseSnapshot evaluatedSnapshot))
        {
            currentLastPlantFoot = evaluatedSnapshot.LastPlantFoot;
            phaseSnapshot = evaluatedSnapshot;
            return;
        }
        phaseSnapshot = new PlayerLocomotionPhaseSnapshot(true, false, stableLoopProfile, normalizedTime, effectiveSpeed, currentLastPlantFoot, PlayerFoot.Unknown, 0f, 0f);
    }

    private void ClearBoundary(bool clearLoop = true)
    {
        boundaryState = null;
        activeBinding = null;
        DebugBoundaryPhase = 0f;
        if (clearLoop)
        {
            handoffLoopState = null;
            stableLoopState = null;
            stableLoopProfile = null;
        }
    }
}
