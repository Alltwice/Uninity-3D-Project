using UnityEngine;
/// <summary>
/// 动画切换参数结构体
/// </summary>
public struct PlayerAnimationFrame
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
        groundProbe.Refresh(motor.VerticalSpeed, motor.IsGrounded);
        float normalizedMoveSpeed;
        if (motor.MoveSpeed <= 0.01f)
        {
            normalizedMoveSpeed = 0f;
        }
        else
        {
            float speedRatio = motor.HorizontalSpeed / motor.MoveSpeed;
            normalizedMoveSpeed = Mathf.Clamp01(speedRatio);
        }
        return new PlayerAnimationFrame(
            normalizedMoveSpeed,
            motor.VerticalSpeed,
            motor.IsGrounded,
            groundProbe.IsNearGround);
    }
}
