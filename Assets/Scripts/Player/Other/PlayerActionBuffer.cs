using System.Collections.Generic;
using UnityEngine;

// 输入类型枚举。
public enum PlayerBufferedAction
{
    Jump,
    Dodge
}

public class PlayerActionBuffer : MonoBehaviour, IPlayerActionBuffer
{
    [Header("配置")]
    [SerializeField] private PlayerActionBufferConfig config;

    /// <summary>
    /// 输入缓冲结构体，记录显式 Simulation Clock 上的到期时间。
    /// </summary>
    private readonly struct BufferedAction
    {
        public readonly float ExpiresAt;

        public BufferedAction(float expiresAt)
        {
            ExpiresAt = expiresAt;
        }
    }

    // 字典通过动作枚举管理对应的缓冲时间。
    private readonly Dictionary<PlayerBufferedAction, BufferedAction> bufferedActions = new Dictionary<PlayerBufferedAction, BufferedAction>();
    private float simulationTime;

    public void Tick(float deltaTime)
    {
        simulationTime += Mathf.Max(0f, deltaTime);
    }

    /// <summary>
    /// 根据动作类型写入默认时长的缓冲。
    /// </summary>
    /// <param name="action">输入动作枚举。</param>
    public void Buffer(PlayerBufferedAction action)
    {
        BufferInternal(action, GetDefaultDuration(action));
    }

    /// <summary>
    /// 消耗缓冲。
    /// </summary>
    /// <param name="action">缓冲类型。</param>
    /// <returns>是否成功消费。</returns>
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
    /// 移除指定缓冲。
    /// </summary>
    /// <param name="action">行为枚举。</param>
    public void Clear(PlayerBufferedAction action)
    {
        bufferedActions.Remove(action);
    }

    /// <summary>
    /// 移除全部缓冲。
    /// </summary>
    public void ClearAll()
    {
        bufferedActions.Clear();
    }

    /// <summary>
    /// 以指定持续时间写入缓冲。
    /// </summary>
    /// <param name="action">行为枚举类型。</param>
    /// <param name="duration">缓冲持续时间。</param>
    public void BufferInternal(PlayerBufferedAction action, float duration)
    {
        bufferedActions[action] = new BufferedAction(simulationTime + Mathf.Max(0f, duration));
    }

    /// <summary>
    /// 返回不同动作的默认缓冲时间。
    /// </summary>
    /// <param name="action">动作枚举。</param>
    /// <returns>缓冲时间。</returns>
    private float GetDefaultDuration(PlayerBufferedAction action)
    {
        switch (action)
        {
            case PlayerBufferedAction.Jump:
                return config.JumpBufferTime;
            case PlayerBufferedAction.Dodge:
                return config.DodgeBufferTime;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// 检测是否存在尚未过期的缓冲。
    /// </summary>
    /// <param name="action">行为枚举类型。</param>
    /// <returns>是否存在缓冲。</returns>
    public bool HasBuffered(PlayerBufferedAction action)
    {
        if (!bufferedActions.TryGetValue(action, out BufferedAction bufferedAction))
        {
            return false;
        }

        // 这里只读取缓存状态；清理由消费、覆盖写入或显式 Clear 负责。
        return simulationTime <= bufferedAction.ExpiresAt;
    }
}
