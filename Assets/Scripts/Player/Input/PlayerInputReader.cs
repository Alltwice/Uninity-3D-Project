using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 具体输入读取类，实现接口能力
/// </summary>
public class PlayerInputReader : MonoBehaviour,IPlayerInputSource
{
    //引用自动创建输入脚本
    private InputSystem_Actions inputActions;
    private IPlayerActionBuffer actionBuffer;
    //承接接口内容
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool IsWalkMode { get; private set; }
    public bool IsSprintHeld { get; private set; }
    //Awake时新建系统输入脚本
    private void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    public void Init(IPlayerActionBuffer actionBuffer)
    {
        this.actionBuffer = actionBuffer;
    }
    //启用时启用对应输入和订阅
    private void OnEnable()
    {
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
        }
        RegisterInputCallbacks();
        inputActions.Player.Enable();
    }
    //关闭时关闭对应输入和订阅
    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Disable();
            UnregisterInputCallbacks();
        }

        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        IsWalkMode = false;
        IsSprintHeld = false;
        actionBuffer?.Clear(PlayerBufferedAction.Jump);
    }
    //当前对象被销毁后系统自动创建对象同时销毁
    private void OnDestroy()
    {
        inputActions?.Dispose();
    }
    /// <summary>
    /// 订阅输入事件
    /// </summary>
    private void RegisterInputCallbacks()
    {
        inputActions.Player.Move.performed += OnMovePerformed;
        inputActions.Player.Move.canceled += OnMoveCanceled;
        inputActions.Player.Look.performed += OnLookPerformed;
        inputActions.Player.Look.canceled += OnLookCanceled;
        inputActions.Player.Jump.started += OnJumpStarted;
        inputActions.Player.Sprint.started += OnSprintStarted;
        inputActions.Player.Sprint.canceled += OnSprintCanceled;
        inputActions.Player.WalkToggle.started += OnWalkToggleStarted;
    }
    /// <summary>
    /// 解绑输入事件
    /// </summary>
    private void UnregisterInputCallbacks()
    {
        inputActions.Player.Move.performed-= OnMovePerformed;
        inputActions.Player.Move.canceled -= OnMoveCanceled;
        inputActions.Player.Look.performed -= OnLookPerformed;
        inputActions.Player.Look.canceled -= OnLookCanceled;
        inputActions.Player.Jump.started -= OnJumpStarted;
        inputActions.Player.Sprint.started -= OnSprintStarted;
        inputActions.Player.Sprint.canceled -= OnSprintCanceled;
        inputActions.Player.WalkToggle.started -= OnWalkToggleStarted;
    }
    //以下为处理按下和松开时的数据
    private void OnMovePerformed(InputAction.CallbackContext ctx)=> MoveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx)=> MoveInput = Vector2.zero;
    private void OnLookPerformed(InputAction.CallbackContext ctx)=> LookInput = ctx.ReadValue<Vector2>();
    private void OnLookCanceled(InputAction.CallbackContext ctx)=> LookInput = Vector2.zero;
    private void OnSprintStarted(InputAction.CallbackContext ctx) => IsSprintHeld = true;
    //奔跑结束后切换会跑步模式
    private void OnSprintCanceled(InputAction.CallbackContext ctx)
    {
        IsSprintHeld = false;
        IsWalkMode = false;
    }
    //切换跑步和行走同时忽略奔跑时
    private void OnWalkToggleStarted(InputAction.CallbackContext ctx)
    {
        if (IsSprintHeld)
        {
            return;
        }

        IsWalkMode = !IsWalkMode;
    }
    //添加输入缓冲
    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        actionBuffer.Buffer(PlayerBufferedAction.Jump);
    }
}
