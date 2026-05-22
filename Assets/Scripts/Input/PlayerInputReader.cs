using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 具体输入读取类，实现接口能力
/// </summary>
public class PlayerInputReader : MonoBehaviour,IPlayerInputSource
{
    //引用自动创建输入脚本
    private InputSystem_Actions _inputActions;
    //承接接口内容
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    private bool jumpPressed;
    //Awake时新建系统输入脚本
    private void Awake()
    {
        _inputActions = new InputSystem_Actions();
    }
    //启用时启用对应输入和订阅
    private void OnEnable()
    {
        RegisterInputCallbacks();
        _inputActions.Player.Enable();
    }
    //关闭时关闭对应输入和订阅
    private void OnDisable()
    {
        _inputActions.Player.Disable();
        UnregisterInputCallbacks();
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
    }
    //当前对象被销毁后系统自动创建对象同时销毁
    private void OnDestroy()
    {
        _inputActions?.Dispose();
    }
/// <summary>
/// 按下跳跃后传入true
/// 如果为true将其设定为false，向外部判定跳跃成功，否则失败
/// </summary>
/// <returns></returns>
    public bool ConsumeJumpPressed()
    {
        if (!jumpPressed)
        {
            return false;
        }

        jumpPressed = false;
        return true;
    }
    /// <summary>
    /// 订阅输入事件
    /// </summary>
    private void RegisterInputCallbacks()
    {
        _inputActions.Player.Move.performed += OnMovePerformed;
        _inputActions.Player.Move.canceled += OnMoveCanceled;
        _inputActions.Player.Look.performed += OnLookPerformed;
        _inputActions.Player.Look.canceled += OnLookCanceled;
        _inputActions.Player.Jump.performed += OnJumpPerformed;
    }
    /// <summary>
    /// 解绑输入事件
    /// </summary>
    private void UnregisterInputCallbacks()
    {
        _inputActions.Player.Move.performed-= OnMovePerformed;
        _inputActions.Player.Move.canceled -= OnMoveCanceled;
        _inputActions.Player.Look.performed -= OnLookPerformed;
        _inputActions.Player.Look.canceled -= OnLookCanceled;
        _inputActions.Player.Jump.performed -= OnJumpPerformed;
    }
    //以下为处理按下和松开时的数据
    private void OnMovePerformed(InputAction.CallbackContext ctx)=> MoveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx)=> MoveInput = Vector2.zero;
    private void OnLookPerformed(InputAction.CallbackContext ctx)=> LookInput = ctx.ReadValue<Vector2>();
    private void OnLookCanceled(InputAction.CallbackContext ctx)=> LookInput = Vector2.zero;
    private void OnJumpPerformed(InputAction.CallbackContext ctx) => jumpPressed = true;
}
