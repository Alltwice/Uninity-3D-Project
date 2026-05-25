using System;
using UnityEngine;
/// <summary>
/// 将玩家数据传入Animator参数
/// </summary>
public class PlayerAnimationDriver : MonoBehaviour
{
    [Header("引用")] 
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMotor motor;
    [Header("动画切换参数")] 
    [SerializeField] private float moveSpeedDampTime=0.1f;
    [SerializeField] private float verticalSpeedDampTime = 0.05f;
    //将字符串信息转为哈希值存储起来避免拼写错误和每帧字符串查找性能损耗
    private static readonly int MoveSpeedID = Animator.StringToHash("MoveSpeed");
    private static readonly int VerticalSpeedID = Animator.StringToHash("VerticalSpeed");
    private static readonly int IsGroundID = Animator.StringToHash("IsGround");
    private void Awake()
    {
        motor=GetComponent<PlayerMotor>();
        animator=GetComponent<Animator>();
    }
    private void Update()
    { 
        UpdateLocomotionParameters();   
    }
    //——————————————————————————————————————————————————调用方法————————————————————————————————————————
    /// <summary>
    /// 将归一化的数值实时传入给animator
    /// </summary>
    private void UpdateLocomotionParameters()
    {
        //具体含义为给编号MoveSoeed的动画，传入当前比值，变化速率为固定每帧moveSpeedDampTime个单位
        float nomalizedMoveSpeed = GetNormalizedMoveSpeed();
        animator.SetFloat(MoveSpeedID, nomalizedMoveSpeed, moveSpeedDampTime,Time.deltaTime);
        animator.SetBool(IsGroundID, motor.IsGrounded);
    }
    
    //————————————————————————————————————————————辅助方法——————————————————————————————————————————————
    /// <summary>
    /// 将真实速度转换为比值后供使用
    /// </summary>
    /// <returns>返回处理后的速度比值</returns>
    private float GetNormalizedMoveSpeed()
    {
        if (motor.MoveSpeed<=0.01f)
        {
            return 0f;
        }
        //这里前者为实际水平移动速度，后者为最大速度，求得其比值供BlenderTree使用
        float speed=motor.HorizontalSpeed/motor.MoveSpeed;
        return Mathf.Clamp01(speed);
        if (speed <= 0.015f)
        {
            return 0f;
        }
    }
}
