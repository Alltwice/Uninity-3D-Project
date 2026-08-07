using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 管理玩家状态注册，并按输入转换、状态行为、结果转换三个阶段更新状态机。
/// </summary>
public class PlayerStateController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private PlayerJump playerJump;
    [FormerlySerializedAs("animationDriver")]
    [SerializeField] private PlayerAnimationController animationController;

    private readonly Dictionary<Type, PlayerStateBase> states = new Dictionary<Type, PlayerStateBase>();
    private PlayerContext context;
    private PlayerStateBase currentState;
    private IPlayerInputSource playerInput;
    private IPlayerActionBuffer actionBuffer;

    public PlayerStateBase CurrentState => currentState;

    public void Init(IPlayerInputSource playerInput, IPlayerActionBuffer actionBuffer)
    {
        this.playerInput = playerInput;
        this.actionBuffer = actionBuffer;
    }

    private void Awake()
    {
        animationController = GetComponent<PlayerAnimationController>();
        playerMotor = GetComponent<PlayerMotor>();
        playerJump = GetComponent<PlayerJump>();
    }

    private void Start()
    {
        context = new PlayerContext(
            playerMotor,
            playerJump,
            animationController,
            playerInput,
            actionBuffer);

        RegisterStates();
        TryChangeState(new PlayerStateTransitionRequest(
            typeof(PlayerIdleState),
            PlayerStateTransitionReason.Initialized));
    }

    private void Update()
    {
        ProcessPreTickTransition();
        currentState?.Tick();
        ProcessPostTickTransition();
    }

    private void ProcessPreTickTransition()
    {
        TryChangeState(currentState?.EvaluateInputTransition());
    }

    private void ProcessPostTickTransition()
    {
        TryChangeState(currentState?.EvaluateResultTransition());
    }

    private void RegisterStates()
    {
        AddState(new PlayerIdleState(context));
        AddState(new PlayerGroundMoveState(context));
        AddState(new PlayerAirState(context));
        AddState(new PlayerHardLandingState(context));
    }

    private bool TryChangeState(PlayerStateTransitionRequest request)
    {
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

        PlayerStateTransition transition = new PlayerStateTransition(
            currentState?.GetType(),
            nextState.GetType(),
            request.Reason,
            playerMotor.CurrentLocomotionMode);

        currentState?.Exit(transition);
        currentState = nextState;
        currentState.Enter(transition);
        return true;
    }

    private void AddState<TState>(TState state) where TState : PlayerStateBase
    {
        Type stateType = state.GetType();
        if (states.ContainsKey(stateType))
        {
            return;
        }

        states.Add(stateType, state);
    }
}
