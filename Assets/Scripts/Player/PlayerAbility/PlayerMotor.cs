using UnityEngine;

/// <summary>
/// 唯一 CharacterController 执行器。只解释 MotorCommand，不认识 Gameplay 动作或动画。
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerGroundProbe))]
public sealed class PlayerMotor : MonoBehaviour
{
    [SerializeField] private PlayerMovementConfig config;

    private CharacterController characterController;
    private PlayerGroundProbe groundProbe;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private bool isGrounded;
    private bool initialized;

    public PlayerMovementConfig Config => config;
    public PlayerMotorResult CurrentResult { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        groundProbe = GetComponent<PlayerGroundProbe>();
    }

    public void EnsureInitialized()
    {
        if (initialized) return;
        groundProbe.Refresh();
        isGrounded = characterController.isGrounded || groundProbe.CanSnapToGround;
        CurrentResult = new PlayerMotorResult(Vector3.zero, Vector3.zero, Vector3.zero, 0f, isGrounded, false, 0f, CollisionFlags.None);
        initialized = true;
    }

    public PlayerMotorResult Simulate(PlayerMotorCommand command, float deltaTime)
    {
        EnsureInitialized();
        float dt = Mathf.Max(0f, deltaTime);
        if (command.HasVerticalImpulse) verticalVelocity = command.VerticalImpulse;
        if (isGrounded && verticalVelocity < 0f) verticalVelocity = config.MotorPhysics.GroundedVerticalVelocity;
        else verticalVelocity += config.MotorPhysics.Gravity * dt;
        Vector3 planarDisplacement;
        if (command.TranslationMode == PlayerMotorTranslationMode.VelocityDriven)
        {
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, command.TargetPlanarVelocity, command.PlanarAcceleration * dt);
            planarDisplacement = horizontalVelocity * dt;
        }
        else
        {
            planarDisplacement = command.PlanarDisplacement;
            planarDisplacement.y = 0f;
        }
        Vector3 positionBeforeMove = transform.position;
        bool wasGrounded = isGrounded;
        float downwardSpeedBeforeMove = Mathf.Max(0f, -verticalVelocity);
        CollisionFlags collisionFlags = characterController.Move(planarDisplacement + Vector3.up * (verticalVelocity * dt));
        bool controllerGrounded = (collisionFlags & CollisionFlags.Below) != 0 || characterController.isGrounded;
        groundProbe.Refresh();
        bool snappedToGround = !controllerGrounded && wasGrounded && verticalVelocity <= 0f && groundProbe.CanSnapToGround;
        if (snappedToGround)
        {
            collisionFlags |= characterController.Move(-transform.up * groundProbe.GroundDistance);
            groundProbe.Refresh();
        }
        isGrounded = controllerGrounded || snappedToGround;
        bool justLanded = !wasGrounded && isGrounded;
        float landingImpactSpeed = justLanded ? downwardSpeedBeforeMove : 0f;
        if (isGrounded && verticalVelocity < config.MotorPhysics.GroundedVerticalVelocity) verticalVelocity = config.MotorPhysics.GroundedVerticalVelocity;
        ApplyRotation(command, dt);
        Vector3 actualDisplacement = transform.position - positionBeforeMove;
        Vector3 actualPlanarDisplacement = Vector3.ProjectOnPlane(actualDisplacement, Vector3.up);
        horizontalVelocity = PlayerMotorKinematics.CalculateActualPlanarVelocity(actualDisplacement, dt);
        CurrentResult = new PlayerMotorResult(actualDisplacement, actualPlanarDisplacement, horizontalVelocity, verticalVelocity, isGrounded, justLanded, landingImpactSpeed, collisionFlags);
        return CurrentResult;
    }

    private void ApplyRotation(PlayerMotorCommand command, float deltaTime)
    {
        if (command.RotationMode == PlayerMotorRotationMode.YawDelta)
        {
            transform.rotation = Quaternion.AngleAxis(command.YawDelta, Vector3.up) * transform.rotation;
            return;
        }
        Vector3 facing = command.DesiredFacingDirection;
        facing.y = 0f;
        if (command.RotationMode != PlayerMotorRotationMode.FaceDirection || facing.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(facing.normalized, Vector3.up);
        float t = 1f - Mathf.Exp(-config.Locomotion.RotationSmoothSpeed * deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, t);
    }
}
