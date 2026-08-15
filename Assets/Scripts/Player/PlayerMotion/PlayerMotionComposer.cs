using UnityEngine;

public static class PlayerMotionComposer
{
    public static PlayerMotorCommand Compose(PlayerGameplayIntent intent, PlayerMotionFrame motionFrame, PlayerMotorResult previousMotorResult, PlayerMovementConfig config, float deltaTime, Vector3 currentFacing)
    {
        Vector3 targetVelocity = intent.DesiredMoveDirection * ResolveSpeed(intent.LocomotionMode, config.Locomotion);
        Vector3 predictedVelocity = CalculateVelocity(previousMotorResult.HorizontalVelocity, targetVelocity, intent.LocomotionMode, config.Locomotion, deltaTime);
        PlayerMotorTranslationMode translationMode = PlayerMotorTranslationMode.VelocityDriven;
        Vector3 displacement = Vector3.zero;
        if (motionFrame.IsValid && motionFrame.TranslationAuthority > 0f && motionFrame.Definition.TranslationPolicy != PlayerMotionTranslationPolicy.VelocityDriven)
        {
            float authority = Mathf.Clamp01(motionFrame.TranslationAuthority);
            displacement = motionFrame.AuthoredPlanarDisplacement * authority + predictedVelocity * deltaTime * (1f - authority);
            translationMode = PlayerMotorTranslationMode.DisplacementDriven;
        }
        ResolveRotation(intent, motionFrame, config.Locomotion, deltaTime, currentFacing, out PlayerMotorRotationMode rotationMode, out Vector3 facingDirection, out float yawDelta);
        float acceleration = ResolveAcceleration(previousMotorResult.HorizontalVelocity, targetVelocity, intent.LocomotionMode, config.Locomotion);
        return new PlayerMotorCommand(translationMode, targetVelocity, acceleration, displacement, rotationMode, facingDirection, yawDelta, intent.HasVerticalImpulse, intent.VerticalImpulse);
    }

    public static Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 targetVelocity, PlayerLocomotionMode locomotionMode, PlayerMovementConfig.LocomotionSettings settings, float deltaTime)
    {
        currentVelocity.y = 0f;
        targetVelocity.y = 0f;
        float acceleration = ResolveAcceleration(currentVelocity, targetVelocity, locomotionMode, settings);
        return Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * Mathf.Max(0f, deltaTime));
    }

    private static float ResolveAcceleration(Vector3 currentVelocity, Vector3 targetVelocity, PlayerLocomotionMode locomotionMode, PlayerMovementConfig.LocomotionSettings settings)
    {
        float acceleration = locomotionMode == PlayerLocomotionMode.Air ? settings.AirAcceleration : settings.GroundAcceleration;
        if (locomotionMode == PlayerLocomotionMode.Idle) return settings.GroundDeceleration;
        if (currentVelocity.sqrMagnitude > 0.0001f && targetVelocity.sqrMagnitude > 0.0001f)
        {
            float alignment = Vector3.Dot(currentVelocity.normalized, targetVelocity.normalized);
            if (alignment < 0.8f) acceleration = settings.GroundTurnAcceleration;
            else if (currentVelocity.magnitude > targetVelocity.magnitude) acceleration = settings.GroundDeceleration;
        }
        return acceleration;
    }

    private static float ResolveSpeed(PlayerLocomotionMode locomotionMode, PlayerMovementConfig.LocomotionSettings settings)
    {
        switch (locomotionMode)
        {
            case PlayerLocomotionMode.Walk: return settings.WalkSpeed;
            case PlayerLocomotionMode.Run: return settings.RunSpeed;
            case PlayerLocomotionMode.FastRun: return settings.FastRunSpeed;
            case PlayerLocomotionMode.Air: return settings.AirMoveSpeed;
            default: return 0f;
        }
    }

    private static void ResolveRotation(PlayerGameplayIntent intent, PlayerMotionFrame frame, PlayerMovementConfig.LocomotionSettings settings, float deltaTime, Vector3 currentFacing, out PlayerMotorRotationMode mode, out Vector3 facingDirection, out float yawDelta)
    {
        facingDirection = intent.DesiredFacingDirection;
        yawDelta = 0f;
        if (!frame.IsValid)
        {
            mode = facingDirection.sqrMagnitude > 0.0001f ? PlayerMotorRotationMode.FaceDirection : PlayerMotorRotationMode.None;
            return;
        }
        PlayerMotionRotationPolicy policy = frame.Definition.RotationPolicy;
        if (policy == PlayerMotionRotationPolicy.ProfileYaw && frame.RotationAuthority > 0f)
        {
            float authored = frame.AuthoredYawDelta * Mathf.Clamp01(frame.RotationAuthority);
            float desired = CalculateFacingYawDelta(currentFacing, facingDirection, settings.RotationSmoothSpeed, deltaTime) * (1f - Mathf.Clamp01(frame.RotationAuthority));
            yawDelta = authored + desired;
            mode = PlayerMotorRotationMode.YawDelta;
            return;
        }
        mode = policy == PlayerMotionRotationPolicy.KeepFacing || policy == PlayerMotionRotationPolicy.None ? PlayerMotorRotationMode.None : facingDirection.sqrMagnitude > 0.0001f ? PlayerMotorRotationMode.FaceDirection : PlayerMotorRotationMode.None;
    }

    private static float CalculateFacingYawDelta(Vector3 currentFacing, Vector3 desiredFacing, float smoothSpeed, float deltaTime)
    {
        currentFacing.y = 0f;
        desiredFacing.y = 0f;
        if (currentFacing.sqrMagnitude < 0.0001f || desiredFacing.sqrMagnitude < 0.0001f) return 0f;
        float angle = Vector3.SignedAngle(currentFacing, desiredFacing, Vector3.up);
        return Mathf.Lerp(0f, angle, 1f - Mathf.Exp(-smoothSpeed * Mathf.Max(0f, deltaTime)));
    }
}
