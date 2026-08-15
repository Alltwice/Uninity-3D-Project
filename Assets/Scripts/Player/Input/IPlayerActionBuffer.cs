using UnityEngine;
/// <summary>
/// 缓冲能力接口
/// </summary>
public interface IPlayerActionBuffer
{
    void Tick(float deltaTime);
    void Buffer(PlayerBufferedAction action);
    void BufferInternal(PlayerBufferedAction action, float duration);
    bool Consume(PlayerBufferedAction action);
    bool HasBuffered(PlayerBufferedAction action);
    void Clear(PlayerBufferedAction action);
    void ClearAll();
}
