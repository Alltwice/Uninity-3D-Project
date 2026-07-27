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
    //Awake时新建系统输入脚本
    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        actionBuffer = GetComponent<PlayerActionBuffer>();
    }

    public void Init(IPlayerActionBuffer actionBuffer)
    {
        this.actionBuffer = actionBuffer;
    }
    //启用时启用对应输入和订阅
    private void OnEnable()
    {
        RegisterInputCallbacks();
        inputActions.Player.Enable();
    }
    //关闭时关闭对应输入和订阅
    private void OnDisable()
    {
        inputActions.Player.Disable();
        UnregisterInputCallbacks();
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        actionBuffer.Clear(PlayerBufferedAction.Jump);
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
    }
    //以下为处理按下和松开时的数据
    private void OnMovePerformed(InputAction.CallbackContext ctx)=> MoveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx)=> MoveInput = Vector2.zero;
    private void OnLookPerformed(InputAction.CallbackContext ctx)=> LookInput = ctx.ReadValue<Vector2>();
    private void OnLookCanceled(InputAction.CallbackContext ctx)=> LookInput = Vector2.zero;
    private void OnJumpStarted(InputAction.CallbackContext ctx)=> actionBuffer.Buffer(PlayerBufferedAction.Jump);
}
