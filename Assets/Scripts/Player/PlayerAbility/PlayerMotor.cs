using UnityEngine;

public enum PlayerLocomotionMode
{
    Idle,
    Walk,
    Run,
    FastRun,
    Air
}

/// <summary>
/// 记录移动模式切换的原因
/// </summary>
public enum PlayerLocomotionTransitionReason
{
    Initialized,
    Started,
    Accelerated,
    Decelerated,
    Stopped,
    BecameAirborne,
    Landed,
    DodgeCompleted
}

public readonly struct PlayerLocomotionTransition
{
    public PlayerLocomotionTransition(ulong sequenceId, PlayerLocomotionMode previousMode, PlayerLocomotionMode currentMode, PlayerLocomotionTransitionReason reason)
    {
        SequenceId = sequenceId;
        PreviousMode = previousMode;
        CurrentMode = currentMode;
        Reason = reason;
    }

    public ulong SequenceId { get; }
    public PlayerLocomotionMode PreviousMode { get; }
    public PlayerLocomotionMode CurrentMode { get; }
    public PlayerLocomotionTransitionReason Reason { get; }
}

/// <summary>
/// 负责角色移动、碰撞处理与已提交的移动模式事实。
/// </summary>
public class PlayerMotor : MonoBehaviour
{
    [Header("移动配置")]
    [SerializeField] private PlayerMotorConfig config;

    private CharacterController characterController;
    private PlayerGroundProbe groundProbe;
    private IPlayerInputSource inputSource;
    private Transform cameraTransform;
    private Vector3 verticalVelocity;
    private Vector3 horizontalVelocity;
    private bool isGrounded;
    private ulong locomotionSequenceId;
    private PlayerLocomotionMode currentLocomotionMode = PlayerLocomotionMode.Idle;
    private PlayerLocomotionTransition lastLocomotionTransition;

    public float MoveSpeed => CurrentTargetSpeed;
    public float CurrentTargetSpeed { get; private set; }
    public PlayerLocomotionMode CurrentLocomotionMode => currentLocomotionMode;
    public PlayerLocomotionTransition LastLocomotionTransition => lastLocomotionTransition;
    public float HorizontalSpeed => new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
    public IPlayerInputSource InputSource => inputSource;
    public float VerticalSpeed => verticalVelocity.y;
    public bool JustLanded { get; private set; }
    public float LandingImpactSpeed { get; private set; }
    public bool IsHardLandingImpact => JustLanded && LandingImpactSpeed >= config.HardLandingMinImpactSpeed;
    public bool IsGrounded => isGrounded;
    public float Gravity => config.Gravity;

    private void Awake()
    {
        lastLocomotionTransition = new PlayerLocomotionTransition(0, PlayerLocomotionMode.Idle, PlayerLocomotionMode.Idle, PlayerLocomotionTransitionReason.Initialized);
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

    public void Init(IPlayerInputSource inputSource)
    {
        this.inputSource = inputSource;
    }

    public void ChangeVerticalVelocity_y(float value)
    {
        verticalVelocity.y = value;
    }

    /// <summary>
    /// 普通地面移动只会提交 Walk 或 Run。
    /// </summary>
    public void MoveFromInput()
    {
        PlayerLocomotionMode mode = inputSource.IsWalkMode ? PlayerLocomotionMode.Walk : PlayerLocomotionMode.Run;
        MoveGround(GetWorldInputDirection(), mode, null);
    }

    /// <summary>
    /// Dodge 完成后提交 FastRun 的唯一入口。
    /// </summary>
    public void MoveFastRunAfterDodge()
    {
        MoveGround(GetWorldInputDirection(), PlayerLocomotionMode.FastRun, PlayerLocomotionTransitionReason.DodgeCompleted);
    }

    public void AirMove()
    {
        Vector3 moveDirection = GetWorldInputDirection();
        CommitLocomotionMode(PlayerLocomotionMode.Air, PlayerLocomotionTransitionReason.BecameAirborne);
        CurrentTargetSpeed = GetSpeed(CurrentLocomotionMode);
        ApplyGravity();
        Vector3 targetHorizontalVelocity = moveDirection * CurrentTargetSpeed;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetHorizontalVelocity, config.AirAcceleration * Time.deltaTime);
        MoveCharacterVelocity(horizontalVelocity + verticalVelocity);
        RotateToMoveDirection(moveDirection);
    }

    public void IdleMove()
    {
        CommitLocomotionMode(PlayerLocomotionMode.Idle, ResolveTransitionReason(PlayerLocomotionMode.Idle));
        CurrentTargetSpeed = 0f;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, config.GroundDeceleration * Time.deltaTime);
        ApplyGravity();
        MoveCharacterVelocity(horizontalVelocity + verticalVelocity);
    }

    public void DodgeMove(Vector3 direction, float horizontalDistance)
    {
        direction.y = 0f;
        direction.Normalize();
        ApplyGravity();

        Vector3 positionBeforeMove = transform.position;
        Vector3 displacement = direction * horizontalDistance + verticalVelocity * Time.deltaTime;
        MoveCharacterDisplacement(displacement);

        Vector3 actualHorizontalDisplacement = transform.position - positionBeforeMove;
        actualHorizontalDisplacement.y = 0f;
        horizontalVelocity = Time.deltaTime > 0f ? actualHorizontalDisplacement / Time.deltaTime : Vector3.zero;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    public Vector3 GetWorldInputDirection()
    {
        Vector2 moveInput = inputSource.MoveInput;
        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }

        return GetCameraMoveDirection(inputDirection);
    }

    private void MoveGround(Vector3 moveDirection, PlayerLocomotionMode mode, PlayerLocomotionTransitionReason? explicitReason)
    {
        PlayerLocomotionTransitionReason reason = explicitReason ?? ResolveTransitionReason(mode);
        CommitLocomotionMode(mode, reason);
        CurrentTargetSpeed = GetSpeed(CurrentLocomotionMode);
        ApplyGravity();
        UpdateGroundHorizontalVelocity(moveDirection);
        MoveCharacterVelocity(horizontalVelocity + verticalVelocity);
        RotateToMoveDirection(moveDirection);
    }

    private void CommitLocomotionMode(PlayerLocomotionMode mode, PlayerLocomotionTransitionReason reason)
    {
        if (mode == currentLocomotionMode)
        {
            return;
        }

        PlayerLocomotionMode previousMode = currentLocomotionMode;
        currentLocomotionMode = mode;
        lastLocomotionTransition = new PlayerLocomotionTransition(++locomotionSequenceId, previousMode, mode, reason);
    }

    private PlayerLocomotionTransitionReason ResolveTransitionReason(PlayerLocomotionMode nextMode)
    {
        PlayerLocomotionMode previousMode = CurrentLocomotionMode;
        if (nextMode == PlayerLocomotionMode.Air)
        {
            return PlayerLocomotionTransitionReason.BecameAirborne;
        }

        if (previousMode == PlayerLocomotionMode.Air)
        {
            return PlayerLocomotionTransitionReason.Landed;
        }

        if (nextMode == PlayerLocomotionMode.Idle)
        {
            return PlayerLocomotionTransitionReason.Stopped;
        }

        if (previousMode == PlayerLocomotionMode.Idle)
        {
            return PlayerLocomotionTransitionReason.Started;
        }

        return GetGroundModeRank(nextMode) > GetGroundModeRank(previousMode) ? PlayerLocomotionTransitionReason.Accelerated : PlayerLocomotionTransitionReason.Decelerated;
    }

    private void UpdateGroundHorizontalVelocity(Vector3 moveDirection)
    {
        Vector3 targetVelocity = moveDirection * CurrentTargetSpeed;
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

    private void RotateToMoveDirection(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, config.RotationSmoothSpeed * Time.deltaTime);
    }

    private float GetSpeed(PlayerLocomotionMode locomotionMode)
    {
        switch (locomotionMode)
        {
            case PlayerLocomotionMode.Walk:
                return config.WalkSpeed;
            case PlayerLocomotionMode.Run:
                return config.RunSpeed;
            case PlayerLocomotionMode.FastRun:
                return config.FastRunSpeed;
            case PlayerLocomotionMode.Air:
                return config.AirMoveSpeed;
            default:
                return 0f;
        }
    }

    private int GetGroundModeRank(PlayerLocomotionMode mode)
    {
        switch (mode)
        {
            case PlayerLocomotionMode.Walk:
                return 1;
            case PlayerLocomotionMode.Run:
                return 2;
            case PlayerLocomotionMode.FastRun:
                return 3;
            default:
                return 0;
        }
    }
}
