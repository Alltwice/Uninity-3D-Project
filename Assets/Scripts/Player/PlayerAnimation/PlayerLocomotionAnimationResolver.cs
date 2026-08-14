using UnityEngine;

public enum LocomotionTurnType
{
    None,
    Turn180Left,
    Turn180Right
}

/// <summary>
/// 将用于判断转向角和左右
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
        //无输入
        if (referenceDirection.sqrMagnitude < 0.001f || targetDirection.sqrMagnitude < 0.001f)
        {
            return LocomotionTurnType.None;
        }
        float signedAngle = Vector3.SignedAngle(referenceDirection, targetDirection, Vector3.up);
        //小于触发转向角度
        if (Mathf.Abs(signedAngle) < turn180Threshold)
        {
            return LocomotionTurnType.None;
        }
        //根据转向角，正为右，左为负
        return signedAngle > 0f ? LocomotionTurnType.Turn180Right : LocomotionTurnType.Turn180Left;
    }
}
