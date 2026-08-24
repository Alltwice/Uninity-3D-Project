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
        HashSet<PlayerMotionProfile> validatedProfiles = new HashSet<PlayerMotionProfile>();
        for (int i = 0; i < animationSet.MotionBindings.Count; i++)
        {
            PlayerMotionAnimationBinding binding = animationSet.MotionBindings[i];
            if (binding?.Definition == null) continue;
            valid &= ValidateProfile(binding.Definition.Profile, binding.Definition.name + ".Default", errors, validatedProfiles);
            if (binding.Definition.RequiresFootProfiles)
            {
                valid &= ValidateProfile(binding.Definition.LeftFootProfile, binding.Definition.name + ".Left", errors, validatedProfiles);
                valid &= ValidateProfile(binding.Definition.RightFootProfile, binding.Definition.name + ".Right", errors, validatedProfiles);
            }
        }
        valid &= ValidateLoop(animationSet.WalkLoop, "WalkLoop", errors, validatedProfiles);
        valid &= ValidateLoop(animationSet.RunLoop, "RunLoop", errors, validatedProfiles);
        valid &= ValidateLoop(animationSet.FastRunLoop, "SprintLoop", errors, validatedProfiles);
        if (valid) Debug.Log(animationSet.name + ": Motion bindings and baked sources valid.", animationSet);
        else Debug.LogError(string.Join("\n", errors), animationSet);
    }

    private static bool ValidateLoop(PlayerLoopAnimationPair pair, string label, ICollection<string> errors, ISet<PlayerMotionProfile> validatedProfiles)
    {
        if (pair == null) return false;
        bool valid = true;
        valid &= ValidateProfile(pair.DefaultProfile, label + ".Default", errors, validatedProfiles);
        valid &= ValidateProfile(pair.LeftProfile, label + ".Left", errors, validatedProfiles);
        valid &= ValidateProfile(pair.RightProfile, label + ".Right", errors, validatedProfiles);
        return valid;
    }

    private static bool ValidateProfile(PlayerMotionProfile profile, string label, ICollection<string> errors, ISet<PlayerMotionProfile> validatedProfiles)
    {
        if (profile == null)
        {
            errors.Add(label + ": 缺少 MotionProfile。");
            return false;
        }
        return validatedProfiles.Add(profile) ? PlayerMotionBaker.Validate(profile, errors) : true;
    }
}
