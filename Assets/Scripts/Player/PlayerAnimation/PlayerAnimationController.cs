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
    [SerializeField] private LinearMixerTransition groundLocomotionTransition = new LinearMixerTransition();
    [SerializeField] private ClipTransition fastRunLoopTransition = new ClipTransition();
    [SerializeField] private ClipTransition airLoopTransition = new ClipTransition();

    [Header("能力与落地")]
    [SerializeField] private ClipTransition dodgeTransition = new ClipTransition();
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
    [SerializeField] private ClipTransition walkStart180LeftTransition = new ClipTransition();
    [SerializeField] private ClipTransition walkStart180RightTransition = new ClipTransition();
    [SerializeField] private ClipTransition runStart180LeftTransition = new ClipTransition();
    [SerializeField] private ClipTransition runStart180RightTransition = new ClipTransition();
    [SerializeField] private ClipTransition walkTurn180Transition = new ClipTransition();
    [SerializeField] private ClipTransition runTurn180Transition = new ClipTransition();
    [SerializeField] private ClipTransition fastRunTurn180Transition = new ClipTransition();

    private ulong playbackSequence;
    private AnimancerState hardLandingState;
    private PlayerMotor playerMotor;
    private PlayerLocomotionAnimationResolver locomotionResolver;
    private Type currentGameplayStateType;
    private bool isTurnPresentationActive;
    private bool isTurnRotationUnlocked;
    private Vector3 turnRequestDirection;

    public bool IsHardLandingComplete => hardLandingState.NormalizedTime >= 1f;
    public bool CanInterruptHardLanding => hardLandingState.NormalizedTime >= config.HardLandingInterruptNormalizedTime;

    private void Awake()
    {
        animancer = GetComponent<AnimancerComponent>();
        playerMotor = GetComponent<PlayerMotor>();
        locomotionResolver = new PlayerLocomotionAnimationResolver(config.Turn180Threshold);
    }
    /// <summary>
    /// 动画层自己的 Update 用于驱动连续 Mixer 参数并维护短暂 Turn180 表现
    /// </summary>
    private void LateUpdate()
    {
        if (IsGroundLocomotionState(currentGameplayStateType))
        {
            if (groundLocomotionTransition.State != null)
            {
                groundLocomotionTransition.State.Parameter = playerMotor.HorizontalSpeed;
            }
        }
        EvaluateTurnPresentation();
    }
    /// <summary>
    /// 拿到状态机数据快照后开始处理动画
    /// </summary>
    public void PlayTransition(PlayerStateTransition transition)
    {
        currentGameplayStateType = transition.CurrentStateType;
        if (IsGroundLocomotionSwitch(transition))
        {
            if (!IsLocomotionLoopCurrent() && !isTurnPresentationActive)
            {
                ++playbackSequence;
                playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
                animancer.Play(groundLocomotionTransition);
            }
            return;
        }
        bool interruptedTurnPresentation = isTurnPresentationActive;
        //给予编号，确保在未发生变化时不会重复处理内容
        ulong requestSequence = ++playbackSequence;
        isTurnPresentationActive = false;
        isTurnRotationUnlocked = false;
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
        if (interruptedTurnPresentation)
        {
            ITransition interruptedTargetLoop = ResolveLoop(transition.CurrentStateType);
            if (interruptedTargetLoop != null)
            {
                animancer.Play(interruptedTargetLoop);
            }
            return;
        }
        ClipTransition edge = ResolveEdge(transition);
        if (IsStart180Transition(edge))
        {
            PlayStart180(edge, ResolveLoop(transition.CurrentStateType), requestSequence);
            return;
        }
        bool isAnimationDriven = IsAnimationDrivenGroundEdge(edge);
        PlayOptionalTransition(edge, ResolveLoop(transition.CurrentStateType), requestSequence, isAnimationDriven);
    }
    /// <summary>
    /// 这里就规定了是否是动画驱动
    /// </summary>
    private bool IsAnimationDrivenGroundEdge(ClipTransition edge)
    {
        if (edge == null || edge.Clip == null) return false;
        return edge == idleToWalkTransition || edge == walkToIdleTransition || edge == idleToRunTransition ||
               edge == runToIdleTransition || edge == fastRunDodgeToIdleTransition || edge == dodgeToFastRunTransition;
    }
    //给走动和奔跑开启移动旋转
    private bool ShouldRedirectAnimationMotion(ClipTransition edge)
    {
        return edge == idleToWalkTransition || edge == idleToRunTransition;
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
                isTurnPresentationActive = false;
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
            playerMotor.SetMotionMode(PlayerMotionMode.AnimationDriven, AnimationMotionChannels.Translation, ShouldRedirectAnimationMotion(edge));
        }
        animancer.Play(edge);
    }

    private void PlayStart180(ClipTransition edge, ITransition targetLoop, ulong requestSequence)
    {
        isTurnPresentationActive = true;
        isTurnRotationUnlocked = false;
        turnRequestDirection = playerMotor.DesiredMoveDirection;
        edge.Events.OnEnd = () =>
        {
            if (requestSequence == playbackSequence)
            {
                CompleteTurnPresentation(targetLoop);
            }
        };
        playerMotor.SetMotionMode(PlayerMotionMode.AnimationDriven, AnimationMotionChannels.Translation | AnimationMotionChannels.Rotation);
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
        //处理轻落地后的动画打断
        if (previous == typeof(PlayerAirState))
        {
            return IsGroundLocomotionState(current) && playerMotor.DesiredMoveDirection.sqrMagnitude > 0.001f ? null : landingTransition;
        }
        if (previous == typeof(PlayerDodgeState) && current == typeof(PlayerFastRunState)) return dodgeToFastRunTransition;
        //之前是idle，之后是地面状态
        if (previous == typeof(PlayerIdleState) && IsGroundLocomotionState(current))
        {
            //比较朝向和输入意图
            LocomotionTurnType turnType = locomotionResolver.ResolveIdleStart(transform.forward, playerMotor.DesiredMoveDirection);
            ClipTransition start180Transition = ResolveStart180(current, turnType);
            if (start180Transition != null && start180Transition.Clip != null)
            {
                return start180Transition;
            }
            //转向动画为空的情况下默认不启用
            return current == typeof(PlayerWalkState) ? idleToWalkTransition : idleToRunTransition;
        }
        if (previous == typeof(PlayerWalkState) && current == typeof(PlayerIdleState)) return walkToIdleTransition;
        if (previous == typeof(PlayerRunState) && current == typeof(PlayerIdleState)) return runToIdleTransition;
        if (previous == typeof(PlayerFastRunState) && current == typeof(PlayerIdleState)) return fastRunDodgeToIdleTransition;
        if (previous == typeof(PlayerDodgeState) && current == typeof(PlayerIdleState)) return fastRunDodgeToIdleTransition;
        return null;
    }
    /// <summary>
    /// 处理移动中转身动画
    /// </summary>
    private void EvaluateTurnPresentation()
    {
        if (!isTurnPresentationActive) return;
        Vector3 desiredMoveDirection = playerMotor.DesiredMoveDirection;
        if (desiredMoveDirection.sqrMagnitude < 0.001f || Vector3.Angle(turnRequestDirection, desiredMoveDirection) > config.TurnPresentationIntentTolerance)
        {
            CancelTurnPresentation();
            return;
        }
        if (!isTurnRotationUnlocked && Vector3.Angle(transform.forward, desiredMoveDirection) <= config.TurnRotationUnlockAngle)
        {
            UnlockTurnRotation();
        }
        if (isTurnRotationUnlocked)
        {
            playerMotor.RotateTowardsDesiredDirection();
        }
    }

    private void UnlockTurnRotation()
    {
        isTurnRotationUnlocked = true;
        playerMotor.SetMotionMode(PlayerMotionMode.AnimationDriven, AnimationMotionChannels.Translation);
    }

    private void CancelTurnPresentation()
    {
        isTurnPresentationActive = false;
        isTurnRotationUnlocked = false;
        ++playbackSequence;
        playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
        ITransition targetLoop = ResolveLoop(currentGameplayStateType);
        if (targetLoop != null)
        {
            animancer.Play(targetLoop);
        }
    }

    private void CompleteTurnPresentation(ITransition targetLoop)
    {
        isTurnPresentationActive = false;
        isTurnRotationUnlocked = false;
        playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
        if (targetLoop != null)
        {
            animancer.Play(targetLoop);
        }
    }
    /// <summary>
    /// 处理起步转向
    /// </summary>
    private ClipTransition ResolveStart180(Type stateType, LocomotionTurnType turnType)
    {
        if (stateType == typeof(PlayerWalkState))
        {
            if (turnType == LocomotionTurnType.Turn180Left) return walkStart180LeftTransition;
            if (turnType == LocomotionTurnType.Turn180Right) return walkStart180RightTransition;
        }
        if (stateType == typeof(PlayerRunState))
        {
            if (turnType == LocomotionTurnType.Turn180Left) return runStart180LeftTransition;
            if (turnType == LocomotionTurnType.Turn180Right) return runStart180RightTransition;
        }
        return null;
    }

    private bool IsStart180Transition(ClipTransition transition)
    {
        return transition == walkStart180LeftTransition || transition == walkStart180RightTransition ||
               transition == runStart180LeftTransition || transition == runStart180RightTransition;
    }
    /// <summary>
    /// 如果是地面移动状态
    /// </summary>
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
