using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 注册并按输入转换、状态 Tick、结果转换三个阶段更新唯一的玩家 Gameplay 状态。
/// </summary>
public class PlayerStateController : MonoBehaviour
{
    [SerializeField] private PlayerMovementConfig movementConfig;

    private readonly Dictionary<Type, PlayerStateBase> states = new Dictionary<Type, PlayerStateBase>();
    private PlayerJump playerJump;
    private PlayerDodge playerDodge;
    private PlayerContext context;
    private PlayerStateBase currentState;
    private IPlayerInputSource playerInput;
    private IPlayerActionBuffer actionBuffer;

    public PlayerStateBase CurrentState => currentState;
    public PlayerLocomotionMode CurrentLocomotionMode => currentState?.LocomotionMode ?? PlayerLocomotionMode.Idle;
    public float CurrentPresentationProgress => currentState?.PresentationProgress ?? 0f;

    private void Awake()
    {
        playerJump = GetComponent<PlayerJump>();
        playerDodge = GetComponent<PlayerDodge>();
    }
    /// <summary>
    /// 状态机的初始化设定
    /// </summary>
    public PlayerStateTransition Initialize(IPlayerInputSource inputSource, IPlayerActionBuffer inputActionBuffer)
    {
        playerInput = inputSource;
        actionBuffer = inputActionBuffer;
        context = new PlayerContext(playerJump, playerDodge, playerInput, actionBuffer, movementConfig);
        RegisterStates();
        TryChangeState(new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.Initialized), out PlayerStateTransition transition);
        return transition;
    }
    /// <summary>
    /// 设置移动数据和事实
    /// </summary>
    public void SetSimulationFacts(PlayerMotorResult motorResult, PlayerMotionSnapshot motionSnapshot, Vector3 desiredMoveDirection)
    {
        context.SetSimulationFacts(motorResult, motionSnapshot, desiredMoveDirection);
    }

    public PlayerStateTransition? ProcessPreTickTransition()
    {
        return TryChangeState(currentState?.EvaluateInputTransition(), out PlayerStateTransition transition) ? transition : (PlayerStateTransition?)null;
    }

    public void Tick(float deltaTime, ref PlayerGameplayIntent intent)
    {
        currentState?.Tick(deltaTime, ref intent);
    }

    public PlayerStateTransition? ProcessPostTickTransition()
    {
        return TryChangeState(currentState?.EvaluateResultTransition(), out PlayerStateTransition transition) ? transition : (PlayerStateTransition?)null;
    }

    private void RegisterStates()
    {
        AddState(new PlayerIdleState(context));
        AddState(new PlayerWalkState(context));
        AddState(new PlayerRunState(context));
        AddState(new PlayerFastRunState(context));
        AddState(new PlayerDodgeState(context));
        AddState(new PlayerAirState(context));
        AddState(new PlayerHardLandingState(context));
    }

    private bool TryChangeState(PlayerStateTransitionRequest request, out PlayerStateTransition transition)
    {
        transition = default;
        if (request == null)
        {
            return false;
        }
        if (!states.TryGetValue(request.TargetStateType, out PlayerStateBase nextState))
        {
            return false;
        }
        if (currentState == nextState && !request.AllowReentry)
        {
            return false;
        }

        transition = new PlayerStateTransition(currentState?.GetType(), nextState.GetType(), request.Reason, currentState?.LocomotionMode ?? PlayerLocomotionMode.Idle, nextState.LocomotionMode);
        currentState?.Exit(transition);
        currentState = nextState;
        currentState.Enter(transition);
        return true;
    }

    private void AddState<TState>(TState state) where TState : PlayerStateBase
    {
        Type stateType = state.GetType();
        if (!states.ContainsKey(stateType))
        {
            states.Add(stateType, state);
        }
    }
}
