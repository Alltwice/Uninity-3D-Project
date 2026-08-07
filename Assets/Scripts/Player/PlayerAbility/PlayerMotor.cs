using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum PlayerLocomotionMode
{
    Idle,
    Walk,
    Run,
    FastRun,
    Air
}

/// <summary>
/// 在拿到输入数据之后具体要处理的输入内容
/// </summary>
public class PlayerMotor : MonoBehaviour
{
    [Header("移动速度")]
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float fastRunSpeed = 7.5f;
    [SerializeField] private float airMoveSpeed = 2f;
    [Header("移动加速度")] 
    [SerializeField] private float groundAcceleration = 35f;
    [SerializeField] private float groundDeceleration = 45f;
    [SerializeField] private float groundTurnAcceleration = 80f;
    [SerializeField] private float airAcceleration = 8f;
    [Header("其他")]
    [Tooltip("变向时旋转速度")]
    [SerializeField] private float rotationSmoothSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("为了让角色稳稳压在地上给一个向下的速度")]
    [SerializeField] private float groundedVerticalVelocity = -2f;
    [Header("落地判定")]
    [SerializeField] private float hardLandingMinImpactSpeed = 10f;
    //能够处理碰撞，斜坡，台阶，贴地
    private CharacterController characterController;
    private PlayerGroundProbe groundProbe;
    private IPlayerInputSource inputSource;
    private Transform cameraTransform;
    //主要用于处理角色竖直方向上的下落和跳跃
    private Vector3 verticalVelocity;
    private Vector3 horizontalVelocity;
    private bool isGrounded;
    //给外部暴露读取的数据
    public float MoveSpeed => CurrentTargetSpeed;
    public float CurrentTargetSpeed { get; private set; }
    public PlayerLocomotionMode CurrentLocomotionMode { get; private set; } = PlayerLocomotionMode.Idle;
    //获取向量长度
    public float HorizontalSpeed => new Vector3(horizontalVelocity.x, 0, horizontalVelocity.z).magnitude;
    public IPlayerInputSource InputSource => inputSource;
    public float VerticalSpeed => verticalVelocity.y;
    public bool JustLanded { get; private set; }
    public float LandingImpactSpeed { get; private set; }
    public bool IsHardLandingImpact => JustLanded && LandingImpactSpeed >= hardLandingMinImpactSpeed;

    /// <summary>
    /// 给外部暴露修改高度和应用重力
    /// </summary>
    /// <param name="value">传入高度参数</param>
    public void ChangeVerticalVelocity_y(float value)
    {
        verticalVelocity.y =value;
    }
    public bool IsGrounded => isGrounded;
    public float Gravity => gravity;
    private void Awake()
    {
        characterController=GetComponent<CharacterController>();
        groundProbe = GetComponent<PlayerGroundProbe>();
        isGrounded = characterController.isGrounded;
        cameraTransform = Camera.main.transform;
    }

    private void Start()
    {
        groundProbe.Refresh();
        isGrounded = characterController.isGrounded || groundProbe.CanSnapToGround;
    }
    /// <summary>
    /// 对外部暴露主动依赖函数，等待被组合脚本调用后注入
    /// </summary>
    public void Init(IPlayerInputSource inputSource)
    {
        //给当前脚本中的内容赋值
        this.inputSource = inputSource;
    }
    //——————————————————————————————————————主要方法————————————————————————————————————————————————
    /// <summary>
    /// 处理角大角度变相的速度问题
    /// </summary>
    /// <param name="moveDirection"></param>
    private void UpdateGroundHorizontalVelocity(Vector3 moveDirection)
    {
        Vector3 targetVelocity = moveDirection * CurrentTargetSpeed;

        float acceleration = groundAcceleration;

        if (horizontalVelocity.sqrMagnitude > 0.001f &&
            targetVelocity.sqrMagnitude > 0.001f)
        {
            float alignment = Vector3.Dot(
                horizontalVelocity.normalized,
                targetVelocity.normalized);

            if (alignment < 0.8f)
            {
                // 明显变向时快速修正速度方向。
                acceleration = groundTurnAcceleration;
            }
            else if (horizontalVelocity.magnitude > targetVelocity.magnitude)
            {
                // 例如 FastRun 7.5 -> Run 5。
                acceleration = groundDeceleration;
            }
        }

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetVelocity,
            acceleration * Time.deltaTime);
    }
    /// <summary>
    /// 处理移动逻辑
    /// </summary>
    public void Move()
    {
        Vector3 moveDirection = GetInputDirection();

        CurrentLocomotionMode = ResolveGroundLocomotionMode();
        CurrentTargetSpeed = GetSpeed(CurrentLocomotionMode);

        ApplyGravity();

        UpdateGroundHorizontalVelocity(moveDirection);

        Vector3 finalVelocity = horizontalVelocity + verticalVelocity;

        MoveCharacter(finalVelocity);
        RotateToMoveDirection(moveDirection);
    }
    /// <summary>
    /// 处理空中移动
    /// </summary>
    public void AirMove()
    {
        Vector3 moveDirection = GetInputDirection();
        CurrentLocomotionMode = PlayerLocomotionMode.Air;
        CurrentTargetSpeed = GetSpeed(CurrentLocomotionMode);
        ApplyGravity();
        Vector3 targetHorizontalVelocity = moveDirection * CurrentTargetSpeed;
        horizontalVelocity=Vector3.MoveTowards(horizontalVelocity, targetHorizontalVelocity, airAcceleration * Time.deltaTime);
        Vector3 finalVelocity = horizontalVelocity + verticalVelocity;
        MoveCharacter(finalVelocity);
        RotateToMoveDirection(moveDirection);
    }
    /// <summary>
    /// 移动时有一个减速效果
    /// </summary>
    public void IdleMove()
    {
        CurrentLocomotionMode = PlayerLocomotionMode.Idle;
        CurrentTargetSpeed = 0f;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, groundDeceleration * Time.deltaTime);
        ApplyGravity();
        Vector3 finalVelocity = horizontalVelocity + verticalVelocity;
        MoveCharacter(finalVelocity);
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
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = groundedVerticalVelocity;
            return;
        }
        verticalVelocity.y += gravity * Time.deltaTime;
    }
    /// <summary>
    /// 移动辅助方法
    /// </summary>
    /// <param name="velocity">传入最终真实移动速度</param>
    private void MoveCharacter(Vector3 velocity)
    {
        //判断角色先前是否接地
        bool wasGrounded = isGrounded;
        float downwardSpeedBeforeMove = Mathf.Max(0f, -verticalVelocity.y);
        JustLanded = false;

        //CC移动时会返回一个是否有碰撞的信息
        CollisionFlags collisionFlags = characterController.Move(velocity * Time.deltaTime);
        bool controllerGrounded = (collisionFlags & CollisionFlags.Below) != 0 || characterController.isGrounded;

        groundProbe.Refresh();
        bool snappedToGround =
            !controllerGrounded &&
            wasGrounded &&
            verticalVelocity.y <= 0f &&
            groundProbe.CanSnapToGround;

        if (snappedToGround)
        {
            characterController.Move(-transform.up * groundProbe.GroundDistance);
            groundProbe.Refresh();
        }

        //确认是接地才会将isGround设定为地面并持续施加向下的力
        isGrounded = controllerGrounded || snappedToGround;
        if (!wasGrounded && isGrounded)
        {
            JustLanded = true;
            LandingImpactSpeed = downwardSpeedBeforeMove;
        }

        if (isGrounded && verticalVelocity.y < groundedVerticalVelocity)
        {
            verticalVelocity.y = groundedVerticalVelocity;
        }
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
    /// <summary>
    /// 从输入源中获取输入方向并将其向量化
    /// </summary>
    /// <returns></returns>
    private Vector3 GetInputDirection()
    {
        //获取输入方向
        Vector2 moveInput = inputSource.MoveInput;
        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }
        Vector3 moveDirection = GetCameraMoveDir(inputDirection);
        return moveDirection;
    }

    private PlayerLocomotionMode ResolveGroundLocomotionMode()
    {
        if (inputSource.IsSprintHeld)
        {
            return PlayerLocomotionMode.FastRun;
        }

        return inputSource.IsWalkMode ? PlayerLocomotionMode.Walk : PlayerLocomotionMode.Run;
    }

    private float GetSpeed(PlayerLocomotionMode locomotionMode)
    {
        switch (locomotionMode)
        {
            case PlayerLocomotionMode.Walk:
                return walkSpeed;
            case PlayerLocomotionMode.FastRun:
                return fastRunSpeed;
            case PlayerLocomotionMode.Run:
                return runSpeed;
            case PlayerLocomotionMode.Air:
                return airMoveSpeed;
            default:
                return 0f;
        }
    }
}
