using UnityEngine;

public enum PlayerLocomotionMode
{
    Idle,
    Walk,
    Run,
    FastRun,
    Dodge,
    Air
}

public enum PlayerMotorTranslationMode
{
    VelocityDriven,
    DisplacementDriven
}

public enum PlayerMotorRotationMode
{
    None,
    FaceDirection,
    YawDelta
}

public struct PlayerGameplayIntent
{
    public PlayerLocomotionMode LocomotionMode;
    public Vector3 DesiredMoveDirection;
    public Vector3 DesiredFacingDirection;
    public float VerticalImpulse;
    public bool HasVerticalImpulse;

    public static PlayerGameplayIntent Create(Vector3 desiredMoveDirection, Vector3 currentFacing)
    {
        desiredMoveDirection.y = 0f;
        currentFacing.y = 0f;
        if (desiredMoveDirection.sqrMagnitude > 1f) desiredMoveDirection.Normalize();
        return new PlayerGameplayIntent
        {
            LocomotionMode = PlayerLocomotionMode.Idle,
            DesiredMoveDirection = desiredMoveDirection,
            DesiredFacingDirection = desiredMoveDirection.sqrMagnitude > 0.0001f ? desiredMoveDirection.normalized : currentFacing.normalized
        };
    }

    public void RequestVerticalImpulse(float impulse)
    {
        VerticalImpulse = impulse;
        HasVerticalImpulse = true;
    }
}

public readonly struct PlayerMotorCommand
{
    public PlayerMotorCommand(PlayerMotorTranslationMode translationMode, Vector3 targetPlanarVelocity, float planarAcceleration, Vector3 planarDisplacement, PlayerMotorRotationMode rotationMode, Vector3 desiredFacingDirection, float yawDelta, bool hasVerticalImpulse, float verticalImpulse)
    {
        TranslationMode = translationMode;
        TargetPlanarVelocity = targetPlanarVelocity;
        PlanarAcceleration = planarAcceleration;
        PlanarDisplacement = planarDisplacement;
        RotationMode = rotationMode;
        DesiredFacingDirection = desiredFacingDirection;
        YawDelta = yawDelta;
        HasVerticalImpulse = hasVerticalImpulse;
        VerticalImpulse = verticalImpulse;
    }

    public PlayerMotorTranslationMode TranslationMode { get; }
    public Vector3 TargetPlanarVelocity { get; }
    public float PlanarAcceleration { get; }
    public Vector3 PlanarDisplacement { get; }
    public PlayerMotorRotationMode RotationMode { get; }
    public Vector3 DesiredFacingDirection { get; }
    public float YawDelta { get; }
    public bool HasVerticalImpulse { get; }
    public float VerticalImpulse { get; }
}

public readonly struct PlayerMotorResult
{
    public PlayerMotorResult(Vector3 actualDisplacement, Vector3 actualPlanarDisplacement, Vector3 horizontalVelocity, float verticalVelocity, bool isGrounded, bool justLanded, float landingImpactSpeed, CollisionFlags collisionFlags)
    {
        ActualDisplacement = actualDisplacement;
        ActualPlanarDisplacement = actualPlanarDisplacement;
        HorizontalVelocity = horizontalVelocity;
        VerticalVelocity = verticalVelocity;
        IsGrounded = isGrounded;
        JustLanded = justLanded;
        LandingImpactSpeed = landingImpactSpeed;
        CollisionFlags = collisionFlags;
    }

    public Vector3 ActualDisplacement { get; }
    public Vector3 ActualPlanarDisplacement { get; }
    public Vector3 HorizontalVelocity { get; }
    public float HorizontalSpeed => HorizontalVelocity.magnitude;
    public float VerticalVelocity { get; }
    public bool IsGrounded { get; }
    public bool JustLanded { get; }
    public float LandingImpactSpeed { get; }
    public CollisionFlags CollisionFlags { get; }
}

public static class PlayerMotorKinematics
{
    public static Vector3 CalculateActualPlanarVelocity(Vector3 actualDisplacement, float deltaTime)
    {
        actualDisplacement.y = 0f;
        return deltaTime > 0f ? actualDisplacement / deltaTime : Vector3.zero;
    }
}
