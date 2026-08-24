using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif
/// <summary>
/// 动画数据和动画片段的绑定
/// </summary>
[Serializable]
public class PlayerMotionAnimationBinding
{
    [SerializeField] private PlayerMotionDefinition definition;
    [FormerlySerializedAs("transition")] [SerializeField] private ClipTransition defaultTransition = new ClipTransition();
    [SerializeField] private ClipTransition leftTransition = new ClipTransition();
    [SerializeField] private ClipTransition rightTransition = new ClipTransition();
    [SerializeField] private AnimationCurve poseFadeWeight = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public PlayerMotionDefinition Definition => definition;
    public ClipTransition Transition => defaultTransition;
    public ClipTransition LeftTransition => leftTransition;
    public ClipTransition RightTransition => rightTransition;
    public float EvaluatePoseFade(float handoffProgress) => Mathf.Clamp01(poseFadeWeight == null ? handoffProgress : poseFadeWeight.Evaluate(handoffProgress));

    public ClipTransition ResolveTransition(PlayerMotionProfile selectedProfile)
    {
        if (definition != null && selectedProfile != null)
        {
            if (selectedProfile == definition.LeftFootProfile && leftTransition != null && leftTransition.Clip != null) return leftTransition;
            if (selectedProfile == definition.RightFootProfile && rightTransition != null && rightTransition.Clip != null) return rightTransition;
        }
        return defaultTransition;
    }

    public bool Validate(ICollection<string> errors)
    {
        bool valid = definition != null && defaultTransition != null && defaultTransition.Clip != null;
        if (!valid) errors?.Add("Motion Binding 缺少默认 Definition 或 Clip。");
#if UNITY_EDITOR
        if (definition != null) valid &= PlayerAnimationSet.ValidateProfileClip(definition.Profile, defaultTransition, definition.name + ".Default", errors);
#endif
        if (definition == null || !definition.RequiresFootProfiles) return valid;
        if (leftTransition == null || leftTransition.Clip == null) { errors?.Add(definition.name + ": 缺少 Left Foot Animation Binding。"); valid = false; }
        if (rightTransition == null || rightTransition.Clip == null) { errors?.Add(definition.name + ": 缺少 Right Foot Animation Binding。"); valid = false; }
#if UNITY_EDITOR
        valid &= PlayerAnimationSet.ValidateProfileClip(definition.LeftFootProfile, leftTransition, definition.name + ".Left", errors);
        valid &= PlayerAnimationSet.ValidateProfileClip(definition.RightFootProfile, rightTransition, definition.name + ".Right", errors);
#endif
        return valid;
    }

#if UNITY_EDITOR
    public void Configure(PlayerMotionDefinition motionDefinition, AnimationClip clip, float fadeDuration)
    {
        definition = motionDefinition;
        ConfigureTransition(ref defaultTransition, clip, fadeDuration);
        poseFadeWeight = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    public void ConfigureFoot(PlayerFoot foot, AnimationClip clip, float fadeDuration)
    {
        if (foot == PlayerFoot.Left) ConfigureTransition(ref leftTransition, clip, fadeDuration);
        else if (foot == PlayerFoot.Right) ConfigureTransition(ref rightTransition, clip, fadeDuration);
    }

    private static void ConfigureTransition(ref ClipTransition transition, AnimationClip clip, float fadeDuration)
    {
        transition ??= new ClipTransition();
        transition.Clip = clip;
        transition.FadeDuration = fadeDuration;
        transition.Speed = 1f;
    }
#endif
}
/// <summary>
/// 专门用于处理脚步相位情况下的循环动画选用
/// </summary>
[Serializable]
public class PlayerLoopAnimationPair
{
    [FormerlySerializedAs("transition")] [SerializeField] private ClipTransition defaultTransition = new ClipTransition();
    [SerializeField] private ClipTransition leftTransition = new ClipTransition();
    [SerializeField] private ClipTransition rightTransition = new ClipTransition();
    [SerializeField] private PlayerMotionProfile defaultProfile;
    [SerializeField] private PlayerMotionProfile leftProfile;
    [SerializeField] private PlayerMotionProfile rightProfile;

    public ClipTransition DefaultTransition => defaultTransition;
    public ClipTransition LeftTransition => leftTransition;
    public ClipTransition RightTransition => rightTransition;
    public PlayerMotionProfile DefaultProfile => defaultProfile;
    public PlayerMotionProfile LeftProfile => leftProfile;
    public PlayerMotionProfile RightProfile => rightProfile;

    public PlayerAnimationSelection Resolve(PlayerFoot foot)
    {
        if (foot == PlayerFoot.Left && leftTransition != null && leftTransition.Clip != null && leftProfile != null) return new PlayerAnimationSelection(leftTransition, leftProfile);
        if (foot == PlayerFoot.Right && rightTransition != null && rightTransition.Clip != null && rightProfile != null) return new PlayerAnimationSelection(rightTransition, rightProfile);
        return new PlayerAnimationSelection(defaultTransition, defaultProfile);
    }

    public bool Validate(string label, ICollection<string> errors)
    {
        bool valid = defaultTransition != null && defaultTransition.Clip != null && defaultProfile != null;
        if (!valid) errors?.Add(label + ": 缺少默认循环 Clip 或 Profile。");
        if (leftTransition == null || leftTransition.Clip == null || leftProfile == null) { errors?.Add(label + ": 缺少 Left Foot 循环 Clip/Profile。"); valid = false; }
        if (rightTransition == null || rightTransition.Clip == null || rightProfile == null) { errors?.Add(label + ": 缺少 Right Foot 循环 Clip/Profile。"); valid = false; }
#if UNITY_EDITOR
        valid &= PlayerAnimationSet.ValidateProfileClip(defaultProfile, defaultTransition, label + ".Default", errors);
        valid &= PlayerAnimationSet.ValidateProfileClip(leftProfile, leftTransition, label + ".Left", errors);
        valid &= PlayerAnimationSet.ValidateProfileClip(rightProfile, rightTransition, label + ".Right", errors);
#endif
        return valid;
    }

#if UNITY_EDITOR
    public void Configure(PlayerFoot foot, PlayerMotionProfile motionProfile, AnimationClip clip, float fadeDuration)
    {
        if (foot == PlayerFoot.Left)
        {
            leftProfile = motionProfile;
            ConfigureTransition(ref leftTransition, clip, fadeDuration);
        }
        else if (foot == PlayerFoot.Right)
        {
            rightProfile = motionProfile;
            ConfigureTransition(ref rightTransition, clip, fadeDuration);
        }
        else
        {
            defaultProfile = motionProfile;
            ConfigureTransition(ref defaultTransition, clip, fadeDuration);
        }
    }

    private static void ConfigureTransition(ref ClipTransition transition, AnimationClip clip, float fadeDuration)
    {
        transition ??= new ClipTransition();
        transition.Clip = clip;
        transition.FadeDuration = fadeDuration;
        transition.Speed = 1f;
    }
#endif
}

public struct PlayerAnimationSelection
{
    public PlayerAnimationSelection(ClipTransition transition, PlayerMotionProfile profile)
    {
        Transition = transition;
        Profile = profile;
    }

    public ClipTransition Transition { get; }
    public PlayerMotionProfile Profile { get; }
    public bool IsValid => Transition != null && Transition.Clip != null;
}

[CreateAssetMenu(fileName = "PlayerAnimationSet", menuName = "Player/Animation Set")]
public class PlayerAnimationSet : ScriptableObject
{
    [SerializeField] private PlayerMotionCatalog motionCatalog;
    [SerializeField] private List<PlayerMotionAnimationBinding> motionBindings = new List<PlayerMotionAnimationBinding>();
    [SerializeField] private PlayerLoopAnimationPair walkLoop = new PlayerLoopAnimationPair();
    [SerializeField] private PlayerLoopAnimationPair runLoop = new PlayerLoopAnimationPair();
    [SerializeField] private PlayerLoopAnimationPair fastRunLoop = new PlayerLoopAnimationPair();

    public PlayerMotionCatalog MotionCatalog => motionCatalog;
    //只读接口泛型存储
    public IReadOnlyList<PlayerMotionAnimationBinding> MotionBindings => motionBindings;
    public PlayerLoopAnimationPair WalkLoop => walkLoop;
    public PlayerLoopAnimationPair RunLoop => runLoop;
    public PlayerLoopAnimationPair FastRunLoop => fastRunLoop;

    public bool TryGetBinding(PlayerMotionDefinition definition, out PlayerMotionAnimationBinding binding)
    {
        for (int i = 0; i < motionBindings.Count; i++)
        {
            if (motionBindings[i].Definition != definition) continue;
            binding = motionBindings[i];
            return true;
        }
        binding = null;
        return false;
    }

    public bool TryGetBinding(PlayerMotionDefinition definition, PlayerMotionProfile selectedProfile, out PlayerMotionAnimationBinding binding, out ClipTransition transition)
    {
        if (TryGetBinding(definition, out binding))
        {
            transition = binding.ResolveTransition(selectedProfile);
            return transition != null && transition.Clip != null;
        }
        transition = null;
        return false;
    }

    public bool TryResolveLoop(PlayerLocomotionMode locomotionMode, PlayerFoot foot, out PlayerAnimationSelection selection)
    {
        PlayerLoopAnimationPair pair = locomotionMode == PlayerLocomotionMode.Walk ? walkLoop : locomotionMode == PlayerLocomotionMode.Run ? runLoop : locomotionMode == PlayerLocomotionMode.FastRun ? fastRunLoop : null;
        if (pair != null)
        {
            selection = pair.Resolve(foot);
            return selection.IsValid;
        }
        selection = default;
        return false;
    }

    public bool Validate(ICollection<string> errors)
    {
        bool valid = true;
        HashSet<PlayerMotionDefinition> seen = new HashSet<PlayerMotionDefinition>();
        for (int i = 0; i < motionBindings.Count; i++)
        {
            PlayerMotionAnimationBinding binding = motionBindings[i];
            if (binding == null || binding.Definition == null || binding.Transition == null || binding.Transition.Clip == null) { errors?.Add(name + ": Binding " + i + " 缺少 Definition 或 Clip。"); valid = false; continue; }
            valid &= binding.Validate(errors);
            if (!seen.Add(binding.Definition)) { errors?.Add(name + ": " + binding.Definition.name + " 存在重复 Binding。"); valid = false; }
        }
        if (motionCatalog == null) { errors?.Add(name + ": 缺少 MotionCatalog。"); return false; }
        for (int i = 0; i < motionCatalog.Motions.Count; i++)
        {
            PlayerMotionDefinition definition = motionCatalog.Motions[i].Definition;
            if (definition == null) { errors?.Add(name + ": Catalog Entry " + i + " 缺少 Definition。"); valid = false; continue; }
            valid &= definition.Validate(errors);
            if (definition.RequiresPresentation && !seen.Contains(definition)) { errors?.Add(name + ": " + definition.name + " 缺少 Animation Binding。"); valid = false; }
        }
        if (walkLoop == null) { errors?.Add(name + ": 缺少 Walk Loop Pair。"); valid = false; }
        else valid &= walkLoop.Validate(name + ".WalkLoop", errors);
        if (runLoop == null) { errors?.Add(name + ": 缺少 Run Loop Pair。"); valid = false; }
        else valid &= runLoop.Validate(name + ".RunLoop", errors);
        if (fastRunLoop == null) { errors?.Add(name + ": 缺少 Sprint Loop Pair。"); valid = false; }
        else valid &= fastRunLoop.Validate(name + ".SprintLoop", errors);
        return valid;
    }

#if UNITY_EDITOR
    internal static bool ValidateProfileClip(PlayerMotionProfile profile, ClipTransition transition, string label, ICollection<string> errors)
    {
        if (profile == null || transition == null || transition.Clip == null) return false;
        PlayerMotionProfileMetadata metadata = profile.EditorMetadata;
        if (metadata == null || string.IsNullOrEmpty(metadata.SourceClipGuid) || metadata.SourceClipLocalId == 0)
        {
            errors?.Add(label + ": Profile 缺少源动画元数据。");
            return false;
        }
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(transition.Clip, out string clipGuid, out long clipLocalId);
        if (clipGuid != metadata.SourceClipGuid || clipLocalId != metadata.SourceClipLocalId)
        {
            errors?.Add(label + ": Clip 与 MotionProfile 源动画不一致。");
            return false;
        }
        return true;
    }
#endif

#if UNITY_EDITOR
    public void Configure(PlayerMotionCatalog catalog, IEnumerable<PlayerMotionAnimationBinding> bindings)
    {
        motionCatalog = catalog;
        motionBindings.Clear();
        motionBindings.AddRange(bindings);
    }

    public void ConfigureLoop(PlayerLocomotionMode locomotionMode, PlayerFoot foot, PlayerMotionProfile profile, AnimationClip clip, float fadeDuration)
    {
        PlayerLoopAnimationPair pair = locomotionMode == PlayerLocomotionMode.Walk ? walkLoop : locomotionMode == PlayerLocomotionMode.Run ? runLoop : fastRunLoop;
        pair ??= new PlayerLoopAnimationPair();
        if (locomotionMode == PlayerLocomotionMode.Walk) walkLoop = pair;
        else if (locomotionMode == PlayerLocomotionMode.Run) runLoop = pair;
        else fastRunLoop = pair;
        pair.Configure(foot, profile, clip, fadeDuration);
    }
#endif
}
