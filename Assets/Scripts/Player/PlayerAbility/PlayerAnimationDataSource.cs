using UnityEngine;

public readonly struct PlayerAnimationFrame
{
    public PlayerAnimationFrame(
        float normalizedMoveSpeed,
        float verticalSpeed,
        bool isGrounded,
        bool isNearGround)
    {
        NormalizedMoveSpeed = normalizedMoveSpeed;
        VerticalSpeed = verticalSpeed;
        IsGrounded = isGrounded;
        IsNearGround = isNearGround;
    }

    public float NormalizedMoveSpeed { get; }
    public float VerticalSpeed { get; }
    public bool IsGrounded { get; }
    public bool IsNearGround { get; }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerGroundProbe))]
public sealed class PlayerAnimationDataSource : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private PlayerGroundProbe groundProbe;

    private void Awake()
    {
        if (motor == null)
        {
            motor = GetComponent<PlayerMotor>();
        }

        if (groundProbe == null)
        {
            groundProbe = GetComponent<PlayerGroundProbe>();
        }
    }

    public PlayerAnimationFrame Capture()
    {
        groundProbe.Refresh(motor.VerticalSpeed, motor.IsGrounded);

        float normalizedMoveSpeed = motor.MoveSpeed <= 0.01f
            ? 0f
            : Mathf.Clamp01(motor.HorizontalSpeed / motor.MoveSpeed);

        return new PlayerAnimationFrame(
            normalizedMoveSpeed,
            motor.VerticalSpeed,
            motor.IsGrounded,
            groundProbe.IsNearGround);
    }
}
