using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerFoot
{
    Unknown,
    Left,
    Right
}

[Serializable]
public struct PlayerFootContactMarker
{
    [SerializeField] private bool contact;
    [SerializeField] private bool plant;
    [SerializeField] private bool lift;

    public PlayerFootContactMarker(bool isContact, bool isPlant, bool isLift)
    {
        contact = isContact;
        plant = isPlant;
        lift = isLift;
    }

    public bool Contact => contact;
    public bool Plant => plant;
    public bool Lift => lift;
}

[Serializable]
public struct PlayerFootContact
{
    public PlayerFootContact(bool leftContact, bool rightContact, bool leftPlant, bool rightPlant, bool leftLift, bool rightLift)
    {
        LeftContact = leftContact;
        RightContact = rightContact;
        LeftPlant = leftPlant;
        RightPlant = rightPlant;
        LeftLift = leftLift;
        RightLift = rightLift;
    }

    public bool LeftContact { get; }
    public bool RightContact { get; }
    public bool LeftPlant { get; }
    public bool RightPlant { get; }
    public bool LeftLift { get; }
    public bool RightLift { get; }
    public bool HasSingleContact => LeftContact ^ RightContact;
    public bool HasAnyContact => LeftContact || RightContact;
    public PlayerFoot SingleContactFoot => LeftContact == RightContact ? PlayerFoot.Unknown : LeftContact ? PlayerFoot.Left : PlayerFoot.Right;
}

[Serializable]
public class PlayerFootMotionBakeData
{
    public float[] SoleHeight;
    public float[] VerticalSpeed;
    public float[] HorizontalSpeed;
    public float[] StableTime;
    public PlayerFootContactMarker[] AutoMarkers;
}

[Serializable]
public class PlayerFootMotionChannel
{
    [SerializeField] private float[] soleHeight = Array.Empty<float>();
    [SerializeField] private float[] verticalSpeed = Array.Empty<float>();
    [SerializeField] private float[] horizontalSpeed = Array.Empty<float>();
    [SerializeField] private float[] stableTime = Array.Empty<float>();
    [SerializeField] private PlayerFootContactMarker[] autoMarkers = Array.Empty<PlayerFootContactMarker>();
    [SerializeField] private PlayerFootContactMarker[] manualMarkers = Array.Empty<PlayerFootContactMarker>();
    [SerializeField] private bool useManualOverride;

    public int SampleCount => soleHeight?.Length ?? 0;
    public bool HasData => SampleCount >= 2 && verticalSpeed != null && horizontalSpeed != null && stableTime != null && autoMarkers != null && verticalSpeed.Length == SampleCount && horizontalSpeed.Length == SampleCount && stableTime.Length == SampleCount && autoMarkers.Length == SampleCount;
    public bool UseManualOverride => useManualOverride;
    public IReadOnlyList<float> SoleHeight => soleHeight;
    public IReadOnlyList<float> VerticalSpeed => verticalSpeed;
    public IReadOnlyList<float> HorizontalSpeed => horizontalSpeed;
    public IReadOnlyList<float> StableTime => stableTime;
    public IReadOnlyList<PlayerFootContactMarker> AutoMarkers => autoMarkers;
    public IReadOnlyList<PlayerFootContactMarker> ManualMarkers => manualMarkers;

    public PlayerFootContactMarker EvaluateMarker(float normalizedTime)
    {
        if (!HasData) return default;
        int index = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalizedTime) * (SampleCount - 1)), 0, SampleCount - 1);
        return GetMarker(index);
    }

    public PlayerFootContactMarker GetMarker(int index)
    {
        if (!HasData) return default;
        index = Mathf.Clamp(index, 0, SampleCount - 1);
        return useManualOverride && manualMarkers != null && manualMarkers.Length == SampleCount ? manualMarkers[index] : autoMarkers[index];
    }

    public float GetSupportPhase()
    {
        if (!HasData) return 0f;
        for (int index = 0; index < SampleCount; index++)
        {
            if (!GetMarker(index).Contact) continue;
            return index / (float)(SampleCount - 1);
        }
        return 0f;
    }

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
        if (useManualOverride && (manualMarkers == null || manualMarkers.Length != SampleCount))
        {
            errors?.Add(label + ": 人工脚步标记数量与采样数量不一致。");
            valid = false;
        }
        for (int index = 0; index < SampleCount; index++)
        {
            if (!IsFinite(soleHeight[index]) || !IsFinite(verticalSpeed[index]) || !IsFinite(horizontalSpeed[index]) || !IsFinite(stableTime[index]) || stableTime[index] < 0f)
            {
                errors?.Add(label + ": 脚底采样包含无效数值。");
                valid = false;
                break;
            }
            PlayerFootContactMarker marker = autoMarkers[index];
            if (marker.Plant && marker.Lift || marker.Plant && !marker.Contact || marker.Lift && marker.Contact)
            {
                errors?.Add(label + ": 自动 Plant/Lift 标记无效。");
                valid = false;
                break;
            }
            if (useManualOverride)
            {
                PlayerFootContactMarker manual = manualMarkers[index];
                if (manual.Plant && manual.Lift || manual.Plant && !manual.Contact || manual.Lift && manual.Contact)
                {
                    errors?.Add(label + ": 人工 Plant/Lift 标记无效。");
                    valid = false;
                    break;
                }
            }
        }
        valid &= ValidateMarkerSequence(autoMarkers, label + " 自动", errors);
        if (useManualOverride) valid &= ValidateMarkerSequence(manualMarkers, label + " 人工", errors);
        return valid;
    }

#if UNITY_EDITOR
    public void SetBakedData(PlayerFootMotionBakeData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        soleHeight = data.SoleHeight ?? Array.Empty<float>();
        verticalSpeed = data.VerticalSpeed ?? Array.Empty<float>();
        horizontalSpeed = data.HorizontalSpeed ?? Array.Empty<float>();
        stableTime = data.StableTime ?? Array.Empty<float>();
        autoMarkers = data.AutoMarkers ?? Array.Empty<PlayerFootContactMarker>();
        manualMarkers = Array.Empty<PlayerFootContactMarker>();
        useManualOverride = false;
    }

    public void CopyAutoToManual()
    {
        manualMarkers = autoMarkers == null ? Array.Empty<PlayerFootContactMarker>() : (PlayerFootContactMarker[])autoMarkers.Clone();
        useManualOverride = true;
    }

    public void RestoreAutomatic()
    {
        useManualOverride = false;
    }
#endif

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool ValidateMarkerSequence(PlayerFootContactMarker[] markers, string label, ICollection<string> errors)
    {
        bool valid = true;
        bool contact = false;
        for (int index = 0; index < markers.Length; index++)
        {
            PlayerFootContactMarker marker = markers[index];
            if (marker.Plant)
            {
                if (contact) { errors?.Add(label + " Plant 顺序无效：尚未 Lift。"); valid = false; }
                contact = true;
            }
            if (marker.Lift)
            {
                if (!contact) { errors?.Add(label + " Lift 顺序无效：没有对应 Plant。"); valid = false; }
                contact = false;
            }
            if (marker.Contact != contact && !(index == 0 && marker.Contact))
            {
                errors?.Add(label + " Contact 状态与 Plant/Lift 顺序不一致。");
                valid = false;
            }
        }
        return valid;
    }
}
