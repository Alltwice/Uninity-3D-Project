using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 定义一个地面循环模式的三种起步脚变体；Unknown 始终使用 Default。
/// </summary>
[Serializable]
public class PlayerLocomotionCycleDefinition
{
    [SerializeField] private PlayerLocomotionMode mode;
    [SerializeField] private PlayerMotionProfile defaultProfile;
    [SerializeField] private PlayerMotionProfile leftProfile;
    [SerializeField] private PlayerMotionProfile rightProfile;

    public PlayerLocomotionMode Mode => mode;
    public PlayerMotionProfile DefaultProfile => defaultProfile;
    public PlayerMotionProfile LeftProfile => leftProfile;
    public PlayerMotionProfile RightProfile => rightProfile;

    public bool TryResolveProfile(PlayerFoot requestedVariantFoot, out PlayerMotionProfile profile, out PlayerFoot resolvedVariantFoot)
    {
        resolvedVariantFoot = requestedVariantFoot == PlayerFoot.Left || requestedVariantFoot == PlayerFoot.Right ? requestedVariantFoot : PlayerFoot.Unknown;
        profile = resolvedVariantFoot == PlayerFoot.Left ? leftProfile : resolvedVariantFoot == PlayerFoot.Right ? rightProfile : defaultProfile;
        return profile != null;
    }

    public bool Validate(ICollection<string> errors)
    {
        bool valid = true;
        if (!IsGroundLoopMode(mode)) { errors?.Add("Locomotion Cycle: Mode 必须是 Walk、Run 或 FastRun。"); valid = false; }
        valid &= ValidateProfile(defaultProfile, "Default", errors);
        valid &= ValidateProfile(leftProfile, "Left", errors);
        valid &= ValidateProfile(rightProfile, "Right", errors);
        return valid;
    }

#if UNITY_EDITOR
    public void Configure(PlayerLocomotionMode locomotionMode, PlayerMotionProfile defaultLoopProfile, PlayerMotionProfile leftLoopProfile, PlayerMotionProfile rightLoopProfile)
    {
        mode = locomotionMode;
        defaultProfile = defaultLoopProfile;
        leftProfile = leftLoopProfile;
        rightProfile = rightLoopProfile;
    }
#endif

    public static bool IsGroundLoopMode(PlayerLocomotionMode locomotionMode)
    {
        return locomotionMode == PlayerLocomotionMode.Walk || locomotionMode == PlayerLocomotionMode.Run || locomotionMode == PlayerLocomotionMode.FastRun;
    }

    private static bool ValidateProfile(PlayerMotionProfile profile, string label, ICollection<string> errors)
    {
        if (profile == null)
        {
            errors?.Add("Locomotion Cycle." + label + ": 缺少 Loop Profile。");
            return false;
        }
        return profile.ValidateLoopPhase(errors);
    }
}
