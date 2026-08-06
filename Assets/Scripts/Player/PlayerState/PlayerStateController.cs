using System;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
/// <summary>
/// 具体管理状态机切换流程
/// </summary>
public class PlayerStateController : MonoBehaviour
{
    [Header(("引用"))]
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private PlayerJump playerJump;
    [FormerlySerializedAs("animationDriver")]
    [SerializeField] private PlayerAnimationController animationController;
    //泛型字典管理具体状态类，同时使用readonly防止运行过程中修改
    private readonly Dictionary<Type, PlayerStateBase> states=new Dictionary<Type, PlayerStateBase>();
    //获取玩家当前引用和信息，负责调用实际行为逻辑和供具体状态类引用
    private PlayerContext context;
    private PlayerStateBase currentState;
    private IPlayerInputSource playerInput;
    private IPlayerActionBuffer actionBuffer;
    //利用属性封装外部可读不可改
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
        //存入引用即可
        context = new PlayerContext(
            playerMotor,
            playerJump,
            animationController,
            playerInput,
            actionBuffer);
        //获取组件信息和玩家信息
        RegisterState();
        //一开始就设定为待机状态，填入的泛型参数即为你想切换的状态
        ChangeState<PlayerIdleState>();
    }
    private void Update()
    {
        //读取当前状态给出的目标类型，并交给唯一的核心切换逻辑处理
        ChangeState(currentState?.GetNextStateType());
        //执行当前状态类中的刷新逻辑
        currentState?.Tick();
    }
//——————————————————————————————————————————————————————————————主要方法————————————————————————————

    private void RegisterState()
    {
        //注册方法，调用添加字典方法，同时完成new操作触发构造函数，传入context值
        AddState(new PlayerIdleState(context));
        AddState(new PlayerGroundMoveState(context));
        AddState(new PlayerAirState(context));
        AddState(new PlayerHardLandingState(context));
    }
    /// <summary>
    /// 根据传入的类型切换状态
    /// </summary>
    /// <param name="nextStateType">目标状态类型，null表示保持当前状态</param>
    private void ChangeState(Type nextStateType)
    {
        if (nextStateType == null)
        {
            return;
        }

        //如果尝试获取失败可能为未加入不切换,同时无论成功失败与否，都会将最终内容out到nextState当中供使用
        if (!states.TryGetValue(nextStateType, out PlayerStateBase nextState))
        {
            return;
        }
        //重复状态不切换
        if (currentState == nextState)
        {
            return;
        }
        //如果当前状态不为空的同时状态锁为锁定则不切换状态
        if (currentState != null&&!currentState.CanExit())
        {
            return;
        }
        //退出当前状态，切换下一个状态
        currentState?.Exit();
        currentState = nextState;
        currentState?.Enter();
    }
    //————————————————————————————————————————————————辅助方法————————————————————————————————————————————
    /// <summary>
    /// 注册状态实例
    /// </summary>
    /// <param name="state">传入状态类</param>
    /// <typeparam name="TState">泛型存储状态类</typeparam>
    private void AddState<TState>(TState state) where TState : PlayerStateBase
    {
        //获取具体的状态数据类型
        Type stateType = state.GetType();
        //存在不加入
        if (states.ContainsKey(stateType))
        {
            return;
        }
        //不存在则加入，前者为获取到的具体数据信息为键，后者同样为数据类型，但可取用
        states.Add(stateType, state);
    }
    /// <summary>
    /// 切换状态类
    /// </summary>
    /// <typeparam name="TState">泛型存储状态类</typeparam>
    private void ChangeState<TState>() where TState : PlayerStateBase
    {
        ChangeState(typeof(TState));
    }
}
