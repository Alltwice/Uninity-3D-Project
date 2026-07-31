using UnityEngine;
/// <summary>
/// 动画切换参数结构体
/// </summary>
public struct PlayerAnimationFrame
{
    public PlayerAnimationFrame(
        float horizontalSpeed,
        float targetMoveSpeed,
        PlayerLocomotionMode locomotionMode,
        float verticalSpeed,
        bool isGrounded,
        bool justLanded,
        float landingImpactSpeed,
        bool isHardLandingImpact,
        bool isNearGround)
    {
        HorizontalSpeed = horizontalSpeed;
        TargetMoveSpeed = targetMoveSpeed;
        LocomotionMode = locomotionMode;
        VerticalSpeed = verticalSpeed;
        IsGrounded = isGrounded;
        JustLanded = justLanded;
        LandingImpactSpeed = landingImpactSpeed;
        IsHardLandingImpact = isHardLandingImpact;
        IsNearGround = isNearGround;
    }

    public float HorizontalSpeed { get; }
    public float TargetMoveSpeed { get; }
    public PlayerLocomotionMode LocomotionMode { get; }
    public float VerticalSpeed { get; }
    public bool IsGrounded { get; }
    public bool JustLanded { get; }
    public float LandingImpactSpeed { get; }
    public bool IsHardLandingImpact { get; }
    public bool IsNearGround { get; }
}
/// <summary>
/// 捕获和处理动画所需数据
/// </summary>
public class PlayerAnimationDataSource : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private PlayerGroundProbe groundProbe;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        groundProbe = GetComponent<PlayerGroundProbe>();
    }
    /// <summary>
    /// 时刻更新当前的动画数据
    /// </summary>
    /// <returns>返回可用的动画数据结构体</returns>
    public PlayerAnimationFrame Capture()
    {
        return new PlayerAnimationFrame(
            motor.HorizontalSpeed,
            motor.CurrentTargetSpeed,
            motor.CurrentLocomotionMode,
            motor.VerticalSpeed,
            motor.IsGrounded,
            motor.JustLanded,
            motor.LandingImpactSpeed,
            motor.IsHardLandingImpact,
            groundProbe.IsNearGround(motor.VerticalSpeed, motor.IsGrounded));
    }
}
