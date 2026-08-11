using System.Collections.Generic;
using UnityEngine;

public enum PlayerMovementPresentationPhase
{
    Idle,
    Starting,
    Looping,
    Bridging,
    Stopping,
    Dodging,
    Suspended
}

public enum PlayerMovementPresentationCommandType
{
    None,
    StartMode,
    LoopMode,
    BridgeMode,
    StopMode,
    BeginDodge,
    CompleteDodge,
    CancelDodge,
    SuspendGroundLocomotion
}

public readonly struct PlayerMovementPresentationCommand
{
    public PlayerMovementPresentationCommand(PlayerMovementPresentationCommandType commandType, PlayerLocomotionMode previousMode, PlayerLocomotionMode currentMode)
    {
        CommandType = commandType;
        PreviousMode = previousMode;
        CurrentMode = currentMode;
    }

    public PlayerMovementPresentationCommandType CommandType { get; }
    public PlayerLocomotionMode PreviousMode { get; }
    public PlayerLocomotionMode CurrentMode { get; }
}

public readonly struct PlayerAnimationFrame
{
    public PlayerAnimationFrame(float horizontalSpeed, float targetMoveSpeed, PlayerLocomotionMode locomotionMode, float verticalSpeed, bool isGrounded, bool justLanded, float landingImpactSpeed, bool isHardLandingImpact, bool isNearGround, PlayerLocomotionTransition locomotionTransition, PlayerDodgeLifecycleTransition dodgeTransition, PlayerMovementPresentationPhase presentationPhase, IReadOnlyList<PlayerMovementPresentationCommand> presentationCommands)
    {
        HorizontalSpeed = horizontalSpeed;
        TargetMoveSpeed = targetMoveSpeed;
        LocomotionMode = locomotionMode;
        VerticalSpeed = verticalSpeed;
        IsGrounded = isGrounded;
        JustLanded = justLanded;
        LandingImpactSpeed = landingImpactSpeed;
        IsHardLandingImpact = isHardLandingImpact;
        IsNearGround = isNearGround;
        LocomotionTransition = locomotionTransition;
        DodgeTransition = dodgeTransition;
        PresentationPhase = presentationPhase;
        PresentationCommands = presentationCommands;
    }

    public float HorizontalSpeed { get; }
    public float TargetMoveSpeed { get; }
    public PlayerLocomotionMode LocomotionMode { get; }
    public float VerticalSpeed { get; }
    public bool IsGrounded { get; }
    public bool JustLanded { get; }
    public float LandingImpactSpeed { get; }
    public bool IsHardLandingImpact { get; }
    public bool IsNearGround { get; }
    public PlayerLocomotionTransition LocomotionTransition { get; }
    public PlayerDodgeLifecycleTransition DodgeTransition { get; }
    public PlayerMovementPresentationPhase PresentationPhase { get; }
    public IReadOnlyList<PlayerMovementPresentationCommand> PresentationCommands { get; }
}

/// <summary>
/// 汇总已提交的玩法事实，并维护供动画系统消费的移动表现阶段。
/// </summary>
public class PlayerAnimationDataSource : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private PlayerDodge dodge;
    [SerializeField] private PlayerGroundProbe groundProbe;

    private readonly List<PlayerMovementPresentationCommand> presentationCommands = new List<PlayerMovementPresentationCommand>(2);
    private ulong lastLocomotionSequence;
    private ulong lastDodgeSequence;
    private PlayerMovementPresentationPhase currentPresentationPhase = PlayerMovementPresentationPhase.Idle;
    private PlayerLocomotionMode currentPresentationMode = PlayerLocomotionMode.Idle;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        dodge = GetComponent<PlayerDodge>();
        groundProbe = GetComponent<PlayerGroundProbe>();
    }

    public PlayerAnimationFrame Capture()
    {
        IReadOnlyList<PlayerMovementPresentationCommand> commands = ConsumePresentationTransitions(motor.LastLocomotionTransition, dodge.LastTransition);
        return new PlayerAnimationFrame(motor.HorizontalSpeed, motor.CurrentTargetSpeed, motor.CurrentLocomotionMode, motor.VerticalSpeed, motor.IsGrounded, motor.JustLanded, motor.LandingImpactSpeed, motor.IsHardLandingImpact, groundProbe.IsNearGround(motor.VerticalSpeed, motor.IsGrounded), motor.LastLocomotionTransition, dodge.LastTransition, currentPresentationPhase, commands);
    }

    public PlayerMovementPresentationCommand CompleteCurrentPresentationPhase()
    {
        if (currentPresentationPhase == PlayerMovementPresentationPhase.Starting || currentPresentationPhase == PlayerMovementPresentationPhase.Bridging)
        {
            currentPresentationPhase = PlayerMovementPresentationPhase.Looping;
            return new PlayerMovementPresentationCommand(PlayerMovementPresentationCommandType.LoopMode, currentPresentationMode, currentPresentationMode);
        }

        if (currentPresentationPhase == PlayerMovementPresentationPhase.Stopping)
        {
            currentPresentationPhase = PlayerMovementPresentationPhase.Idle;
        }

        return new PlayerMovementPresentationCommand(PlayerMovementPresentationCommandType.None, currentPresentationMode, currentPresentationMode);
    }

    private IReadOnlyList<PlayerMovementPresentationCommand> ConsumePresentationTransitions(PlayerLocomotionTransition locomotionTransition, PlayerDodgeLifecycleTransition dodgeTransition)
    {
        presentationCommands.Clear();
        if (dodgeTransition.SequenceId > lastDodgeSequence)
        {
            ConsumeDodgeTransition(dodgeTransition);
            lastDodgeSequence = dodgeTransition.SequenceId;
        }

        if (locomotionTransition.SequenceId > lastLocomotionSequence)
        {
            ConsumeLocomotionTransition(locomotionTransition);
            lastLocomotionSequence = locomotionTransition.SequenceId;
        }

        return presentationCommands.ToArray();
    }

    private void ConsumeDodgeTransition(PlayerDodgeLifecycleTransition transition)
    {
        switch (transition.LifecycleType)
        {
            case PlayerDodgeLifecycleType.Started:
                currentPresentationPhase = PlayerMovementPresentationPhase.Dodging;
                presentationCommands.Add(new PlayerMovementPresentationCommand(PlayerMovementPresentationCommandType.BeginDodge, currentPresentationMode, currentPresentationMode));
                break;
            case PlayerDodgeLifecycleType.Completed:
                PlayerLocomotionMode completedMode = ToLocomotionMode(transition.ExitMode);
                presentationCommands.Add(new PlayerMovementPresentationCommand(PlayerMovementPresentationCommandType.CompleteDodge, currentPresentationMode, completedMode));
                currentPresentationMode = completedMode;
                currentPresentationPhase = transition.ExitMode == PlayerDodgeExitMode.FastRun ? PlayerMovementPresentationPhase.Starting : PlayerMovementPresentationPhase.Idle;
                break;
            case PlayerDodgeLifecycleType.Cancelled:
                currentPresentationPhase = transition.CancelReason == DodgeCancelReason.Jumped || transition.CancelReason == DodgeCancelReason.BecameAirborne ? PlayerMovementPresentationPhase.Suspended : PlayerMovementPresentationPhase.Idle;
                presentationCommands.Add(new PlayerMovementPresentationCommand(PlayerMovementPresentationCommandType.CancelDodge, currentPresentationMode, ToLocomotionMode(transition.ExitMode)));
                break;
        }
    }

    private void ConsumeLocomotionTransition(PlayerLocomotionTransition transition)
    {
        currentPresentationMode = transition.CurrentMode;
        if (transition.CurrentMode == PlayerLocomotionMode.Air)
        {
            currentPresentationPhase = PlayerMovementPresentationPhase.Suspended;
            presentationCommands.Add(new PlayerMovementPresentationCommand(PlayerMovementPresentationCommandType.SuspendGroundLocomotion, transition.PreviousMode, transition.CurrentMode));
            return;
        }

        if (transition.CurrentMode == PlayerLocomotionMode.Idle)
        {
            currentPresentationPhase = transition.PreviousMode == PlayerLocomotionMode.Idle || transition.PreviousMode == PlayerLocomotionMode.Air ? PlayerMovementPresentationPhase.Idle : PlayerMovementPresentationPhase.Stopping;
            if (currentPresentationPhase == PlayerMovementPresentationPhase.Stopping)
            {
                presentationCommands.Add(new PlayerMovementPresentationCommand(PlayerMovementPresentationCommandType.StopMode, transition.PreviousMode, transition.CurrentMode));
            }

            return;
        }

        if (transition.PreviousMode == PlayerLocomotionMode.Idle || transition.PreviousMode == PlayerLocomotionMode.Air)
        {
            currentPresentationPhase = PlayerMovementPresentationPhase.Starting;
            presentationCommands.Add(new PlayerMovementPresentationCommand(PlayerMovementPresentationCommandType.StartMode, transition.PreviousMode, transition.CurrentMode));
            return;
        }

        currentPresentationPhase = PlayerMovementPresentationPhase.Bridging;
        presentationCommands.Add(new PlayerMovementPresentationCommand(PlayerMovementPresentationCommandType.BridgeMode, transition.PreviousMode, transition.CurrentMode));
    }

    private PlayerLocomotionMode ToLocomotionMode(PlayerDodgeExitMode exitMode)
    {
        return exitMode == PlayerDodgeExitMode.FastRun ? PlayerLocomotionMode.FastRun : PlayerLocomotionMode.Idle;
    }
}
