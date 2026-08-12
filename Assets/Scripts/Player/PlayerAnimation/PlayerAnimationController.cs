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
    [SerializeField] private ClipTransition fastRunDodgeToIdleTransition = new ClipTransition();
    [SerializeField] private ClipTransition dodgeToFastRunTransition = new ClipTransition();

    private ulong playbackSequence;
    private AnimancerState hardLandingState;
    private PlayerMotor playerMotor;

    public bool IsHardLandingComplete => hardLandingState.NormalizedTime >= 1f;
    public bool CanInterruptHardLanding => hardLandingState.NormalizedTime >= config.HardLandingInterruptNormalizedTime;

    private void Awake()
    {
        animancer = GetComponent<AnimancerComponent>();
        playerMotor = GetComponent<PlayerMotor>();
    }
    /// <summary>
    /// 拿到状态机数据快照后开始处理动画
    /// </summary>
    public void PlayTransition(PlayerStateTransition transition)
    {
        //给予编号，确保在未发生变化时不会重复处理内容
        ulong requestSequence = ++playbackSequence;
        playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
        //优先处理特殊状态
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
        
        ClipTransition edge = ResolveEdge(transition);
        bool isAnimationDriven = IsAnimationDrivenGroundEdge(edge);
        PlayOptionalTransition(edge, ResolveLoop(transition.CurrentStateType), requestSequence, isAnimationDriven);
    }

    private bool IsAnimationDrivenGroundEdge(ClipTransition edge)
    {
        if (edge == null || edge.Clip == null) return false;
        return edge == idleToWalkTransition || edge == walkToIdleTransition || edge == idleToRunTransition || edge == runToIdleTransition || edge == walkToRunTransition || edge == runToWalkTransition || edge == fastRunDodgeToIdleTransition || edge == dodgeToFastRunTransition;
    }

    private void PlayOptionalTransition(ClipTransition edge, ClipTransition targetLoop, ulong requestSequence, bool isAnimationDriven = false)
    {
        //没有edgeloop
        if (edge == null || edge.Clip == null)
        {
            if (targetLoop != null)
            {
                animancer.Play(targetLoop);
            }
            return;
        }
        //有edge播edge并在结尾处依据是否还有loop选播放loop
        //这里是一个回调，最终执行顺序在edge执行完毕和后
        edge.Events.OnEnd = targetLoop == null && !isAnimationDriven ? null : () =>
        {
            if (requestSequence == playbackSequence)
            {
                if (targetLoop != null)
                {
                    animancer.Play(targetLoop);
                }
                if (isAnimationDriven)
                {
                    playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
                }
            }
        };
        if (isAnimationDriven)
        {
            playerMotor.SetMotionMode(PlayerMotionMode.AnimationDriven);
        }
        animancer.Play(edge);
    }
    /// <summary>
    /// 依据当前状态选择循环
    /// </summary>
    private ClipTransition ResolveLoop(Type stateType)
    {
        if (stateType == typeof(PlayerIdleState)) return idleLoopTransition;
        if (stateType == typeof(PlayerWalkState)) return walkLoopTransition;
        if (stateType == typeof(PlayerRunState)) return runLoopTransition;
        if (stateType == typeof(PlayerFastRunState)) return fastRunLoopTransition;
        return null;
    }
    //通过前后状态比对触发过渡动画
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
        if (previous == typeof(PlayerFastRunState) && current == typeof(PlayerIdleState)) return fastRunDodgeToIdleTransition;
        if (previous == typeof(PlayerDodgeState) && current == typeof(PlayerIdleState)) return fastRunDodgeToIdleTransition;
        return null;
    }
}
