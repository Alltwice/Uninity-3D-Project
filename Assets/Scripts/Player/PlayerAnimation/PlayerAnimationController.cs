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
    [SerializeField] private LinearMixerTransition groundLocomotionTransition = new LinearMixerTransition();
    [SerializeField] private ClipTransition fastRunLoopTransition = new ClipTransition();
    [SerializeField] private ClipTransition airLoopTransition = new ClipTransition();

    [Header("非 Motion 表现")]
    [SerializeField] private ClipTransition jumpStartTransition = new ClipTransition();
    [SerializeField] private ClipTransition landingTransition = new ClipTransition();
    [SerializeField] private ClipTransition hardLandingTransition = new ClipTransition();

    private AnimancerState boundaryState;
    private AnimancerState handoffLoopState;
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

    public void Present(Type currentGameplayStateType, PlayerStateTransition? transition, PlayerMotionSnapshot motion, PlayerMotorResult motorResult, float stateProgress)
    {
        gameplayStateType = currentGameplayStateType;
        bool newMotion = motion.ActiveDefinition != null && motion.InstanceId != presentedMotionInstanceId;
        if (newMotion) PlayMotion(motion);
        if (transition.HasValue && !newMotion) PlayStateTransition(transition.Value);
        if (motion.ActiveDefinition != null && motion.InstanceId == presentedMotionInstanceId) UpdateBoundaryMotion(motion);
        else if (motion.JustCancelled) PlayStableLoop(gameplayStateType);
        if (gameplayStateType == typeof(PlayerHardLandingState) && hardLandingState != null)
        {
            hardLandingState.Speed = 0f;
            hardLandingState.NormalizedTime = stateProgress;
        }
        if (groundLocomotionTransition.State != null) groundLocomotionTransition.State.Parameter = motorResult.HorizontalSpeed;
    }

    public void EvaluateGraph(float deltaTime)
    {
        animancer.Evaluate(Mathf.Max(0f, deltaTime));
    }

    private void PlayMotion(PlayerMotionSnapshot motion)
    {
        ++presentationSequence;
        presentedMotionInstanceId = motion.InstanceId;
        boundaryState = null;
        handoffLoopState = null;
        activeBinding = null;
        //没拿到绑定就去依据状态播动画
        if (!animationSet.TryGetBinding(motion.ActiveDefinition, out activeBinding))
        {
            PlayStableLoop(gameplayStateType);
            return;
        }
        //拿到绑定播放动画，动画不自己播放，其播放进程绑定motionruntime
        boundaryState = animancer.Play(activeBinding.Transition, activeBinding.Transition.FadeDuration, FadeMode.FixedDuration);
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

    private void EnsureHandoffLoop()
    {
        if (handoffLoopState != null) return;
        ITransition loop = ResolveLoop(gameplayStateType);
        if (loop == null) return;
        handoffLoopState = animancer.Play(loop, 0f, FadeMode.FixedDuration);
        boundaryState.Weight = 1f;
        handoffLoopState.Weight = 0f;
        if (groundLocomotionTransition.State != null) groundLocomotionTransition.State.Parameter = 0f;
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
        if (transition.CurrentStateType == typeof(PlayerAirState))
        {
            if (transition.Reason == PlayerStateTransitionReason.Jumped) PlayPresentationEdge(jumpStartTransition, airLoopTransition, presentationSequence);
            else animancer.Play(airLoopTransition);
            return;
        }
        if (transition.PreviousStateType == typeof(PlayerAirState) && transition.CurrentStateType == typeof(PlayerIdleState))
        {
            PlayPresentationEdge(landingTransition, idleLoopTransition, presentationSequence);
            return;
        }
        PlayStableLoop(transition.CurrentStateType);
    }
    /// <summary>
    /// 处理动画边
    /// </summary>
    private void PlayPresentationEdge(ClipTransition edge, ITransition targetLoop, ulong sequence)
    {
        if (edge == null || edge.Clip == null)
        {
            if (targetLoop != null) animancer.Play(targetLoop);
            return;
        }
        edge.Events.OnEnd = () =>
        {
            if (sequence == presentationSequence && targetLoop != null) animancer.Play(targetLoop);
        };
        animancer.Play(edge);
    }

    private void PlayStableLoop(Type stateType)
    {
        ITransition loop = ResolveLoop(stateType);
        if (loop != null) animancer.Play(loop);
    }

    private ITransition ResolveLoop(Type stateType)
    {
        if (stateType == typeof(PlayerIdleState) || stateType == typeof(PlayerHardLandingState)) return idleLoopTransition;
        if (stateType == typeof(PlayerWalkState) || stateType == typeof(PlayerRunState)) return groundLocomotionTransition;
        if (stateType == typeof(PlayerFastRunState)) return fastRunLoopTransition;
        if (stateType == typeof(PlayerAirState)) return airLoopTransition;
        return null;
    }

    private void ClearBoundary(bool clearLoop = true)
    {
        boundaryState = null;
        activeBinding = null;
        DebugBoundaryPhase = 0f;
        if (clearLoop) handoffLoopState = null;
    }
}
