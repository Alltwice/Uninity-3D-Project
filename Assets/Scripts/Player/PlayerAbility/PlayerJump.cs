using UnityEngine;
/// <summary>
/// 玩家跳跃能力
/// </summary>
public class PlayerJump : MonoBehaviour
{
    [Header("跳跃设置")] 
    [SerializeField] private float jumpHeight = 1.5f;
    private PlayerMotor playerMotor;

    public bool CanJump => playerMotor.IsGrounded;
    //确认固定组件同根物体无需注入
    private void Awake()
    {
        playerMotor = GetComponent<PlayerMotor>();
    }

    /// <summary>
    /// 执行已经通过状态转换判断的跳跃
    /// </summary>
    public void ExecuteJump()
    {
        //经典v2=2gh，开方后得到速度
        float jumpVelocity = Mathf.Sqrt(jumpHeight * -2f * playerMotor.Gravity);
        playerMotor.ChangeVerticalVelocity_y(jumpVelocity);
    }
}
