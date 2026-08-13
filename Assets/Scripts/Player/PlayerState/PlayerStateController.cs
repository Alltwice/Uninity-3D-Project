using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 注册并按输入转换、状态 Tick、结果转换三个阶段更新唯一的玩家 Gameplay 状态。
/// </summary>
public class PlayerStateController : MonoBehaviour
{
    private readonly Dictionary<Type, PlayerStateBase> states = new Dictionary<Type, PlayerStateBase>();
    private PlayerMotor playerMotor;
    private PlayerJump playerJump;
    private PlayerDodge playerDodge;
    private PlayerAnimationController animationController;
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
        playerMotor = GetComponent<PlayerMotor>();
        playerJump = GetComponent<PlayerJump>();
        playerDodge = GetComponent<PlayerDodge>();
        animationController = GetComponent<PlayerAnimationController>();
    }

    private void Start()
    {
        context = new PlayerContext(playerMotor, playerJump, playerDodge, animationController, playerInput, actionBuffer);
        RegisterStates();
        TryChangeState(new PlayerStateTransitionRequest(typeof(PlayerIdleState), PlayerStateTransitionReason.Initialized));
    }

    private void Update()
    {
        //一次输入采样
        SampleMoveIntent();
        ProcessPreTickTransition();
        currentState?.Tick();
        ProcessPostTickTransition();
    }

    private void SampleMoveIntent()
    {
        playerMotor.SetDesiredMoveDirection(playerMotor.GetWorldMoveDirection(playerInput.MoveInput));
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
        AddState(new PlayerWalkState(context));
        AddState(new PlayerRunState(context));
        AddState(new PlayerFastRunState(context));
        AddState(new PlayerDodgeState(context));
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

        PlayerStateTransition transition = new PlayerStateTransition(currentState?.GetType(), nextState.GetType(), request.Reason);
        currentState?.Exit(transition);
        currentState = nextState;
        currentState.Enter(transition);
        animationController.PlayTransition(transition);
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
