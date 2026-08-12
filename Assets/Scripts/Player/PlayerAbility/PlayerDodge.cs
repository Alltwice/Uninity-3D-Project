using UnityEngine;

public readonly struct DodgeTickResult
{
    public DodgeTickResult(Vector3 direction, float horizontalDistance, bool justCompleted)
    {
        Direction = direction;
        HorizontalDistance = horizontalDistance;
        JustCompleted = justCompleted;
    }

    public Vector3 Direction { get; }
    public float HorizontalDistance { get; }
    public bool JustCompleted { get; }
}

/// <summary>
/// 计算一次 Dodge 的方向、位移进度和冷却，不表达玩家 Gameplay 状态
/// </summary>
public sealed class PlayerDodge : MonoBehaviour
{
    private const float DirectionEpsilon = 0.0001f;

    [Header("闪避配置")]
    [SerializeField] private PlayerDodgeConfig config;

    private float elapsedTime;
    private float previousProgress;
    private float cooldownEndsAt;
    private Vector3 currentDirection;

    public bool CanDodge => Time.time >= cooldownEndsAt;

    public void Begin(Vector3 initialDirection)
    {
        currentDirection = NormalizePlanarDirection(initialDirection);
        elapsedTime = 0f;
        previousProgress = Mathf.Clamp01(config.DistanceProgress.Evaluate(0f));
    }

    public DodgeTickResult Tick(float deltaTime, Vector3 inputDirection)
    {
        Vector3 normalizedInput = NormalizePlanarDirection(inputDirection);
        if (normalizedInput.sqrMagnitude >= DirectionEpsilon)
        {
            currentDirection = normalizedInput;
        }
        //记录播放时间
        elapsedTime = Mathf.Min(config.Duration, elapsedTime + deltaTime);
        //推测播放进度
        float normalizedTime = elapsedTime / config.Duration;
        //时间推进度
        float progress = Mathf.Clamp01(config.DistanceProgress.Evaluate(normalizedTime));
        //移动距离
        float horizontalDistance = config.Distance * Mathf.Max(0f, progress - previousProgress);
        previousProgress = progress;
        bool justCompleted = elapsedTime >= config.Duration;
        if (justCompleted)
        {
            cooldownEndsAt = Time.time + config.Cooldown;
        }
        return new DodgeTickResult(currentDirection, horizontalDistance, justCompleted);
    }

    public void Cancel()
    {
        cooldownEndsAt = Time.time + config.Cooldown;
    }

    private Vector3 NormalizePlanarDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude >= DirectionEpsilon)
        {
            direction.Normalize();
        }
        return direction;
    }
}
