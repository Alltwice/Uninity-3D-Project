using System.Collections.Generic;
using UnityEngine;

//输入类型枚举
public enum PlayerBufferedAction
{
    Jump
}

public class PlayerActionBuffer : MonoBehaviour, IPlayerActionBuffer
{
    [Header("配置")]
    [SerializeField] private PlayerActionBufferConfig config;
    /// <summary>
    /// 输入缓冲结构体，由开始时间和持续时间组成
    /// </summary>
    private readonly struct BufferedAction
    {
        public readonly float StartTime;
        public readonly float Duration;

        public BufferedAction(float startTime, float duration)
        {
            StartTime = startTime;
            Duration = duration;
        }
    }
    //字典加枚举管理输入缓冲，对应为动作和缓冲时间
    private readonly Dictionary<PlayerBufferedAction, BufferedAction> bufferedActions = new Dictionary<PlayerBufferedAction, BufferedAction>();
    //——————————————————————————————————————————————————————主要方法——————————————————————————————————————
    /// <summary>
    /// 输入枚举类型，供外部调用，更具类型给予缓冲时间
    /// </summary>
    /// <param name="action">输入枚举</param>
    public void Buffer(PlayerBufferedAction action)
    {
        //存入字典缓冲管理
        BufferInternal(action, GetDefaultDuration(action));
    }
    /// <summary>
    /// 消耗缓冲
    /// </summary>
    /// <param name="action">缓冲类型</param>
    /// <returns>是否被消耗</returns>
    public bool Consume(PlayerBufferedAction action)
    {
        if (!HasBuffered(action))
        {
            bufferedActions.Remove(action);
            return false;
        }

        bufferedActions.Remove(action);
        return true;
    }
/// <summary>
/// 移除缓冲
/// </summary>
/// <param name="action">行为枚举</param>
    public void Clear(PlayerBufferedAction action)
    {
        bufferedActions.Remove(action);
    }
/// <summary>
/// 移除所有缓冲
/// </summary>
    public void ClearAll()
    {
        bufferedActions.Clear();
    }
//——————————————————————————————————————————————————辅助方法——————————————————————————————————————————
    /// <summary>
    /// 调用该方法存入字典管理
    /// </summary>
    /// <param name="action">行为枚举类型</param>
    /// <param name="duration">缓冲持续时间</param>
    public void BufferInternal(PlayerBufferedAction action, float duration)
    {
        //记录开始时间和缓冲时间
        bufferedActions[action] = new BufferedAction(Time.time, Mathf.Max(0f, duration));
    }
    /// <summary>
    /// 通过不同的枚举类型给不同的缓冲时间
    /// </summary>
    /// <param name="action">枚举类型</param>
    /// <returns>缓冲时间</returns>
    private float GetDefaultDuration(PlayerBufferedAction action)
    {
        switch (action)
        {
            case PlayerBufferedAction.Jump:
                return config.JumpBufferTime;
            default:
                return 0f;
        }
    }
    /// <summary>
    /// 检测是否存在缓冲
    /// </summary>
    /// <param name="action">行为枚举类型</param>
    /// <returns>是否存在缓冲</returns>
    public bool HasBuffered(PlayerBufferedAction action)
    {
        if (!bufferedActions.TryGetValue(action, out BufferedAction bufferedAction))
        {
            return false;
        }
        //判断只读取缓存状态；清理由消费、覆盖写入或显式 Clear 负责。
        return Time.time - bufferedAction.StartTime <= bufferedAction.Duration;
    }
}
