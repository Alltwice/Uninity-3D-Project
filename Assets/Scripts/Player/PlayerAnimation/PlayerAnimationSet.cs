using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
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
    [SerializeField] private ClipTransition defaultTransition = new ClipTransition();
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

    internal bool Validate(string label, ICollection<string> errors)
    {
        bool valid = true;
        if (definition == null)
        {
            errors?.Add(label + ": 缺少 Definition。");
            return false;
        }
        valid &= ValidateTransition(defaultTransition, label + ".Default", errors);
#if UNITY_EDITOR
        valid &= PlayerAnimationSet.ValidateProfileClip(definition.Profile, defaultTransition, label + ".Default", errors);
#endif
        if (!definition.RequiresFootProfiles) return valid;
        valid &= PlayerAnimationSet.ValidateProfilePlantMarkers(definition.Profile, label + ".Default", errors);
        valid &= ValidateTransition(leftTransition, label + ".Left", errors);
        valid &= ValidateTransition(rightTransition, label + ".Right", errors);
        valid &= PlayerAnimationSet.ValidateProfilePlantMarkers(definition.LeftFootProfile, label + ".Left", errors);
        valid &= PlayerAnimationSet.ValidateProfilePlantMarkers(definition.RightFootProfile, label + ".Right", errors);
#if UNITY_EDITOR
        valid &= PlayerAnimationSet.ValidateProfileClip(definition.LeftFootProfile, leftTransition, label + ".Left", errors);
        valid &= PlayerAnimationSet.ValidateProfileClip(definition.RightFootProfile, rightTransition, label + ".Right", errors);
#endif
        return valid;
    }

    private static bool ValidateTransition(ClipTransition transition, string label, ICollection<string> errors)
    {
        if (transition != null && transition.Clip != null) return true;
        errors?.Add(label + ": 缺少 Clip。");
        return false;
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
/// Idle 表现资源
/// </summary>
[Serializable]
public class PlayerIdleAnimationGroup
{
    [SerializeField] private ClipTransition loop = new ClipTransition();

    public ClipTransition Loop => loop;
}

/// <summary>
/// 地面运动表现资源及其 MotionDefinition 绑定
/// </summary>
[Serializable]
public class PlayerLocomotionAnimationGroup
{
    [SerializeField] private PlayerLoopAnimationPair loop = new PlayerLoopAnimationPair();
    [SerializeField] private List<PlayerMotionAnimationBinding> motionBindings = new List<PlayerMotionAnimationBinding>();

    public PlayerLoopAnimationPair Loop => loop;
    public List<PlayerMotionAnimationBinding> MotionBindings => motionBindings;
}

/// <summary>
/// 跳跃、空中和落地表现资源
/// </summary>
[Serializable]
public class PlayerJumpAnimationGroup
{
    [SerializeField] private ClipTransition jumpStart = new ClipTransition();
    [SerializeField] private ClipTransition airLoop = new ClipTransition();
    [SerializeField] private ClipTransition landing = new ClipTransition();
    [SerializeField] private ClipTransition hardLanding = new ClipTransition();

    public ClipTransition JumpStart => jumpStart;
    public ClipTransition AirLoop => airLoop;
    public ClipTransition Landing => landing;
    public ClipTransition HardLanding => hardLanding;
}

/// <summary>
/// 不属于地面运动分类的 MotionDefinition 绑定
/// </summary>
[Serializable]
public class PlayerOtherAnimationGroup
{
    [SerializeField] private List<PlayerMotionAnimationBinding> motionBindings = new List<PlayerMotionAnimationBinding>();

    public List<PlayerMotionAnimationBinding> MotionBindings => motionBindings;
}

/// <summary>
/// 按当前脚选择循环动画及其 MotionProfile
/// </summary>
[Serializable]
public class PlayerLoopAnimationPair
{
    [SerializeField] private ClipTransition defaultTransition = new ClipTransition();
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
        return Validate(label, errors, null);
    }

    internal bool Validate(string label, ICollection<string> errors, HashSet<PlayerMotionProfile> validatedProfiles)
    {
        bool valid = true;
        valid &= ValidateSlot(defaultTransition, defaultProfile, label + ".Default", errors);
        valid &= ValidateSlot(leftTransition, leftProfile, label + ".Left", errors);
        valid &= ValidateSlot(rightTransition, rightProfile, label + ".Right", errors);
#if UNITY_EDITOR
        valid &= PlayerAnimationSet.ValidateProfileClip(defaultProfile, defaultTransition, label + ".Default", errors);
        valid &= PlayerAnimationSet.ValidateProfileClip(leftProfile, leftTransition, label + ".Left", errors);
        valid &= PlayerAnimationSet.ValidateProfileClip(rightProfile, rightTransition, label + ".Right", errors);
#endif
        if (validatedProfiles == null)
        {
            valid &= ValidateLoopProfile(defaultProfile, errors);
            valid &= ValidateLoopProfile(leftProfile, errors);
            valid &= ValidateLoopProfile(rightProfile, errors);
        }
        else
        {
            valid &= ValidateLoopProfile(defaultProfile, validatedProfiles, errors);
            valid &= ValidateLoopProfile(leftProfile, validatedProfiles, errors);
            valid &= ValidateLoopProfile(rightProfile, validatedProfiles, errors);
        }
        return valid;
    }

    private static bool ValidateSlot(ClipTransition transition, PlayerMotionProfile profile, string label, ICollection<string> errors)
    {
        bool valid = true;
        if (transition == null || transition.Clip == null) { errors?.Add(label + ": 缺少循环 Clip。"); valid = false; }
        if (profile == null) { errors?.Add(label + ": 缺少循环 Profile。"); valid = false; }
        return valid;
    }

    private static bool ValidateLoopProfile(PlayerMotionProfile profile, ICollection<string> errors)
    {
        return profile != null && profile.ValidateLoopPhase(errors);
    }

    private static bool ValidateLoopProfile(PlayerMotionProfile profile, HashSet<PlayerMotionProfile> validatedProfiles, ICollection<string> errors)
    {
        if (profile == null || !validatedProfiles.Add(profile)) return profile != null;
        return profile.ValidateLoopPhase(errors);
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

public enum PlayerAnimationCue
{
    JumpStart,
    Landing,
    HardLanding
}

[CreateAssetMenu(fileName = "PlayerAnimationSet", menuName = "Player/Animation Set")]
public class PlayerAnimationSet : ScriptableObject
{
    [SerializeField] private PlayerMotionCatalog motionCatalog;
    [SerializeField] private PlayerIdleAnimationGroup idle = new PlayerIdleAnimationGroup();
    [SerializeField] private PlayerLocomotionAnimationGroup walk = new PlayerLocomotionAnimationGroup();
    [SerializeField] private PlayerLocomotionAnimationGroup run = new PlayerLocomotionAnimationGroup();
    [SerializeField] private PlayerLocomotionAnimationGroup sprint = new PlayerLocomotionAnimationGroup();
    [SerializeField] private PlayerJumpAnimationGroup jump = new PlayerJumpAnimationGroup();
    [SerializeField] private PlayerOtherAnimationGroup other = new PlayerOtherAnimationGroup();

    public PlayerMotionCatalog MotionCatalog => motionCatalog;

    public IEnumerable<PlayerMotionAnimationBinding> MotionBindings => EnumerateMotionBindings();

    public bool TryGetBinding(PlayerMotionDefinition definition, PlayerMotionProfile selectedProfile, out PlayerMotionAnimationBinding binding, out ClipTransition transition)
    {
        foreach (PlayerMotionAnimationBinding candidate in MotionBindings)
        {
            if (candidate == null || candidate.Definition != definition) continue;
            binding = candidate;
            transition = candidate.ResolveTransition(selectedProfile);
            return transition != null && transition.Clip != null;
        }
        binding = null;
        transition = null;
        return false;
    }

    public bool TryResolveLoop(PlayerLocomotionMode locomotionMode, PlayerFoot foot, out PlayerAnimationSelection selection)
    {
        switch (locomotionMode)
        {
            case PlayerLocomotionMode.Idle:
            case PlayerLocomotionMode.HardLanding:
                selection = new PlayerAnimationSelection(idle?.Loop, null);
                return selection.IsValid;
            case PlayerLocomotionMode.Walk:
                selection = walk == null || walk.Loop == null ? default : walk.Loop.Resolve(foot);
                return selection.IsValid;
            case PlayerLocomotionMode.Run:
                selection = run == null || run.Loop == null ? default : run.Loop.Resolve(foot);
                return selection.IsValid;
            case PlayerLocomotionMode.FastRun:
                selection = sprint == null || sprint.Loop == null ? default : sprint.Loop.Resolve(foot);
                return selection.IsValid;
            case PlayerLocomotionMode.Air:
                selection = new PlayerAnimationSelection(jump?.AirLoop, null);
                return selection.IsValid;
            default:
                selection = default;
                return false;
        }
    }

    public bool TryResolveCue(PlayerAnimationCue cue, out ClipTransition transition)
    {
        transition = cue switch
        {
            PlayerAnimationCue.JumpStart => jump?.JumpStart,
            PlayerAnimationCue.Landing => jump?.Landing,
            PlayerAnimationCue.HardLanding => jump?.HardLanding,
            _ => null
        };
        if (transition != null && transition.Clip != null) return true;
        if (cue == PlayerAnimationCue.HardLanding && idle?.Loop != null && idle.Loop.Clip != null)
        {
            transition = idle.Loop;
            return true;
        }
        transition = null;
        return false;
    }

    public bool Validate(ICollection<string> errors)
    {
        bool valid = true;
        HashSet<PlayerMotionDefinition> seenDefinitions = new HashSet<PlayerMotionDefinition>();
        valid &= ValidateBindingGroup("Walk", walk?.MotionBindings, seenDefinitions, errors);
        valid &= ValidateBindingGroup("Run", run?.MotionBindings, seenDefinitions, errors);
        valid &= ValidateBindingGroup("Sprint", sprint?.MotionBindings, seenDefinitions, errors);
        valid &= ValidateBindingGroup("Other", other?.MotionBindings, seenDefinitions, errors);

        if (motionCatalog == null)
        {
            errors?.Add(name + ": 缺少 MotionCatalog。");
            valid = false;
        }
        else
        {
            for (int i = 0; i < motionCatalog.Motions.Count; i++)
            {
                PlayerMotionDefinition definition = motionCatalog.Motions[i].Definition;
                if (definition == null)
                {
                    errors?.Add(name + ": Catalog Entry " + i + " 缺少 Definition。");
                    valid = false;
                    continue;
                }
                valid &= definition.Validate(errors);
                if (definition.RequiresPresentation)
                {
                    int bindingCount = CountBindings(definition);
                    if (bindingCount != 1)
                    {
                        errors?.Add(name + ": " + definition.name + " 需要且只能有一个 Animation Binding，当前为 " + bindingCount + "。");
                        valid = false;
                    }
                }
            }
        }

        valid &= ValidateTransition(idle?.Loop, "Idle.Loop", errors);
        if (walk == null) { errors?.Add(name + ": Walk 分类缺失。"); valid = false; }
        else valid &= ValidateLoop(walk.Loop, "Walk.Loop", errors);
        if (run == null) { errors?.Add(name + ": Run 分类缺失。"); valid = false; }
        else valid &= ValidateLoop(run.Loop, "Run.Loop", errors);
        if (sprint == null) { errors?.Add(name + ": Sprint 分类缺失。"); valid = false; }
        else valid &= ValidateLoop(sprint.Loop, "Sprint.Loop", errors);
        valid &= ValidateTransition(jump?.JumpStart, "Jump.JumpStart", errors);
        valid &= ValidateTransition(jump?.AirLoop, "Jump.AirLoop", errors);
        valid &= ValidateTransition(jump?.Landing, "Jump.Landing", errors);
        valid &= ValidateTransition(jump?.HardLanding, "Jump.HardLanding", errors);
        if (jump == null) { errors?.Add(name + ": Jump 分类缺失。"); valid = false; }
        if (other == null) { errors?.Add(name + ": Other 分类缺失。"); valid = false; }
        return valid;
    }

    private IEnumerable<PlayerMotionAnimationBinding> EnumerateMotionBindings()
    {
        if (walk?.MotionBindings != null)
        {
            foreach (PlayerMotionAnimationBinding binding in walk.MotionBindings) yield return binding;
        }
        if (run?.MotionBindings != null)
        {
            foreach (PlayerMotionAnimationBinding binding in run.MotionBindings) yield return binding;
        }
        if (sprint?.MotionBindings != null)
        {
            foreach (PlayerMotionAnimationBinding binding in sprint.MotionBindings) yield return binding;
        }
        if (other?.MotionBindings != null)
        {
            foreach (PlayerMotionAnimationBinding binding in other.MotionBindings) yield return binding;
        }
    }

    private bool ValidateBindingGroup(string category, List<PlayerMotionAnimationBinding> bindings, HashSet<PlayerMotionDefinition> seenDefinitions, ICollection<string> errors)
    {
        if (bindings == null)
        {
            errors?.Add(name + ": " + category + ".MotionBindings 缺失。");
            return false;
        }
        bool valid = true;
        for (int i = 0; i < bindings.Count; i++)
        {
            PlayerMotionAnimationBinding binding = bindings[i];
            string label = category + ".MotionBindings[" + i + "]";
            if (binding == null)
            {
                errors?.Add(name + ": " + label + " 缺少 Binding。");
                valid = false;
                continue;
            }
            valid &= binding.Validate(label, errors);
            if (binding.Definition != null && !seenDefinitions.Add(binding.Definition))
            {
                errors?.Add(name + ": " + label + " 的 Definition " + binding.Definition.name + " 在分类之间重复。");
                valid = false;
            }
        }
        return valid;
    }

    private int CountBindings(PlayerMotionDefinition definition)
    {
        int count = 0;
        foreach (PlayerMotionAnimationBinding binding in MotionBindings)
        {
            if (binding != null && binding.Definition == definition) count++;
        }
        return count;
    }

    private static bool ValidateTransition(ClipTransition transition, string label, ICollection<string> errors)
    {
        if (transition != null && transition.Clip != null) return true;
        errors?.Add(label + ": 缺少 Clip。");
        return false;
    }

    private static bool ValidateLoop(PlayerLoopAnimationPair loop, string label, ICollection<string> errors)
    {
        return loop != null && loop.Validate(label, errors);
    }

    internal static bool ValidateProfilePlantMarkers(PlayerMotionProfile profile, string label, ICollection<string> errors)
    {
        if (profile == null)
        {
            errors?.Add(label + ": 缺少 MotionProfile。");
            return false;
        }
        if (profile.HasPlantMarkers) return true;
        errors?.Add(label + ": MotionProfile 缺少人工 Plant Marker。");
        return false;
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
    public void Configure(PlayerMotionCatalog catalog)
    {
        motionCatalog = catalog;
    }

    public void ConfigureLoop(PlayerLocomotionMode locomotionMode, PlayerFoot foot, PlayerMotionProfile profile, AnimationClip clip, float fadeDuration)
    {
        PlayerLocomotionAnimationGroup group = locomotionMode == PlayerLocomotionMode.Walk ? walk : locomotionMode == PlayerLocomotionMode.Run ? run : sprint;
        group ??= new PlayerLocomotionAnimationGroup();
        if (locomotionMode == PlayerLocomotionMode.Walk) walk = group;
        else if (locomotionMode == PlayerLocomotionMode.Run) run = group;
        else sprint = group;
        group.Loop.Configure(foot, profile, clip, fadeDuration);
    }
#endif
}
