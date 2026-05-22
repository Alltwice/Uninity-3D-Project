using System;
using UnityEngine;

/// <summary>
/// 在拿到输入数据之后具体要处理的输入内容
/// </summary>
public class PlayerMotor : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("变向时旋转速度")]
    [SerializeField] private float rotationSmoothSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("为了让角色稳稳压在地上给一个向下的速度")]
    [SerializeField] private float groundedVerticalVelocity = -2f;
    //能够处理碰撞，斜坡，台阶，贴地
    private CharacterController characterController;
    private IPlayerInputSource inputSource;
    public IPlayerInputSource InputSource => inputSource;
    private Transform cameraTransform;
    //主要用于处理角色竖直方向上的下落和跳跃
    private Vector3 verticalVelocity;

    private void Awake()
    {
        characterController=GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    /// <summary>
    /// 对外部暴露主动依赖函数，等待被组合脚本调用后注入
    /// </summary>
    public void Init(IPlayerInputSource inputSource)
    {
        //给当前脚本中的内容赋值
        this.inputSource = inputSource;
    }
    //——————————————————————————————————————调用方法————————————————————————————————————————————————
    
    /// <summary>
    /// 处理移动逻辑
    /// </summary>
    public void Move()
    {
        //获取外部的位移信息
        Vector2 moveInput = inputSource.MoveInput;
        //将二维输入转为三维位置信息
        Vector3 inputDirection = new Vector3(moveInput.x, 0, moveInput.y);
        //处理斜向移动和可能的遥感百分比移速问题
        if(inputDirection.sqrMagnitude>1f)
        {
            inputDirection.Normalize();
        }
        //处理相机朝向和输入力度之后确定移动方向为摄像机朝向位置
        Vector3 moveDirection = GetCameraMoveDir(inputDirection);
        ApplyGravity();
        //确定水平速度
        Vector3 horizontalVelocity = moveDirection * moveSpeed;
        //确定最终方向
        Vector3 finalVelocity = horizontalVelocity+verticalVelocity;
        //移动
        characterController.Move(finalVelocity * Time.deltaTime);
        //旋转角色向移动方向
        RotateToMoveDirection(moveDirection);

    }
    //——————————————————————————————————————辅助方法——————————————————————————————————————————————
    
    /// <summary>
    /// 获取当前摄像机在二维平面上的朝向将其作为角色移动方向
    /// </summary>
    /// <param name="inputDirection">获取当前玩家输入方向</param>
    /// <returns>返回最终移动朝向</returns>
    private Vector3 GetCameraMoveDir(Vector3 inputDirection)
    {
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        //清理掉摄像机的竖直分量
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();
        //最终移动方向=相机前后方向*前后输入力度+相机左右方向*左右输入力度
        Vector3 moveDirection = cameraForward * inputDirection.z + cameraRight * inputDirection.x;
        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }
        return moveDirection;
    }
    /// <summary>
    /// 处理重力和将角色压在地面上
    /// </summary>
    private void ApplyGravity()
    {
        //确保当前始终有力压着角色且不会累计
        if (characterController.isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = groundedVerticalVelocity;
        }
        verticalVelocity.y += gravity * Time.deltaTime;
    }
    /// <summary>
    /// 处理角色向不同方向移动时的躯体旋转
    /// </summary>
    /// <param name="moveDirection">传入当前移动方向</param>
    private void RotateToMoveDirection(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude < 0.001f)
        {
            return;
        }
        //此处不能传入0值，前面的if判断在此生效
        //创建旋转让角色方向对准moveDirection
        Quaternion targetRotation=Quaternion.LookRotation(moveDirection);
        //做线性插值旋转，最后一个参数值越大越接近于b值
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.deltaTime);
    }
}
