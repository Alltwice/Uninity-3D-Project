using System;
using UnityEngine;

/// <summary>
/// 将 Gameplay transition/intent 解析成唯一 MotionDefinition，不接触动画或 CharacterController。
/// </summary>
public sealed class PlayerMotionPlanner : MonoBehaviour
{
    [SerializeField] private PlayerMotionCatalog catalog;

    private readonly PlayerMotionRuntime runtime = new PlayerMotionRuntime();

    public PlayerMotionCatalog Catalog => catalog;
    public PlayerMotionFrame CurrentFrame => runtime.CurrentFrame;
    public PlayerMotionSnapshot Snapshot => runtime.Snapshot;

    public void BeginFrame() => runtime.BeginFrame();

    public void HandleStateTransition(PlayerStateTransition transition, PlayerGameplayIntent intent, PlayerMotorResult motorResult)
    {
        if (TryResolveTransitionMotion(transition, intent, out PlayerMotionDefinition definition))
        {
            Begin(definition, intent, motorResult);
            return;
        }
        if (runtime.Snapshot.IsActive) runtime.Cancel();
    }

    public void ResolveContinuousMotion(Type stateType, PlayerGameplayIntent intent, PlayerMotorResult motorResult)
    {
        if (runtime.Snapshot.IsActive || intent.DesiredMoveDirection.sqrMagnitude < 0.0001f) return;
        PlayerMotionId left;
        PlayerMotionId right;
        if (stateType == typeof(PlayerWalkState))
        {
            left = PlayerMotionId.WalkTurn180Left;
            right = PlayerMotionId.WalkTurn180Right;
        }
        else if (stateType == typeof(PlayerRunState))
        {
            left = PlayerMotionId.RunTurn180Left;
            right = PlayerMotionId.RunTurn180Right;
        }
        else if (stateType == typeof(PlayerFastRunState))
        {
            left = PlayerMotionId.FastRunTurn180Left;
            right = PlayerMotionId.FastRunTurn180Right;
        }
        else return;
        Vector3 reference = motorResult.HorizontalVelocity.sqrMagnitude > 0.0001f ? motorResult.HorizontalVelocity : transform.forward;
        float signedAngle = SignedPlanarAngle(reference, intent.DesiredMoveDirection);
        if (Mathf.Abs(signedAngle) < catalog.Turn180Threshold) return;
        if (catalog.TryGet(signedAngle < 0f ? left : right, out PlayerMotionDefinition definition)) Begin(definition, intent, motorResult);
    }

    public PlayerMotionFrame Advance(float deltaTime, PlayerGameplayIntent intent)
    {
        return runtime.Advance(deltaTime, intent, transform.forward, catalog.TurnIntentTolerance, catalog.TurnRotationUnlockAngle);
    }

    private bool TryResolveTransitionMotion(PlayerStateTransition transition, PlayerGameplayIntent intent, out PlayerMotionDefinition definition)
    {
        Type previous = transition.PreviousStateType;
        Type current = transition.CurrentStateType;
        PlayerMotionId id;
        if (current == typeof(PlayerDodgeState)) id = PlayerMotionId.Dodge;
        else if (previous == typeof(PlayerIdleState) && current == typeof(PlayerWalkState)) id = ResolveStartId(PlayerMotionId.IdleToWalk, PlayerMotionId.WalkStart180Left, PlayerMotionId.WalkStart180Right, intent);
        else if (previous == typeof(PlayerIdleState) && current == typeof(PlayerRunState)) id = ResolveStartId(PlayerMotionId.IdleToRun, PlayerMotionId.RunStart180Left, PlayerMotionId.RunStart180Right, intent);
        else if (previous == typeof(PlayerWalkState) && current == typeof(PlayerIdleState)) id = PlayerMotionId.WalkToIdle;
        else if (previous == typeof(PlayerRunState) && current == typeof(PlayerIdleState)) id = PlayerMotionId.RunToIdle;
        else if (previous == typeof(PlayerFastRunState) && current == typeof(PlayerIdleState)) id = PlayerMotionId.FastRunToIdle;
        else { definition = null; return false; }
        return catalog.TryGet(id, out definition);
    }

    private PlayerMotionId ResolveStartId(PlayerMotionId standard, PlayerMotionId left, PlayerMotionId right, PlayerGameplayIntent intent)
    {
        float signedAngle = SignedPlanarAngle(transform.forward, intent.DesiredMoveDirection);
        if (Mathf.Abs(signedAngle) < catalog.Turn180Threshold) return standard;
        PlayerMotionId turnId = signedAngle < 0f ? left : right;
        return catalog.TryGet(turnId, out _) ? turnId : standard;
    }

    private void Begin(PlayerMotionDefinition definition, PlayerGameplayIntent intent, PlayerMotorResult motorResult)
    {
        Vector3 desired = intent.DesiredMoveDirection.sqrMagnitude > 0.0001f ? intent.DesiredMoveDirection : transform.forward;
        Vector3 entryVelocity = motorResult.HorizontalVelocity.sqrMagnitude > 0.0001f ? motorResult.HorizontalVelocity : transform.forward;
        Vector3 basis = definition.BasisPolicy == PlayerMotionBasisPolicy.DesiredDirection ? desired : definition.BasisPolicy == PlayerMotionBasisPolicy.EntryVelocityDirection ? entryVelocity : transform.forward;
        runtime.Begin(definition, basis, desired, desired);
    }

    private static float SignedPlanarAngle(Vector3 from, Vector3 to)
    {
        from.y = 0f;
        to.y = 0f;
        return from.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f ? 0f : Vector3.SignedAngle(from, to, Vector3.up);
    }
}
