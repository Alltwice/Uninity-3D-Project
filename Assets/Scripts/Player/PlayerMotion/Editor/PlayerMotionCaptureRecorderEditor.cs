using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerMotionCaptureRecorder))]
public sealed class PlayerMotionCaptureRecorderEditor : Editor
{
    private const string RunStartProfilePath = "Assets/Settings/Player/RunStartMotionProfile.asset";
    private const string RunStopProfilePath = "Assets/Settings/Player/RunStopMotionProfile.asset";

    private PlayerMotionCaptureRecorder Recorder => (PlayerMotionCaptureRecorder)target;

    private void OnEnable()
    {
        EditorApplication.update += ProcessCompletedCapture;
    }

    private void OnDisable()
    {
        EditorApplication.update -= ProcessCompletedCapture;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Capture State", $"Armed: {Recorder.ArmedKind} / Capturing: {Recorder.CapturingKind}");
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode before arming a capture.", MessageType.Info);
        }
        else if (Recorder.Config.Mode != RunTransitionMotionMode.RuntimeRootMotion)
        {
            EditorGUILayout.HelpBox("Capture requires RuntimeRootMotion mode.", MessageType.Warning);
        }
        using (new EditorGUI.DisabledScope(!Application.isPlaying || Recorder.Config.Mode != RunTransitionMotionMode.RuntimeRootMotion))
        {
            if (GUILayout.Button("Arm RunStart Capture")) Recorder.ArmRunStartCapture();
            if (GUILayout.Button("Arm RunStop Capture")) Recorder.ArmRunStopCapture();
        }
        if (GUILayout.Button("Cancel Capture")) Recorder.CancelCapture();
    }

    private void ProcessCompletedCapture()
    {
        if (Recorder == null || Recorder.CompletedCapture == null) return;
        PlayerMotionCaptureData data = Recorder.CompletedCapture;
        if (data.Duration <= 0f || data.Samples.Count < 2)
        {
            Recorder.ConsumeCompletedCapture();
            return;
        }
        string assetPath = data.Kind == PlayerMotionCaptureKind.RunStart ? RunStartProfilePath : RunStopProfilePath;
        PlayerMotionProfile profile = AssetDatabase.LoadAssetAtPath<PlayerMotionProfile>(assetPath);
        if (profile == null)
        {
            profile = CreateInstance<PlayerMotionProfile>();
            AssetDatabase.CreateAsset(profile, assetPath);
        }
        AnimationCurve localX = BuildLinearCurve(data, sample => sample.LocalX);
        AnimationCurve localZ = BuildLinearCurve(data, sample => sample.LocalZ);
        AnimationCurve travelDistance = BuildLinearCurve(data, sample => sample.TravelDistance);
        SerializedObject profileObject = new SerializedObject(profile);
        profileObject.FindProperty("duration").floatValue = data.Duration;
        profileObject.FindProperty("cumulativeLocalX").animationCurveValue = localX;
        profileObject.FindProperty("cumulativeLocalZ").animationCurveValue = localZ;
        profileObject.FindProperty("cumulativeTravelDistance").animationCurveValue = travelDistance;
        profileObject.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject configObject = new SerializedObject(Recorder.Config);
        string profilePropertyName = data.Kind == PlayerMotionCaptureKind.RunStart ? "runStartProfile" : "runStopProfile";
        configObject.FindProperty(profilePropertyName).objectReferenceValue = profile;
        configObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(Recorder.Config);
        AssetDatabase.SaveAssets();
        Recorder.ConsumeCompletedCapture();
        Repaint();
        Debug.Log($"Saved {data.Kind} motion capture to {assetPath} ({data.Samples.Count} samples, {data.Duration:F3}s).", profile);
    }

    private static AnimationCurve BuildLinearCurve(PlayerMotionCaptureData data, System.Func<PlayerMotionCaptureSample, float> selector)
    {
        List<Keyframe> keys = new List<Keyframe>(data.Samples.Count);
        for (int i = 0; i < data.Samples.Count; i++)
        {
            PlayerMotionCaptureSample sample = data.Samples[i];
            keys.Add(new Keyframe(Mathf.Clamp01(sample.Time / data.Duration), selector(sample)));
        }
        AnimationCurve curve = new AnimationCurve(keys.ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }
}
