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
        Debug.Log("Entering PlayerIdleState");
    }
    public override void Exit()
    {
        Debug.Log("Exiting PlayerIdleState");
    }
    public override void Tick()
    {

    }
    public override bool CanExit()
    {
        return true;
    }
}
