using UnityEngine;
/// <summary>
/// 存放具体状态类需要脚本供状态类引用
/// </summary>
public class PlayerContext
{
    // ===== 稳定引用 =====
    public PlayerMotor Motor { get; private set; }
    public PlayerJump Jump { get; private set; }
    public PlayerAnimationDriver AnimationDriver { get; private set; }
    public PlayerContext( PlayerMotor motor, PlayerJump jump , PlayerAnimationDriver animationDriver)
    {
        Motor = motor;
        Jump = jump;
        AnimationDriver = animationDriver;
    }
}
