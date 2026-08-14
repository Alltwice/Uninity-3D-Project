using UnityEngine;

public enum PlayerMotionMode
{
    CodeDriven,
    AnimationDriven
}
//支持组合使用
[System.Flags]
public enum AnimationMotionChannels
{
    //一次移动一位，为了确保占不同bit
    None = 0,
    Translation = 1 << 0,
    Rotation = 1 << 1
}

/// <summary>
/// 执行 CharacterController 移动、速度连续性、重力、碰撞与接地结果
/// </summary>
public class PlayerMotor : MonoBehaviour
{
    [Header("移动配置")]
    [SerializeField] private PlayerMotorConfig config;

    private CharacterController characterController;
    private PlayerGroundProbe groundProbe;
    private Transform cameraTransform;
    private Vector3 verticalVelocity;
    private Vector3 horizontalVelocity;
    private Vector3 desiredMoveDirection;
    private AnimationMotionChannels animationMotionChannels;
    private bool redirectAnimationMotionToDesiredDirection;
    private bool isGrounded;

    public PlayerMotionMode MotionMode { get; private set; }
    public float HorizontalSpeed => new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
    public Vector3 DesiredMoveDirection => desiredMoveDirection;
    public Vector3 HorizontalMoveDirection => horizontalVelocity.sqrMagnitude > 0.001f ? horizontalVelocity.normalized : Vector3.zero;
    public float VerticalSpeed => verticalVelocity.y;
    public bool JustLanded { get; private set; }
    public float LandingImpactSpeed { get; private set; }
    public bool IsHardLandingImpact => JustLanded && LandingImpactSpeed >= config.HardLandingMinImpactSpeed;
    public bool IsGrounded => isGrounded;
    public float Gravity => config.Gravity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        groundProbe = GetComponent<PlayerGroundProbe>();
        isGrounded = characterController.isGrounded;
        cameraTransform = Camera.main.transform;
    }

    private void Start()
    {
        groundProbe.Refresh();
        isGrounded = characterController.isGrounded || groundProbe.CanSnapToGround;
    }

    public void ChangeVerticalVelocity_y(float value)
    {
        verticalVelocity.y = value;
    }

    public void WalkMove(Vector3 moveDirection)
    {
        if (MotionMode != PlayerMotionMode.CodeDriven) return;
        MoveGround(moveDirection, config.WalkSpeed);
    }

    public void RunMove(Vector3 moveDirection)
    {
        if (MotionMode != PlayerMotionMode.CodeDriven) return;
        MoveGround(moveDirection, config.RunSpeed);
    }

    public void FastRunMove(Vector3 moveDirection)
    {
        if (MotionMode != PlayerMotionMode.CodeDriven) return;
        MoveGround(moveDirection, config.FastRunSpeed);
    }

    public void AirMove(Vector3 moveDirection)
    {
        if (MotionMode != PlayerMotionMode.CodeDriven) return;
        ApplyGravity();
        Vector3 targetHorizontalVelocity = moveDirection * config.AirMoveSpeed;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetHorizontalVelocity, config.AirAcceleration * Time.deltaTime);
        MoveCharacterVelocity(horizontalVelocity + verticalVelocity);
        RotateToMoveDirection(moveDirection);
    }

    public void IdleMove()
    {
        if (MotionMode != PlayerMotionMode.CodeDriven) return;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, config.GroundDeceleration * Time.deltaTime);
        ApplyGravity();
        MoveCharacterVelocity(horizontalVelocity + verticalVelocity);
    }

    public void DodgeMove(Vector3 direction, float horizontalDistance)
    {
        if (MotionMode != PlayerMotionMode.CodeDriven) return;
        direction.y = 0f;
        direction.Normalize();
        ApplyGravity();
        Vector3 positionBeforeMove = transform.position;
        Vector3 displacement = direction * horizontalDistance + verticalVelocity * Time.deltaTime;
        MoveCharacterDisplacement(displacement);
        UpdateHorizontalVelocityFromActualDisplacement(positionBeforeMove);
        if (direction.sqrMagnitude > 0.001f)
        {
            RotateToMoveDirection(direction);
        }
    }

    public void SetMotionMode(PlayerMotionMode motionMode, AnimationMotionChannels channels = AnimationMotionChannels.None, bool redirectToDesiredDirection = false)
    {
        MotionMode = motionMode;
        animationMotionChannels = motionMode == PlayerMotionMode.AnimationDriven ? channels : AnimationMotionChannels.None;
        redirectAnimationMotionToDesiredDirection = motionMode == PlayerMotionMode.AnimationDriven && redirectToDesiredDirection;
    }

    public void SubmitAnimationMotion(Vector3 deltaPosition, Quaternion deltaRotation)
    {
        if (MotionMode != PlayerMotionMode.AnimationDriven) return;
        ApplyGravity();
        Vector3 positionBeforeMove = transform.position;
        Vector3 horizontalDisplacement = Vector3.zero;
        //通过每个枚举占一位1，并通过位与运算判断你现在这个动画的权限和转向权限是否有一者没占位，如果是，最终结果为0，条件不满足
        if ((animationMotionChannels & AnimationMotionChannels.Translation) != 0)
        {
            Vector3 horizontalRootMotion = new Vector3(deltaPosition.x, 0f, deltaPosition.z);
            horizontalDisplacement = redirectAnimationMotionToDesiredDirection ? desiredMoveDirection * horizontalRootMotion.magnitude : horizontalRootMotion;
        }
        MoveCharacterDisplacement(horizontalDisplacement + verticalVelocity * Time.deltaTime);
        UpdateHorizontalVelocityFromActualDisplacement(positionBeforeMove);
        if ((animationMotionChannels & AnimationMotionChannels.Rotation) != 0)
        {
            transform.rotation *= deltaRotation;
        }
        else if (redirectAnimationMotionToDesiredDirection)
        {
            RotateToMoveDirection(desiredMoveDirection);
        }
    }
    
    public void RotateTowardsDesiredDirection()
    {
        RotateToMoveDirection(desiredMoveDirection);
    }

    public Vector3 GetWorldMoveDirection(Vector2 moveInput)
    {
        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }
        return GetCameraMoveDirection(inputDirection);
    }

    private void MoveGround(Vector3 moveDirection, float targetSpeed)
    {
        ApplyGravity();
        UpdateGroundHorizontalVelocity(moveDirection, targetSpeed);
        MoveCharacterVelocity(horizontalVelocity + verticalVelocity);
        RotateToMoveDirection(moveDirection);
    }
    /// <summary>
    /// 获取期望输入方向
    /// </summary>
    public void SetDesiredMoveDirection(Vector3 moveDirection)
    {
        moveDirection.y = 0f;
        desiredMoveDirection = moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : Vector3.zero;
    }
    /// <summary>
    /// 处理移动时大角度转向
    /// </summary>
    private void UpdateGroundHorizontalVelocity(Vector3 moveDirection, float targetSpeed)
    {
        Vector3 targetVelocity = moveDirection * targetSpeed;
        float acceleration = config.GroundAcceleration;
        if (horizontalVelocity.sqrMagnitude > 0.001f && targetVelocity.sqrMagnitude > 0.001f)
        {
            float alignment = Vector3.Dot(horizontalVelocity.normalized, targetVelocity.normalized);
            if (alignment < 0.8f)
            {
                acceleration = config.GroundTurnAcceleration;
            }
            else if (horizontalVelocity.magnitude > targetVelocity.magnitude)
            {
                acceleration = config.GroundDeceleration;
            }
        }
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * Time.deltaTime);
    }

    private Vector3 GetCameraMoveDirection(Vector3 inputDirection)
    {
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();
        Vector3 moveDirection = cameraForward * inputDirection.z + cameraRight * inputDirection.x;
        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }
        return moveDirection;
    }

    private void ApplyGravity()
    {
        if (isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = config.GroundedVerticalVelocity;
            return;
        }
        verticalVelocity.y += config.Gravity * Time.deltaTime;
    }

    private void MoveCharacterVelocity(Vector3 velocity)
    {
        MoveCharacterDisplacement(velocity * Time.deltaTime);
    }
    /// <summary>
    /// 最终实际移动逻辑
    /// </summary>
    private void MoveCharacterDisplacement(Vector3 displacement)
    {
        bool wasGrounded = isGrounded;
        float downwardSpeedBeforeMove = Mathf.Max(0f, -verticalVelocity.y);
        JustLanded = false;
        CollisionFlags collisionFlags = characterController.Move(displacement);
        bool controllerGrounded = (collisionFlags & CollisionFlags.Below) != 0 || characterController.isGrounded;
        groundProbe.Refresh();
        bool snappedToGround = !controllerGrounded && wasGrounded && verticalVelocity.y <= 0f && groundProbe.CanSnapToGround;
        if (snappedToGround)
        {
            characterController.Move(-transform.up * groundProbe.GroundDistance);
            groundProbe.Refresh();
        }
        isGrounded = controllerGrounded || snappedToGround;
        if (!wasGrounded && isGrounded)
        {
            JustLanded = true;
            LandingImpactSpeed = downwardSpeedBeforeMove;
        }
        if (isGrounded && verticalVelocity.y < config.GroundedVerticalVelocity)
        {
            verticalVelocity.y = config.GroundedVerticalVelocity;
        }
    }
    /// <summary>
    /// 路程/时间＝速度
    /// </summary>
    private void UpdateHorizontalVelocityFromActualDisplacement(Vector3 positionBeforeMove)
    {
        Vector3 actualHorizontalDisplacement = transform.position - positionBeforeMove;
        actualHorizontalDisplacement.y = 0f;
        horizontalVelocity = Time.deltaTime > 0f ? actualHorizontalDisplacement / Time.deltaTime : Vector3.zero;
    }

    private void RotateToMoveDirection(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude < 0.001f)
        {
            return;
        }
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, config.RotationSmoothSpeed * Time.deltaTime);
    }
}
