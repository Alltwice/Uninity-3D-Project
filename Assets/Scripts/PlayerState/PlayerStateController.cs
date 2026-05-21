using System;
using UnityEngine;
using System.Collections.Generic;
/// <summary>
/// 具体管理状态机切换流程
/// </summary>
public class PlayerStateController : MonoBehaviour
{
    //泛型字典管理具体状态类，同时使用readonly防止运行过程中修改
    private readonly Dictionary<Type,PlayerStateBase> states=new Dictionary<Type, PlayerStateBase>();
    //获取玩家当前引用和信息，负责调用实际行为逻辑和供具体状态类引用
    private PlayerInfoProvider contextProvider;
    private PlayerContext context;
    private PlayerStateBase currentState;
    //利用属性封装外部可读不可改
    public PlayerStateBase CurrentState => currentState;

    private void Awake()
    {
        //获取组件信息和玩家信息
        contextProvider=GetComponent<PlayerInfoProvider>();
        context = contextProvider.Context;
        //new具体状态类同时注入信息
        RegisterState();
    }

    private void Start()
    {
        //一开始就设定为待机状态，填入的泛型参数即为你想切换的状态
        ChangeState<PlayerIdleState>();
    }

    private void Update()
    {
        //处理状态切换逻辑
        HandleStateTransitions();
        //执行当前状态类中的刷新逻辑
        currentState?.Tick();
    }
//——————————————————————————————————————————————————————————————调用方法————————————————————————————

    private void HandleStateTransitions()
    {
        //当前状态为空不切换
        if (currentState != null)
        {
            return;
        }
        //当前状态上锁不切换
        if (!currentState.CanExit())
        {
            return;
        }

        if (currentState is PlayerIdleState)
        {
            
        }

        if (currentState is PlayerGroundMoveState)
        {
            
        }
    }
    private void RegisterState()
    {
        //注册方法，调用添加字典方法，同时完成new操作触发构造函数，传入context值
        AddState(new PlayerIdleState(context));
        AddState(new PlayerGroundMoveState(context));
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
        //这里没有传入任何参数，但是能通过函数调用时<>中的内容推断类型
        Type nextStateType = typeof(TState);
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
}
