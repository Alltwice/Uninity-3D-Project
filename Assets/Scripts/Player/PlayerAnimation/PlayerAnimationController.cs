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
    [FormerlySerializedAs("locomotionTransition")]
    [SerializeField] private LinearMixerTransition groundLocomotionTransition = new LinearMixerTransition();
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
    [SerializeField] private ClipTransition fastRunDodgeToIdleTransition = new ClipTransition();
    [SerializeField] private ClipTransition dodgeToFastRunTransition = new ClipTransition();

    [Header("180 度 Locomotion 表现")]
    [SerializeField] private ClipTransition walkStart180Transition = new ClipTransition();
    [SerializeField] private ClipTransition runStart180Transition = new ClipTransition();
    [SerializeField] private ClipTransition walkTurn180Transition = new ClipTransition();
    [SerializeField] private ClipTransition runTurn180Transition = new ClipTransition();
    [SerializeField] private ClipTransition fastRunTurn180Transition = new ClipTransition();

    private ulong playbackSequence;
    private AnimancerState hardLandingState;
    private PlayerMotor playerMotor;
    private PlayerLocomotionAnimationResolver locomotionResolver;
    private Type currentGameplayStateType;
    private bool movingTurnArmed = true;

    public bool IsHardLandingComplete => hardLandingState.NormalizedTime >= 1f;
    public bool CanInterruptHardLanding => hardLandingState.NormalizedTime >= config.HardLandingInterruptNormalizedTime;

    private void Awake()
    {
        animancer = GetComponent<AnimancerComponent>();
        playerMotor = GetComponent<PlayerMotor>();
        locomotionResolver = new PlayerLocomotionAnimationResolver(config.Turn180Threshold);
    }

    private void LateUpdate()
    {
        if (IsGroundLocomotionState(currentGameplayStateType))
        {
            if (groundLocomotionTransition.State != null)
            {
                groundLocomotionTransition.State.Parameter = playerMotor.HorizontalSpeed;
            }
        }
        if (IsLocomotionLoopCurrent())
        {
            EvaluateMovingTurn();
        }
    }
    /// <summary>
    /// 拿到状态机数据快照后开始处理动画
    /// </summary>
    public void PlayTransition(PlayerStateTransition transition)
    {
        currentGameplayStateType = transition.CurrentStateType;
        if (IsGroundLocomotionSwitch(transition))
        {
            return;
        }
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
        return edge == idleToWalkTransition || edge == walkToIdleTransition || edge == idleToRunTransition || edge == runToIdleTransition || edge == fastRunDodgeToIdleTransition || edge == dodgeToFastRunTransition || IsStart180Transition(edge);
    }
    //给走动和奔跑开启移动旋转
    private bool ShouldRedirectAnimationMotion(ClipTransition edge)
    {
        return edge == idleToWalkTransition || edge == idleToRunTransition || edge == walkStart180Transition || edge == runStart180Transition;
    }

    private void PlayOptionalTransition(ClipTransition edge, ITransition targetLoop, ulong requestSequence, bool isAnimationDriven = false)
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
            playerMotor.SetMotionMode(PlayerMotionMode.AnimationDriven, ShouldRedirectAnimationMotion(edge));
        }
        animancer.Play(edge);
    }
    /// <summary>
    /// 依据当前状态选择循环
    /// </summary>
    private ITransition ResolveLoop(Type stateType)
    {
        if (stateType == typeof(PlayerIdleState)) return idleLoopTransition;
        if (IsGroundLocomotionState(stateType)) return groundLocomotionTransition;
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
        if (previous == typeof(PlayerIdleState) && IsGroundLocomotionState(current))
        {
            LocomotionTurnType turnType = locomotionResolver.ResolveIdleStart(transform.forward, playerMotor.DesiredMoveDirection);
            ClipTransition start180Transition = ResolveStart180(current, turnType);
            if (start180Transition != null && start180Transition.Clip != null)
            {
                return start180Transition;
            }
            return current == typeof(PlayerWalkState) ? idleToWalkTransition : idleToRunTransition;
        }
        if (previous == typeof(PlayerWalkState) && current == typeof(PlayerIdleState)) return walkToIdleTransition;
        if (previous == typeof(PlayerRunState) && current == typeof(PlayerIdleState)) return runToIdleTransition;
        if (previous == typeof(PlayerFastRunState) && current == typeof(PlayerIdleState)) return fastRunDodgeToIdleTransition;
        if (previous == typeof(PlayerDodgeState) && current == typeof(PlayerIdleState)) return fastRunDodgeToIdleTransition;
        return null;
    }

    private void EvaluateMovingTurn()
    {
        LocomotionTurnType turnType = locomotionResolver.ResolveMovingTurn(playerMotor.HorizontalMoveDirection, playerMotor.DesiredMoveDirection);
        if (turnType == LocomotionTurnType.None)
        {
            movingTurnArmed = true;
            return;
        }
        if (!movingTurnArmed)
        {
            return;
        }
        ClipTransition turnTransition = ResolveMovingTurn(currentGameplayStateType, turnType);
        if (turnTransition == null || turnTransition.Clip == null)
        {
            return;
        }
        movingTurnArmed = false;
        ulong requestSequence = ++playbackSequence;
        playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
        PlayOptionalTransition(turnTransition, ResolveLoop(currentGameplayStateType), requestSequence, IsAnimationDrivenGroundEdge(turnTransition));
    }

    private ClipTransition ResolveStart180(Type stateType, LocomotionTurnType turnType)
    {
        if (turnType != LocomotionTurnType.Turn180) return null;
        return stateType == typeof(PlayerWalkState) ? walkStart180Transition : runStart180Transition;
    }

    private ClipTransition ResolveMovingTurn(Type stateType, LocomotionTurnType turnType)
    {
        if (turnType != LocomotionTurnType.Turn180) return null;
        if (stateType == typeof(PlayerWalkState)) return walkTurn180Transition;
        if (stateType == typeof(PlayerRunState)) return runTurn180Transition;
        if (stateType == typeof(PlayerFastRunState)) return fastRunTurn180Transition;
        return null;
    }

    private bool IsStart180Transition(ClipTransition transition)
    {
        return transition == walkStart180Transition || transition == runStart180Transition;
    }

    private bool IsLocomotionLoopCurrent()
    {
        if (IsGroundLocomotionState(currentGameplayStateType))
        {
            return groundLocomotionTransition.State != null && groundLocomotionTransition.State.IsCurrent;
        }
        return currentGameplayStateType == typeof(PlayerFastRunState) && fastRunLoopTransition.State != null && fastRunLoopTransition.State.IsCurrent;
    }

    private static bool IsGroundLocomotionState(Type stateType)
    {
        return stateType == typeof(PlayerWalkState) || stateType == typeof(PlayerRunState);
    }

    private static bool IsGroundLocomotionSwitch(PlayerStateTransition transition)
    {
        return IsGroundLocomotionState(transition.PreviousStateType) && IsGroundLocomotionState(transition.CurrentStateType);
    }
}
