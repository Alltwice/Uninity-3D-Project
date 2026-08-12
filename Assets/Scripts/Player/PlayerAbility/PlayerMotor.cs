using UnityEngine;

public enum PlayerMotionMode
{
    CodeDriven,
    AnimationDriven
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
    private bool isGrounded;

    public PlayerMotionMode MotionMode { get; private set; }
    public float HorizontalSpeed => new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
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

    public void SetMotionMode(PlayerMotionMode motionMode)
    {
        MotionMode = motionMode;
    }

    public void SubmitAnimationMotion(Vector3 deltaPosition, Quaternion deltaRotation)
    {
        if (MotionMode != PlayerMotionMode.AnimationDriven) return;
        ApplyGravity();
        Vector3 positionBeforeMove = transform.position;
        Vector3 horizontalRootMotion = new Vector3(deltaPosition.x, 0f, deltaPosition.z);
        MoveCharacterDisplacement(horizontalRootMotion + verticalVelocity * Time.deltaTime);
        UpdateHorizontalVelocityFromActualDisplacement(positionBeforeMove);
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
