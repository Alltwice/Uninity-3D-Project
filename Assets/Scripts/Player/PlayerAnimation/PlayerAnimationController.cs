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

    //动画请求编号
    private ulong playbackSequence;
    private AnimancerState hardLandingState;
    private AnimancerState locomotionStartState;
    private AnimancerState locomotionHandoffState;
    private AnimancerState debugLocomotionTransitionState;
    private PlayerMotor playerMotor;
    private PlayerLocomotionAnimationResolver locomotionResolver;
    private Type currentGameplayStateType;
    //开始动画编号
    private ulong locomotionStartSequence;
    private int locomotionHandoffStartFrame;
    //正在混合
    private bool isLocomotionStartHandoffActive;
    private bool locomotionStartOwnsMotion;
    //是否还在Turn表现中
    private bool isTurnPresentationActive;
    //是否失去了Rotation的控制权
    private bool isTurnRotationUnlocked;
    //转向动画开始时记录的玩家最终期望到达的角度
    private Vector3 turnRequestDirection;

    public bool IsHardLandingComplete => hardLandingState.NormalizedTime >= 1f;
    public bool CanInterruptHardLanding => hardLandingState.NormalizedTime >= config.HardLandingInterruptNormalizedTime;
    public float DebugLocomotionTransitionNormalizedTime => debugLocomotionTransitionState == null ? 0f : debugLocomotionTransitionState.NormalizedTime;

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
        EvaluateLocomotionStartHandoff();
        EvaluateTurnPresentation();
    }
    /// <summary>
    /// 拿到状态机数据快照后开始处理动画
    /// </summary>
    public bool IsRunTransitionMotionCandidate(PlayerStateTransition transition)
    {
        if (isTurnPresentationActive || IsGroundLocomotionSwitch(transition)) return false;
        ClipTransition edge = ResolveEdge(transition);
        return edge != null && edge.Clip != null && (edge == idleToRunTransition || edge == runToIdleTransition);
    }

    public void PlayTransition(PlayerStateTransition transition, bool profileDrivenRunTransition)
    {
        currentGameplayStateType = transition.CurrentStateType;
        debugLocomotionTransitionState = null;
        if (IsGroundLocomotionSwitch(transition))
        {
            if (!IsLocomotionLoopCurrent() && !isTurnPresentationActive)
            {
                ++playbackSequence;
                ClearLocomotionStartHandoff();
                playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
                animancer.Play(groundLocomotionTransition);
            }
            return;
        }
        bool interruptedTurnPresentation = isTurnPresentationActive;
        //给予编号，确保在未发生变化时不会重复处理内容
        ulong requestSequence = ++playbackSequence;
        ClearLocomotionStartHandoff();
        isTurnPresentationActive = false;
        isTurnRotationUnlocked = false;
        if (!profileDrivenRunTransition) playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
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
        //新的动画变化来了，如果还在转身，那么必须打断
        if (interruptedTurnPresentation)
        {
            ITransition interruptedTargetLoop = ResolveLoop(transition.CurrentStateType);
            if (interruptedTargetLoop != null)
            {
                animancer.Play(interruptedTargetLoop);
            }
            return;
        }
        //检测edge，是否满足转身动画
        ClipTransition edge = ResolveEdge(transition);
        if (IsStart180Transition(edge))
        {
            PlayStart180(edge, requestSequence);
            return;
        }
        if (IsLocomotionStartTransition(edge))
        {
            PlayLocomotionStart(edge, requestSequence, profileDrivenRunTransition);
            return;
        }
        bool isAnimationDriven = IsAnimationDrivenGroundEdge(edge, profileDrivenRunTransition);
        //所有特殊情况处理完后的最终部分
        PlayOptionalTransition(edge, ResolveLoop(transition.CurrentStateType), requestSequence, isAnimationDriven);
    }
    /// <summary>
    /// 这里就规定了是否是动画驱动
    /// </summary>
    private bool IsAnimationDrivenGroundEdge(ClipTransition edge, bool profileDrivenRunTransition)
    {
        if (edge == null || edge.Clip == null) return false;
        if (profileDrivenRunTransition && (edge == idleToRunTransition || edge == runToIdleTransition)) return false;
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
        //没有edge播loop
        if (edge == null || edge.Clip == null)
        {
            if (targetLoop != null)
            {
                animancer.Play(targetLoop);
            }
            return;
        }
        //有edge注册edge并在结尾处依据是否还有loop选播放loop
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
        //如果边界动画是动画驱动设置其动画性质然后播放
        if (isAnimationDriven)
        {
            playerMotor.SetMotionMode(PlayerMotionMode.AnimationDriven, AnimationMotionChannels.Translation, ShouldRedirectAnimationMotion(edge));
        }
        AnimancerState edgeState = animancer.Play(edge);
        if (edge == idleToRunTransition || edge == runToIdleTransition) debugLocomotionTransitionState = edgeState;
    }
    /// <summary>
    /// 单独处理起步动画
    /// </summary>
    private void PlayLocomotionStart(ClipTransition edge, ulong requestSequence, bool profileDrivenRunTransition)
    {
        if (edge == null || edge.Clip == null)
        {
            animancer.Play(groundLocomotionTransition);
            return;
        }
        PrepareLocomotionStartHandoff(edge, requestSequence, !profileDrivenRunTransition);
        if (!profileDrivenRunTransition) playerMotor.SetMotionMode(PlayerMotionMode.AnimationDriven, AnimationMotionChannels.Translation, true);
        locomotionStartState = animancer.Play(edge);
        debugLocomotionTransitionState = locomotionStartState;
    }

    private void PrepareLocomotionStartHandoff(ClipTransition edge, ulong requestSequence, bool ownsMotion)
    {
        locomotionStartSequence = requestSequence;
        locomotionStartOwnsMotion = ownsMotion;
        edge.Events.OnEnd = () =>
        {
            //检查动画是否被新动画顶掉
            if (requestSequence == playbackSequence && requestSequence == locomotionStartSequence && !isLocomotionStartHandoffActive)
            {
                BeginLocomotionStartHandoff(requestSequence);
            }
        };
    }
    /// <summary>
    /// 每帧观察是否需要启动混合
    /// </summary>
    private void EvaluateLocomotionStartHandoff()
    {
        if (locomotionStartState == null || locomotionStartSequence != playbackSequence) return;
        if (!isLocomotionStartHandoffActive)
        {
            //播放剩余时间小于规定混合时间开始混合
            if (locomotionStartState.RemainingDuration <= config.LocomotionStartHandoffDuration)
            {
                BeginLocomotionStartHandoff(locomotionStartSequence);
            }
            return;
        }
        //确保混合前不会把运动切换为代码驱动
        if (Time.frameCount <= locomotionHandoffStartFrame || locomotionHandoffState == null) return;
        //确保Mixer彻底接管了动画
        if (locomotionHandoffState.IsCurrent && locomotionHandoffState.Weight == 1f && locomotionHandoffState.FadeGroup == null)
        {
            CompleteLocomotionStartHandoff();
        }
    }

    private void BeginLocomotionStartHandoff(ulong requestSequence)
    {
        if (requestSequence != playbackSequence || requestSequence != locomotionStartSequence || isLocomotionStartHandoffActive) return;
        isLocomotionStartHandoffActive = true;
        //记录混合是在哪一帧开始的
        locomotionHandoffStartFrame = Time.frameCount;
        locomotionHandoffState = animancer.Play(groundLocomotionTransition, config.LocomotionStartHandoffDuration, FadeMode.FixedDuration);
        groundLocomotionTransition.State.Parameter = playerMotor.HorizontalSpeed;
    }

    private void CompleteLocomotionStartHandoff()
    {
        isTurnPresentationActive = false;
        isTurnRotationUnlocked = false;
        if (locomotionStartOwnsMotion) playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
        ClearLocomotionStartHandoff();
    }

    private void ClearLocomotionStartHandoff()
    {
        locomotionStartState = null;
        locomotionHandoffState = null;
        locomotionStartSequence = 0;
        locomotionHandoffStartFrame = 0;
        isLocomotionStartHandoffActive = false;
        locomotionStartOwnsMotion = false;
    }
    /// <summary>
    /// 处理转向动画
    /// </summary>
    private void PlayStart180(ClipTransition edge, ulong requestSequence)
    {
        isTurnPresentationActive = true;
        isTurnRotationUnlocked = false;
        turnRequestDirection = playerMotor.DesiredMoveDirection;
        PrepareLocomotionStartHandoff(edge, requestSequence, true);
        playerMotor.SetMotionMode(PlayerMotionMode.AnimationDriven, AnimationMotionChannels.Translation | AnimationMotionChannels.Rotation);
        locomotionStartState = animancer.Play(edge);
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
    /// <summary>
    /// 处理过渡动画，包括转向
    /// </summary>
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
    /// 评估转向动画各类条件
    /// </summary>
    private void EvaluateTurnPresentation()
    {
        //确保在转向中
        if (!isTurnPresentationActive) return;
        Vector3 desiredMoveDirection = playerMotor.DesiredMoveDirection;
        //零输入或是最终到达角度和玩家输入角度偏离过大
        if (desiredMoveDirection.sqrMagnitude < 0.001f || Vector3.Angle(turnRequestDirection, desiredMoveDirection) > config.TurnPresentationIntentTolerance)
        {
            CancelTurnPresentation();
            return;
        }
        //旋转限制未接触的情况下，玩家面向角度和输入角度小于解锁限制角度
        if (!isTurnRotationUnlocked && Vector3.Angle(transform.forward, desiredMoveDirection) <= config.TurnRotationUnlockAngle)
        {
            UnlockTurnRotation();
        }
        //角度解锁限制后启用输入旋转
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
    /// <summary>
    /// 取消当前转向动画
    /// </summary>
    private void CancelTurnPresentation()
    {
        isTurnPresentationActive = false;
        isTurnRotationUnlocked = false;
        ++playbackSequence;
        ClearLocomotionStartHandoff();
        playerMotor.SetMotionMode(PlayerMotionMode.CodeDriven);
        ITransition targetLoop = ResolveLoop(currentGameplayStateType);
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

    private bool IsLocomotionStartTransition(ClipTransition transition)
    {
        return transition == idleToWalkTransition || transition == idleToRunTransition;
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
    /// <summary>
    /// 如果是walk/run混合
    /// </summary>
    private bool IsGroundLocomotionState(Type stateType)
    {
        return stateType == typeof(PlayerWalkState) || stateType == typeof(PlayerRunState);
    }

    private bool IsGroundLocomotionSwitch(PlayerStateTransition transition)
    {
        return IsGroundLocomotionState(transition.PreviousStateType) && IsGroundLocomotionState(transition.CurrentStateType);
    }
}
