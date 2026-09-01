using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerFoot
{
    Unknown,
    Left,
    Right
}

public enum PlayerFootPlantDetectionMode
{
    Loop,
    Start,
    Stop,
    Turn
}

public enum PlayerPlantMarkerMode
{
    ManualOverride = 0,
    Auto = 1
}

[Serializable]
public struct PlayerFootPlantMarker
{
    [SerializeField] private PlayerFoot foot;
    [SerializeField, Range(0f, 1f)] private float normalizedTime;
    [SerializeField, Range(0f, 1f)] private float confidence;

    public PlayerFootPlantMarker(PlayerFoot foot, float normalizedTime, float confidence)
    {
        this.foot = foot;
        this.normalizedTime = normalizedTime;
        this.confidence = confidence;
    }

    public PlayerFoot Foot => foot;
    public float NormalizedTime => normalizedTime;
    public float Confidence => confidence;
}

[Serializable]
public class PlayerFootMotionBakeData
{
    public float[] SoleHeight;
    public float[] VerticalSpeed;
    public float[] HorizontalSpeed;
}

[Serializable]
public class PlayerFootMotionChannel
{
    [SerializeField] private float[] soleHeight = Array.Empty<float>();
    [SerializeField] private float[] verticalSpeed = Array.Empty<float>();
    [SerializeField] private float[] horizontalSpeed = Array.Empty<float>();

    public int SampleCount => soleHeight?.Length ?? 0;
    public bool HasData => SampleCount >= 2 && verticalSpeed != null && horizontalSpeed != null && verticalSpeed.Length == SampleCount && horizontalSpeed.Length == SampleCount;
    public IReadOnlyList<float> SoleHeight => soleHeight;
    public IReadOnlyList<float> VerticalSpeed => verticalSpeed;
    public IReadOnlyList<float> HorizontalSpeed => horizontalSpeed;

    public bool Validate(string label, int expectedSampleCount, ICollection<string> errors)
    {
        bool valid = true;
        if (!HasData)
        {
            errors?.Add(label + ": 脚底采样数据缺失或长度不一致。");
            return false;
        }
        if (expectedSampleCount > 0 && SampleCount != expectedSampleCount)
        {
            errors?.Add(label + ": 脚底采样数量与 Motion 数据不一致。");
            valid = false;
        }
        for (int index = 0; index < SampleCount; index++)
        {
            if (!IsFinite(soleHeight[index]) || !IsFinite(verticalSpeed[index]) || !IsFinite(horizontalSpeed[index]))
            {
                errors?.Add(label + ": 脚底采样包含无效数值。");
                valid = false;
                break;
            }
        }
        return valid;
    }

#if UNITY_EDITOR
    public void SetBakedData(PlayerFootMotionBakeData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        soleHeight = data.SoleHeight ?? Array.Empty<float>();
        verticalSpeed = data.VerticalSpeed ?? Array.Empty<float>();
        horizontalSpeed = data.HorizontalSpeed ?? Array.Empty<float>();
    }
#endif

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
