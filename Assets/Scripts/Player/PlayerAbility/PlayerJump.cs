using System;
using UnityEngine;
/// <summary>
/// 玩家跳跃能力
/// </summary>
public class PlayerJump : MonoBehaviour
{
    [Header("跳跃设置")] 
    [SerializeField] private float jumpHeight = 1.5f;
    private PlayerMotor playerMotor;
    //确认固定组件同根物体无需注入
    private void Awake()
    {
        playerMotor = GetComponent<PlayerMotor>();
    }

    /// <summary>
    /// 计算跳跃高度
    /// </summary>
    /// <returns>返回是否成功起跳</returns>
    public bool TryJump()
    {
        if (!playerMotor.IsGrounded)
        {
            return false;
        }
        //经典v2=2gh，开方后得到速度
        float jumpVelocity = Mathf.Sqrt(jumpHeight * -2f * playerMotor.Gravity);
        playerMotor.ChangeVerticalVelocity_y(jumpVelocity);
        return true;
    }
}
