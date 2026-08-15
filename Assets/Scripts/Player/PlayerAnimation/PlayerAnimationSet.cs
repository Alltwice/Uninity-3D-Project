using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

[Serializable]
public sealed class PlayerMotionAnimationBinding
{
    [SerializeField] private PlayerMotionDefinition definition;
    [SerializeField] private ClipTransition transition = new ClipTransition();
    [SerializeField] private AnimationCurve poseFadeWeight = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public PlayerMotionDefinition Definition => definition;
    public ClipTransition Transition => transition;
    public float EvaluatePoseFade(float handoffProgress) => Mathf.Clamp01(poseFadeWeight == null ? handoffProgress : poseFadeWeight.Evaluate(handoffProgress));

#if UNITY_EDITOR
    public void Configure(PlayerMotionDefinition motionDefinition, AnimationClip clip, float fadeDuration)
    {
        definition = motionDefinition;
        transition ??= new ClipTransition();
        transition.Clip = clip;
        transition.FadeDuration = fadeDuration;
        transition.Speed = 1f;
        poseFadeWeight = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }
#endif
}

[CreateAssetMenu(fileName = "PlayerAnimationSet", menuName = "Player/Animation Set")]
public sealed class PlayerAnimationSet : ScriptableObject
{
    [SerializeField] private PlayerMotionCatalog motionCatalog;
    [SerializeField] private List<PlayerMotionAnimationBinding> motionBindings = new List<PlayerMotionAnimationBinding>();

    public PlayerMotionCatalog MotionCatalog => motionCatalog;
    public IReadOnlyList<PlayerMotionAnimationBinding> MotionBindings => motionBindings;

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

    public bool Validate(ICollection<string> errors)
    {
        bool valid = true;
        HashSet<PlayerMotionDefinition> seen = new HashSet<PlayerMotionDefinition>();
        for (int i = 0; i < motionBindings.Count; i++)
        {
            PlayerMotionAnimationBinding binding = motionBindings[i];
            if (binding == null || binding.Definition == null || binding.Transition == null || binding.Transition.Clip == null) { errors?.Add(name + ": Binding " + i + " 缺少 Definition 或 Clip。"); valid = false; continue; }
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
        return valid;
    }

#if UNITY_EDITOR
    public void Configure(PlayerMotionCatalog catalog, IEnumerable<PlayerMotionAnimationBinding> bindings)
    {
        motionCatalog = catalog;
        motionBindings.Clear();
        motionBindings.AddRange(bindings);
    }
#endif
}
