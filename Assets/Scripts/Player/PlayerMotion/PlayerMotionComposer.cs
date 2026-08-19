using UnityEngine;
/// <summary>
/// 整合运动数据和输入意图为最终的运动命令
/// </summary>
public static class PlayerMotionComposer
{
    public static PlayerMotorCommand Compose(PlayerGameplayIntent intent, PlayerMotionFrame motionFrame, PlayerMotorResult previousMotorResult, PlayerMovementConfig config, float deltaTime, Vector3 currentFacing)
    {
        //目标速度
        Vector3 targetVelocity = intent.DesiredMoveDirection * ResolveSpeed(intent.LocomotionMode, config.Locomotion);
        //在加速度影响下每帧真实速度
        Vector3 predictedVelocity = CalculateVelocity(previousMotorResult.HorizontalVelocity, targetVelocity, intent.LocomotionMode, config.Locomotion, deltaTime);
        PlayerMotorTranslationMode translationMode = PlayerMotorTranslationMode.VelocityDriven;
        Vector3 displacement = Vector3.zero;
        //烘焙和程序混合态时的位移信息
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
    /// <summary>
    /// 每帧真实需要移动的数据
    /// </summary>
    public static Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 targetVelocity, PlayerLocomotionMode locomotionMode, PlayerMovementConfig.LocomotionSettings settings, float deltaTime)
    {
        currentVelocity.y = 0f;
        targetVelocity.y = 0f;
        float acceleration = ResolveAcceleration(currentVelocity, targetVelocity, locomotionMode, settings);
        return Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * Mathf.Max(0f, deltaTime));
    }
    /// <summary>
    /// 处理加减速以及地面转向
    /// </summary>
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
    /// <summary>
    /// 依据枚举拿到对应的速度
    /// </summary>
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
    /// <summary>
    /// 处理旋转
    /// </summary>
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
        //选用动画曲线旋转但又存在程序操作时
        if (policy == PlayerMotionRotationPolicy.ProfileYaw && frame.RotationAuthority > 0f)
        {
            //动画旋转数据
            float authored = frame.AuthoredYawDelta * Mathf.Clamp01(frame.RotationAuthority);
            //程序旋转数据
            float desired = CalculateFacingYawDelta(currentFacing, facingDirection, settings.RotationSmoothSpeed, deltaTime) * (1f - Mathf.Clamp01(frame.RotationAuthority));
            yawDelta = authored + desired;
            //数据平滑过了直接使用
            mode = PlayerMotorRotationMode.YawDelta;
            return;
        }
        //如果时纯动画/程序数据直接平滑旋转即可
        mode = policy == PlayerMotionRotationPolicy.KeepFacing || policy == PlayerMotionRotationPolicy.None ? PlayerMotorRotationMode.None : facingDirection.sqrMagnitude > 0.0001f ? PlayerMotorRotationMode.FaceDirection : PlayerMotorRotationMode.None;
    }
    /// <summary>
    /// 处理平滑旋转
    /// </summary>
    private static float CalculateFacingYawDelta(Vector3 currentFacing, Vector3 desiredFacing, float smoothSpeed, float deltaTime)
    {
        currentFacing.y = 0f;
        desiredFacing.y = 0f;
        if (currentFacing.sqrMagnitude < 0.0001f || desiredFacing.sqrMagnitude < 0.0001f) return 0f;
        float angle = Vector3.SignedAngle(currentFacing, desiredFacing, Vector3.up);
        //代码比较复杂，主要用于平滑旋转
        //第三个参数的Exp用于计算e的x次方，注意符号，e的负指次方为一条无线趋近于0的曲线，而1-这个数值则是相反
        //这恰好可以作为平滑旋转的参数，先慢后快
        return Mathf.Lerp(0f, angle, 1f - Mathf.Exp(-smoothSpeed * Mathf.Max(0f, deltaTime)));
    }
}
