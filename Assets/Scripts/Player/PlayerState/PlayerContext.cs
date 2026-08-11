/// <summary>
/// 玩家状态共享的稳定依赖。
/// </summary>
public class PlayerContext
{
    public PlayerMotor Motor { get; }
    public PlayerJump Jump { get; }
    public PlayerDodge Dodge { get; }
    public IPlayerAnimationController AnimationController { get; }
    public IPlayerInputSource InputSource { get; }
    public IPlayerActionBuffer ActionBuffer { get; }

    public PlayerContext(PlayerMotor motor, PlayerJump jump, PlayerDodge dodge, IPlayerAnimationController animationController, IPlayerInputSource inputSource, IPlayerActionBuffer actionBuffer)
    {
        Motor = motor;
        Jump = jump;
        Dodge = dodge;
        AnimationController = animationController;
        InputSource = inputSource;
        ActionBuffer = actionBuffer;
    }
}
