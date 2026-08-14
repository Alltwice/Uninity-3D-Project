using UnityEngine;

public enum LocomotionTurnType
{
    None,
    Turn180Left,
    Turn180Right
}

/// <summary>
/// 将运动事实解析为 Locomotion 动画语义，不读取输入、不修改移动，也不持有动画播放权
/// </summary>
public class PlayerLocomotionAnimationResolver
{
    private float turn180Threshold;

    public PlayerLocomotionAnimationResolver(float turn180Threshold)
    {
        this.turn180Threshold = turn180Threshold;
    }

    public LocomotionTurnType ResolveIdleStart(Vector3 facingDirection, Vector3 desiredMoveDirection)
    {
        return ResolveTurn(facingDirection, desiredMoveDirection);
    }

    public LocomotionTurnType ResolveMovingTurn(Vector3 horizontalMoveDirection, Vector3 desiredMoveDirection)
    {
        return ResolveTurn(horizontalMoveDirection, desiredMoveDirection);
    }
    /// <summary>
    /// 角度判断方法返回动画类型枚举
    /// </summary>
    private LocomotionTurnType ResolveTurn(Vector3 referenceDirection, Vector3 targetDirection)
    {
        referenceDirection.y = 0f;
        targetDirection.y = 0f;
        if (referenceDirection.sqrMagnitude < 0.001f || targetDirection.sqrMagnitude < 0.001f)
        {
            return LocomotionTurnType.None;
        }
        float signedAngle = Vector3.SignedAngle(referenceDirection, targetDirection, Vector3.up);
        if (Mathf.Abs(signedAngle) < turn180Threshold)
        {
            return LocomotionTurnType.None;
        }
        return signedAngle > 0f ? LocomotionTurnType.Turn180Right : LocomotionTurnType.Turn180Left;
    }
}
