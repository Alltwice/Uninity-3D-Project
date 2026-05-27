using UnityEngine;
/// <summary>
/// 玩家待机状态
/// </summary>
public class PlayerIdleState : PlayerStateBase
{
    //base，默认调用父类构造函数
    public PlayerIdleState(PlayerContext context) : base(context){}

    public override void Enter()
    {
    }
    public override void Exit()
    {
    }
    public override void Tick()
    {
        Context.Motor.IdleMove(); 
    }
    public override bool CanExit()
    {
        return true;
    }
}
