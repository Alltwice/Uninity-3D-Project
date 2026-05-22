using UnityEngine;
/// <summary>
/// 玩家空中状态
/// </summary>
public class PlayerAirState : PlayerStateBase
{
    public PlayerAirState(PlayerContext context) : base(context){}
    public override void Enter()
    {
        
    }

    public override void Exit()
    {
        
    }

    public override void Tick()
    {
        Context.Motor.AirMove();
    }

    public override bool CanExit()
    {
        return true;
    }
}
