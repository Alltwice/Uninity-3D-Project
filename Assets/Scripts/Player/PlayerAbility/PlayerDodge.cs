using UnityEngine;

public enum PlayerDodgeLifecycleType
{
    None,
    Started,
    Completed,
    Cancelled
}

public enum PlayerDodgeExitMode
{
    None,
    Idle,
    FastRun
}

public enum DodgeCancelReason
{
    None,
    Jumped,
    BecameAirborne,
    OtherAction
}

public readonly struct PlayerDodgeLifecycleTransition
{
    public PlayerDodgeLifecycleTransition(ulong sequenceId, PlayerDodgeLifecycleType lifecycleType, PlayerDodgeExitMode exitMode, DodgeCancelReason cancelReason)
    {
        SequenceId = sequenceId;
        LifecycleType = lifecycleType;
        ExitMode = exitMode;
        CancelReason = cancelReason;
    }

    public ulong SequenceId { get; }
    public PlayerDodgeLifecycleType LifecycleType { get; }
    public PlayerDodgeExitMode ExitMode { get; }
    public DodgeCancelReason CancelReason { get; }
}

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
/// 持有闪避过程、方向、位移进度、冷却与生命周期事实
/// </summary>
public sealed class PlayerDodge : MonoBehaviour
{
    private const float DirectionEpsilon = 0.0001f;

    [Header("闪避配置")]
    [SerializeField] private PlayerDodgeConfig config;

    private PlayerMotor motor;
    private bool isActive;
    private float elapsedTime;
    private float previousProgress;
    private float cooldownEndsAt;
    private Vector3 currentDirection;
    private ulong sequenceId;
    private PlayerDodgeLifecycleTransition lastTransition;

    public bool CanDodge => motor.IsGrounded && !isActive && Time.time >= cooldownEndsAt;
    public bool IsActive => isActive;
    public PlayerDodgeLifecycleTransition LastTransition => lastTransition;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
    }

    public void StartDodge(Vector3 initialDirection)
    {
        currentDirection = NormalizePlanarDirection(initialDirection);
        elapsedTime = 0f;
        previousProgress = Mathf.Clamp01(config.DistanceProgress.Evaluate(0f));
        isActive = true;
        lastTransition = new PlayerDodgeLifecycleTransition(++sequenceId, PlayerDodgeLifecycleType.Started, PlayerDodgeExitMode.None, DodgeCancelReason.None);
    }

    public DodgeTickResult Tick(float deltaTime, Vector3 inputDirection, PlayerDodgeExitMode exitModeIfCompleted)
    {
        Vector3 normalizedInput = NormalizePlanarDirection(inputDirection);
        if (normalizedInput.sqrMagnitude >= DirectionEpsilon)
        {
            currentDirection = normalizedInput;
        }

        elapsedTime = Mathf.Min(config.Duration, elapsedTime + deltaTime);
        float normalizedTime = elapsedTime / config.Duration;
        float progress = Mathf.Clamp01(config.DistanceProgress.Evaluate(normalizedTime));
        float horizontalDistance = config.Distance * Mathf.Max(0f, progress - previousProgress);
        previousProgress = progress;

        bool justCompleted = elapsedTime >= config.Duration;
        if (justCompleted)
        {
            isActive = false;
            cooldownEndsAt = Time.time + config.Cooldown;
            lastTransition = new PlayerDodgeLifecycleTransition(++sequenceId, PlayerDodgeLifecycleType.Completed, exitModeIfCompleted, DodgeCancelReason.None);
        }

        return new DodgeTickResult(currentDirection, horizontalDistance, justCompleted);
    }

    public void Cancel(DodgeCancelReason reason, PlayerDodgeExitMode exitMode = PlayerDodgeExitMode.Idle)
    {
        isActive = false;
        cooldownEndsAt = Time.time + config.Cooldown;
        lastTransition = new PlayerDodgeLifecycleTransition(++sequenceId, PlayerDodgeLifecycleType.Cancelled, exitMode, reason);
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
