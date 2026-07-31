using UnityEngine;
/// <summary>
/// 玩家输入接口，用于给具体逻辑实现引用后主动注入
/// </summary>
public interface IPlayerInputSource
{
    //写入具体输入事件
    Vector2 MoveInput { get; }
    Vector2 LookInput { get; }
    bool IsWalkMode { get; }
    bool IsSprintHeld { get; }
}
