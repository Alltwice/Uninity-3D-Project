using System;
using Animancer;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 将已经提交的 Gameplay 状态转换翻译为 Animancer 播放请求。
/// </summary>
public class PlayerAnimationController : MonoBehaviour, IPlayerAnimationController
{
    [Header("引用")]
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private PlayerAnimationConfig config;

    [Header("稳定循环")]
    [SerializeField] private ClipTransition idleLoopTransition = new ClipTransition();
    [SerializeField] private ClipTransition walkLoopTransition = new ClipTransition();
    [SerializeField] private ClipTransition runLoopTransition = new ClipTransition();
    [FormerlySerializedAs("fastRunTransition")]
    [SerializeField] private ClipTransition fastRunLoopTransition = new ClipTransition();
    [FormerlySerializedAs("jumpIdleTransition")]
    [SerializeField] private ClipTransition airLoopTransition = new ClipTransition();

    [Header("能力与落地")]
    [SerializeField] private ClipTransition dodgeTransition = new ClipTransition();
    [FormerlySerializedAs("jumpUpTransition")]
    [SerializeField] private ClipTransition jumpStartTransition = new ClipTransition();
    [SerializeField] private ClipTransition landingTransition = new ClipTransition();
    [SerializeField] private ClipTransition hardLandingTransition = new ClipTransition();

    [Header("地面状态边")]
    [SerializeField] private ClipTransition idleToWalkTransition = new ClipTransition();
    [SerializeField] private ClipTransition walkToIdleTransition = new ClipTransition();
    [SerializeField] private ClipTransition idleToRunTransition = new ClipTransition();
    [SerializeField] private ClipTransition runToIdleTransition = new ClipTransition();
    [SerializeField] private ClipTransition walkToRunTransition = new ClipTransition();
    [SerializeField] private ClipTransition runToWalkTransition = new ClipTransition();
    [FormerlySerializedAs("fastRunStopTransition")]
    [SerializeField] private ClipTransition fastRunToIdleTransition = new ClipTransition();
    [SerializeField] private ClipTransition dodgeToFastRunTransition = new ClipTransition();

    private ulong playbackSequence;
    private AnimancerState hardLandingState;

    public bool IsHardLandingComplete => hardLandingState.NormalizedTime >= 1f;
    public bool CanInterruptHardLanding => hardLandingState.NormalizedTime >= config.HardLandingInterruptNormalizedTime;

    private void Awake()
    {
        animancer = GetComponent<AnimancerComponent>();
    }

    public void PlayTransition(PlayerStateTransition transition)
    {
        ulong requestSequence = ++playbackSequence;
        if (transition.CurrentStateType == typeof(PlayerHardLandingState))
        {
            hardLandingState = animancer.Play(hardLandingTransition);
            return;
        }
        if (transition.CurrentStateType == typeof(PlayerDodgeState))
        {
            PlayOptionalTransition(dodgeTransition, null, requestSequence);
            return;
        }
        if (transition.CurrentStateType == typeof(PlayerAirState))
        {
            if (transition.Reason == PlayerStateTransitionReason.Jumped)
            {
                PlayOptionalTransition(jumpStartTransition, airLoopTransition, requestSequence);
            }
            else
            {
                animancer.Play(airLoopTransition);
            }
            return;
        }

        ClipTransition targetLoop = ResolveLoop(transition.CurrentStateType);
        PlayOptionalTransition(ResolveEdge(transition), targetLoop, requestSequence);
    }

    private void PlayOptionalTransition(ClipTransition edge, ClipTransition targetLoop, ulong requestSequence)
    {
        if (edge == null || edge.Clip == null)
        {
            if (targetLoop != null)
            {
                animancer.Play(targetLoop);
            }
            return;
        }

        edge.Events.OnEnd = targetLoop == null ? null : () =>
        {
            if (requestSequence == playbackSequence)
            {
                animancer.Play(targetLoop);
            }
        };
        animancer.Play(edge);
    }

    private ClipTransition ResolveLoop(Type stateType)
    {
        if (stateType == typeof(PlayerIdleState)) return idleLoopTransition;
        if (stateType == typeof(PlayerWalkState)) return walkLoopTransition;
        if (stateType == typeof(PlayerRunState)) return runLoopTransition;
        if (stateType == typeof(PlayerFastRunState)) return fastRunLoopTransition;
        return null;
    }

    private ClipTransition ResolveEdge(PlayerStateTransition transition)
    {
        Type previous = transition.PreviousStateType;
        Type current = transition.CurrentStateType;
        if (previous == typeof(PlayerAirState)) return landingTransition;
        if (previous == typeof(PlayerDodgeState) && current == typeof(PlayerFastRunState)) return dodgeToFastRunTransition;
        if (previous == typeof(PlayerIdleState) && current == typeof(PlayerWalkState)) return idleToWalkTransition;
        if (previous == typeof(PlayerWalkState) && current == typeof(PlayerIdleState)) return walkToIdleTransition;
        if (previous == typeof(PlayerIdleState) && current == typeof(PlayerRunState)) return idleToRunTransition;
        if (previous == typeof(PlayerRunState) && current == typeof(PlayerIdleState)) return runToIdleTransition;
        if (previous == typeof(PlayerWalkState) && current == typeof(PlayerRunState)) return walkToRunTransition;
        if (previous == typeof(PlayerRunState) && current == typeof(PlayerWalkState)) return runToWalkTransition;
        if (previous == typeof(PlayerFastRunState) && current == typeof(PlayerIdleState)) return fastRunToIdleTransition;
        return null;
    }
}
