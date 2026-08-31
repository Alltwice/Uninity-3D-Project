using System;
using UnityEngine;

/// <summary>
/// 玩家每帧唯一执行顺序
/// </summary>
[RequireComponent(typeof(PlayerStateController), typeof(PlayerMotionPlanner), typeof(PlayerMotor))]
public class PlayerSimulationDriver : MonoBehaviour
{
    [SerializeField] private Transform movementReference;

    private PlayerStateController stateController;
    private PlayerMotionPlanner motionPlanner;
    private PlayerMotor motor;
    private PlayerAnimationController animationController;
    private PlayerDodge dodge;
    private PlayerLandingTracker landingTracker;
    private IPlayerInputSource inputSource;
    private IPlayerActionBuffer actionBuffer;
    private PlayerStateTransition? pendingTransition;

    public PlayerLandingSnapshot LandingSnapshot { get; private set; }

    private void Awake()
    {
        stateController = GetComponent<PlayerStateController>();
        motionPlanner = GetComponent<PlayerMotionPlanner>();
        motor = GetComponent<PlayerMotor>();
        animationController = GetComponent<PlayerAnimationController>();
        dodge = GetComponent<PlayerDodge>();
        landingTracker = new PlayerLandingTracker(motor.Config.Landing);
        if (movementReference == null) movementReference = Camera.main.transform;
    }

    public void Init(IPlayerInputSource playerInput, IPlayerActionBuffer playerActionBuffer)
    {
        inputSource = playerInput;
        actionBuffer = playerActionBuffer;
    }

    private void Start()
    {
        motor.EnsureInitialized();
        pendingTransition = stateController.Initialize(inputSource, actionBuffer);
        landingTracker.Reset(stateController.TargetGroundMode);
        animationController.InitializeManualEvaluation();
    }
    
    private void Update()
    {
        //设定标准时间供下层组件使用
        float deltaTime = Time.deltaTime;
        PlayerLocomotionPhaseSnapshot phaseSnapshot = animationController.PhaseSnapshot;
        actionBuffer.Tick(deltaTime);
        motionPlanner.BeginFrame();
        dodge.TickCooldown(deltaTime);
        Vector3 desiredMoveDirection = ResolveWorldMoveDirection(inputSource.MoveInput);
        //零输入延迟检测
        stateController.UpdateLocomotionIntent(deltaTime);
        stateController.SetSimulationFacts(motor.CurrentResult, motionPlanner.Snapshot, default);
        PlayerStateTransition? transition = stateController.ProcessPreTickTransition();
        //建立输入意图
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(desiredMoveDirection, transform.forward);
        intent.LocomotionMode = stateController.CurrentLocomotionMode;
        //可空类型和一般类型完全是两个东西，需要通过.value获取
        if (transition.HasValue) motionPlanner.HandleStateTransition(transition.Value, intent, motor.CurrentResult, phaseSnapshot);
        else if (pendingTransition.HasValue) motionPlanner.HandleStateTransition(pendingTransition.Value, intent, motor.CurrentResult, phaseSnapshot);
        //给状态机输入意图切换当前的运动状态，ref是确保tick中的修改修改到了原值而不是副本
        stateController.Tick(deltaTime, ref intent);
        motionPlanner.ResolveContinuousMotion(stateController.CurrentState.GetType(), intent, motor.CurrentResult, phaseSnapshot);
        //依据数据真正的执行移动
        PlayerMotionFrame motionFrame = motionPlanner.Advance(deltaTime, intent);
        //拿到动画数据驱动时的命令
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, motionFrame, motor.CurrentResult, motor.Config, deltaTime, transform.forward);
        //执行动画移动
        PlayerLocomotionMode landingSampleMode = stateController.CurrentLocomotionMode;
        PlayerMotorResult motorResult = motor.Simulate(command, deltaTime);
        LandingSnapshot = landingTracker.Advance(motorResult, transform.position.y, landingSampleMode, stateController.TargetGroundMode, intent.DesiredMoveDirection.sqrMagnitude > 0.0001f);
        //设置移动事实
        stateController.SetSimulationFacts(motorResult, motionPlanner.Snapshot, LandingSnapshot);
        //在动画执行完毕后开始帧后状态切换
        PlayerStateTransition? resultTransition = stateController.ProcessPostTickTransition();
        //如果存在帧后切换的数据就执行一遍相同逻辑
        if (resultTransition.HasValue)
        {
            PlayerGameplayIntent postTransitionIntent = PlayerGameplayIntent.Create(desiredMoveDirection, transform.forward);
            postTransitionIntent.LocomotionMode = stateController.CurrentLocomotionMode;
            motionPlanner.HandleStateTransition(resultTransition.Value, postTransitionIntent, motorResult, phaseSnapshot);
        }
        PlayerStateTransition? presentationTransition = resultTransition ?? transition ?? pendingTransition;
        PlayerAnimationCue? landingCue = ResolveLandingCue(resultTransition, LandingSnapshot);
        pendingTransition = null;
        //播放动画表现
        animationController.Present(stateController.CurrentState.GetType(), presentationTransition, motionPlanner.Snapshot, stateController.CurrentPresentationProgress, landingCue);
        //animancer设定为手动后需要手动更新
        animationController.EvaluateGraph(deltaTime);
    }

    private static PlayerAnimationCue? ResolveLandingCue(PlayerStateTransition? transition, PlayerLandingSnapshot snapshot)
    {
        if (!snapshot.IsLandingEvent || !transition.HasValue) return null;
        PlayerStateTransition resolvedTransition = transition.Value;
        if (resolvedTransition.CurrentStateType == typeof(PlayerHardLandingState)) return PlayerAnimationCue.HardLanding;
        if (resolvedTransition.CurrentStateType == typeof(PlayerAirState) && resolvedTransition.Reason == PlayerStateTransitionReason.Jumped) return null;
        if (!IsGroundState(resolvedTransition.CurrentStateType)) return null;
        return PlayerLandingPresentationResolver.TryResolveLand(snapshot, out PlayerAnimationCue cue) ? cue : (PlayerAnimationCue?)null;
    }

    private static bool IsGroundState(Type stateType)
    {
        return stateType == typeof(PlayerIdleState) || stateType == typeof(PlayerWalkState) || stateType == typeof(PlayerRunState) || stateType == typeof(PlayerFastRunState);
    }

    private Vector3 ResolveWorldMoveDirection(Vector2 moveInput)
    {
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        if (input.sqrMagnitude > 1f) input.Normalize();
        Vector3 forward = movementReference.forward;
        Vector3 right = movementReference.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        Vector3 worldDirection = forward * input.z + right * input.x;
        return worldDirection.sqrMagnitude > 1f ? worldDirection.normalized : worldDirection;
    }
}
