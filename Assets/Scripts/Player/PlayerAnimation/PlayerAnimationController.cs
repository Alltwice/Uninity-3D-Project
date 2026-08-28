using System;
using Animancer;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 只把 Gameplay/Motion/Motor 事实表现为 Pose；不拥有 Gameplay 时间或运动权限
/// </summary>
public sealed class PlayerAnimationController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private PlayerAnimationSet animationSet;

    [Header("稳定循环")]
    [SerializeField] private ClipTransition idleLoopTransition = new ClipTransition();
    [SerializeField] private ClipTransition walkLoopTransition = new ClipTransition();
    [SerializeField] private ClipTransition runLoopTransition = new ClipTransition();
    [SerializeField] private ClipTransition fastRunLoopTransition = new ClipTransition();
    [SerializeField] private ClipTransition airLoopTransition = new ClipTransition();

    [Header("非 Motion 表现")]
    [SerializeField] private ClipTransition jumpStartTransition = new ClipTransition();
    [SerializeField] private ClipTransition landingTransition = new ClipTransition();
    [SerializeField] private ClipTransition hardLandingTransition = new ClipTransition();

    private AnimancerState boundaryState;
    private AnimancerState handoffLoopState;
    private AnimancerState stableLoopState;
    private AnimancerState hardLandingState;
    private PlayerMotionAnimationBinding activeBinding;
    private PlayerMotionProfile stableLoopProfile;
    private ulong presentedMotionInstanceId;
    private ulong presentationSequence;
    private Type gameplayStateType;
    private PlayerFoot currentSupportFoot;

    public float DebugBoundaryPhase { get; private set; }
    public PlayerFoot CurrentSupportFoot => currentSupportFoot;

    private void Awake()
    {
        if (animancer == null) animancer = GetComponent<AnimancerComponent>();
    }

    public void InitializeManualEvaluation()
    {
        animancer.Graph.UpdateMode = DirectorUpdateMode.Manual;
    }

    public void Present(Type currentGameplayStateType, PlayerStateTransition? transition, PlayerMotionSnapshot motion, float stateProgress)
    {
        gameplayStateType = currentGameplayStateType;
        if (motion.SupportFoot != PlayerFoot.Unknown) currentSupportFoot = motion.SupportFoot;
        UpdateMotionSupportFoot(motion);
        bool newMotion = motion.ActiveDefinition != null && motion.InstanceId != presentedMotionInstanceId;
        bool motionCancelled = motion.JustCancelled && motion.InstanceId == presentedMotionInstanceId;
        if (newMotion) PlayMotion(motion);
        else if (motionCancelled) ClearBoundary();
        if (transition.HasValue && !newMotion) PlayStateTransition(transition.Value);
        else if (!newMotion && !motionCancelled && motion.ActiveDefinition != null && motion.InstanceId == presentedMotionInstanceId) UpdateBoundaryMotion(motion);
        else if (!newMotion && !transition.HasValue && motionCancelled) PlayStableLoop(gameplayStateType);
        if (gameplayStateType == typeof(PlayerHardLandingState) && hardLandingState != null)
        {
            hardLandingState.Speed = 0f;
            hardLandingState.NormalizedTime = stateProgress;
        }
        UpdateLoopSupportFoot();
    }

    public void EvaluateGraph(float deltaTime)
    {
        animancer.Evaluate(Mathf.Max(0f, deltaTime));
        UpdateLoopSupportFoot();
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
        if (!animationSet.TryGetBinding(motion.ActiveDefinition, motion.ActiveProfile, out activeBinding, out ClipTransition transition))
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
        UpdateLoopSupportFoot();
        boundaryState.Weight = 1f;
        handoffLoopState.Weight = 0f;
    }
    
    private void PlayStateTransition(PlayerStateTransition transition)
    {
        ++presentationSequence;
        ClearBoundary();
        if (transition.CurrentStateType == typeof(PlayerHardLandingState))
        {
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
            if (transition.Reason == PlayerStateTransitionReason.Jumped) PlayPresentationEdge(jumpStartTransition, typeof(PlayerAirState), presentationSequence);
            else PlayStableLoop(typeof(PlayerAirState));
            return;
        }
        if (transition.PreviousStateType == typeof(PlayerAirState) && transition.CurrentStateType == typeof(PlayerIdleState))
        {
            PlayPresentationEdge(landingTransition, typeof(PlayerIdleState), presentationSequence);
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
        edge.Events.OnEnd = () =>
        {
            if (sequence == presentationSequence) PlayStableLoop(targetLoopStateType);
        };
        animancer.Play(edge);
    }

    private void PlayStableLoop(Type stateType)
    {
        PlayerAnimationSelection selection = ResolveLoop(stateType);
        if (!selection.IsValid) return;
        stableLoopState = animancer.Play(selection.Transition);
        stableLoopProfile = selection.Profile;
        UpdateLoopSupportFoot();
    }

    private void PlayStableLoopWithFade(Type stateType)
    {
        PlayerAnimationSelection selection = ResolveLoop(stateType);
        if (!selection.IsValid) return;
        stableLoopState = animancer.Play(selection.Transition, selection.Transition.FadeDuration, FadeMode.FixedDuration);
        stableLoopProfile = selection.Profile;
        UpdateLoopSupportFoot();
    }

    private PlayerAnimationSelection ResolveLoop(Type stateType)
    {
        PlayerLocomotionMode locomotionMode = stateType == typeof(PlayerWalkState) ? PlayerLocomotionMode.Walk : stateType == typeof(PlayerRunState) ? PlayerLocomotionMode.Run : stateType == typeof(PlayerFastRunState) ? PlayerLocomotionMode.FastRun : PlayerLocomotionMode.Idle;
        if (animationSet != null && animationSet.TryResolveLoop(locomotionMode, CurrentSupportFoot, out PlayerAnimationSelection selection))
        {
            return selection;
        }
        if (stateType == typeof(PlayerIdleState) || stateType == typeof(PlayerHardLandingState)) return new PlayerAnimationSelection(idleLoopTransition, null);
        if (stateType == typeof(PlayerWalkState)) return new PlayerAnimationSelection(walkLoopTransition, null);
        if (stateType == typeof(PlayerRunState)) return new PlayerAnimationSelection(runLoopTransition, null);
        if (stateType == typeof(PlayerFastRunState)) return new PlayerAnimationSelection(fastRunLoopTransition, null);
        if (stateType == typeof(PlayerAirState)) return new PlayerAnimationSelection(airLoopTransition, null);
        return default;
    }
    /// <summary>处理motion驱动动画脚步选择</summary>
    private void UpdateMotionSupportFoot(PlayerMotionSnapshot motion)
    {
        if (motion.ActiveProfile == null) return;
        PlayerFoot fallback = motion.SupportFoot == PlayerFoot.Unknown ? currentSupportFoot : motion.SupportFoot;
        currentSupportFoot = motion.ActiveProfile.ResolveSupportFoot(motion.Progress, fallback);
    }
    /// <summary>处理循环脚步选择</summary>
    private void UpdateLoopSupportFoot()
    {
        if (stableLoopState == null || stableLoopProfile == null || !stableLoopProfile.HasPlantMarkers) return;
        currentSupportFoot = stableLoopProfile.ResolveLoopSupportFoot(stableLoopState.NormalizedTime, currentSupportFoot);
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
