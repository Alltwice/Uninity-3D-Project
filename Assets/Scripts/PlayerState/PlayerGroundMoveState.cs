using UnityEngine;
/// <summary>
/// 玩家在地面移动逻辑
/// </summary>
public class PlayerGroundMoveState : PlayerStateBase
{
    public PlayerGroundMoveState(PlayerContext context) : base(context){}
    public override void Enter()
    {

    }
    public override void Exit()
    {

    }

    public override void Tick()
    {
        Context.Motor.Move();
    }

    public override bool CanExit()
    {
        return true;
    }
}
