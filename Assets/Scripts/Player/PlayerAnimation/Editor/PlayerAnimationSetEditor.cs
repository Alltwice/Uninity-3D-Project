using System.Collections.Generic;
using ProjectTools.AnimationPreview;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerAnimationSet))]
public sealed class PlayerAnimationSetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!GUILayout.Button("Validate Motion Bindings")) return;
        PlayerAnimationSet animationSet = (PlayerAnimationSet)target;
        List<string> errors = new List<string>();
        bool valid = animationSet.Validate(errors);
        for (int i = 0; i < animationSet.MotionBindings.Count; i++)
        {
            PlayerMotionAnimationBinding binding = animationSet.MotionBindings[i];
            if (binding?.Definition?.Profile == null) continue;
            valid &= PlayerMotionBaker.Validate(binding.Definition.Profile, errors);
            AnimationClip sourceClip = PlayerMotionBaker.ResolveSourceClip(binding.Definition.Profile);
            if (sourceClip == binding.Transition.Clip) continue;
            errors.Add(binding.Definition.name + ": Animation Binding Clip 与 Profile Source Clip 不一致。");
            valid = false;
        }
        if (valid) Debug.Log(animationSet.name + ": Motion bindings and baked sources valid.", animationSet);
        else Debug.LogError(string.Join("\n", errors), animationSet);
    }
}
