using System;
using UnityEngine;

/// <summary>
/// 以 Motor 的实际平面位移推进循环相位，并保留 Boundary Motion 的最后落地脚。
/// </summary>
public class PlayerLocomotionPhaseRuntime
{
    private readonly PlayerMotionCatalog catalog;
    private PlayerLocomotionMode mode;
    private PlayerMotionProfile profile;
    private PlayerFoot variantFoot;
    private PlayerFoot lastPlantFoot;
    private float normalizedPhase;
    private ulong loopMotionInstanceId;
    private bool hasLoop;

    public PlayerLocomotionPhaseRuntime(PlayerMotionCatalog motionCatalog)
    {
        catalog = motionCatalog;
    }

    public PlayerLocomotionPhaseSnapshot Snapshot => BuildSnapshot();

    /// <summary>
    /// 在本帧最终 Motion 决策和 Motor.Simulate 后提交相位。激活循环的帧不会消费此前位移。
    /// </summary>
    public void Commit(PlayerLocomotionMode locomotionMode, PlayerMotorResult motorResult, PlayerMotionSnapshot motion)
    {
        UpdateLastPlantFootFromBoundary(motion);
        if (!PlayerLocomotionCycleDefinition.IsGroundLoopMode(locomotionMode) || !motorResult.IsGrounded)
        {
            CloseCycle(locomotionMode);
            return;
        }
        if (motion.IsActive && !motion.HandoffActive)
        {
            PauseForBoundary(locomotionMode);
            return;
        }
        bool modeChanged = !hasLoop || mode != locomotionMode;
        bool enteredHandoff = motion.IsActive && motion.HandoffActive && loopMotionInstanceId != motion.InstanceId;
        if (modeChanged || enteredHandoff || motion.JustCompleted || motion.JustCancelled)
        {
            ActivateCycle(locomotionMode);
            loopMotionInstanceId = motion.ActiveDefinition != null ? motion.InstanceId : 0;
            return;
        }
        normalizedPhase = Mathf.Repeat(normalizedPhase + motorResult.ActualPlanarDisplacement.magnitude / profile.CycleDistance, 1f);
        ResolveCurrentPlantFeet(out PlayerFoot resolvedLastFoot, out _, out _);
        lastPlantFoot = resolvedLastFoot;
    }

    private void ActivateCycle(PlayerLocomotionMode locomotionMode)
    {
        if (catalog == null || !catalog.TryGetCycle(locomotionMode, out PlayerLocomotionCycleDefinition definition)) throw new InvalidOperationException("PlayerMotionCatalog 缺少 " + locomotionMode + " 的 Locomotion Cycle。");
        if (!definition.TryResolveProfile(lastPlantFoot, out PlayerMotionProfile selectedProfile, out PlayerFoot selectedVariantFoot)) throw new InvalidOperationException(locomotionMode + " Locomotion Cycle 缺少 " + selectedVariantFoot + " Loop Profile。");
        profile = selectedProfile;
        variantFoot = selectedVariantFoot;
        mode = locomotionMode;
        normalizedPhase = 0f;
        hasLoop = true;
        ResolveCurrentPlantFeet(out PlayerFoot resolvedLastFoot, out _, out _);
        lastPlantFoot = resolvedLastFoot;
    }

    private void UpdateLastPlantFootFromBoundary(PlayerMotionSnapshot motion)
    {
        if (motion.ActiveProfile == null) return;
        PlayerFoot fallback = motion.EntryLastPlantFoot == PlayerFoot.Unknown ? lastPlantFoot : motion.EntryLastPlantFoot;
        lastPlantFoot = motion.ActiveProfile.ResolveLastPlantFoot(motion.Progress, fallback);
    }

    private void PauseForBoundary(PlayerLocomotionMode locomotionMode)
    {
        mode = locomotionMode;
        profile = null;
        variantFoot = PlayerFoot.Unknown;
        normalizedPhase = 0f;
        loopMotionInstanceId = 0;
        hasLoop = false;
    }

    private void CloseCycle(PlayerLocomotionMode locomotionMode)
    {
        mode = locomotionMode;
        profile = null;
        variantFoot = PlayerFoot.Unknown;
        normalizedPhase = 0f;
        loopMotionInstanceId = 0;
        hasLoop = false;
    }

    private PlayerLocomotionPhaseSnapshot BuildSnapshot()
    {
        if (!hasLoop) return new PlayerLocomotionPhaseSnapshot(false, false, mode, PlayerFoot.Unknown, 0f, lastPlantFoot, PlayerFoot.Unknown, 0f);
        ResolveCurrentPlantFeet(out PlayerFoot resolvedLastFoot, out PlayerFoot nextFoot, out float stepProgress);
        return new PlayerLocomotionPhaseSnapshot(true, true, mode, variantFoot, normalizedPhase, resolvedLastFoot, nextFoot, stepProgress);
    }

    private void ResolveCurrentPlantFeet(out PlayerFoot resolvedLastFoot, out PlayerFoot nextFoot, out float stepProgress)
    {
        if (!profile.TryEvaluateLoopPhase(normalizedPhase, out resolvedLastFoot, out nextFoot, out stepProgress)) throw new InvalidOperationException(profile.name + " 的 Loop Phase 配置无效。");
    }
}
