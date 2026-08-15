using UnityEngine;
/// <summary>
/// 玩家跳跃能力
/// </summary>
public class PlayerJump : MonoBehaviour
{
    [Header("配置")]
    [SerializeField] private PlayerMovementConfig config;

    public bool CanJump(bool isGrounded) => isGrounded;

    public float CalculateImpulse()
    {
        return Mathf.Sqrt(config.JumpHeight * -2f * config.MotorPhysics.Gravity);
    }
}
