using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PlayerMotionProfileMetadata
{
    [SerializeField] private int bakeVersion;
    [SerializeField] private int sampleRate;
    [SerializeField] private string sourceClipGuid;
    [SerializeField] private long sourceClipLocalId;
    [SerializeField] private string modelGuid;
    [SerializeField] private string sourceDependencyHash;

    public int BakeVersion => bakeVersion;
    public int SampleRate => sampleRate;
    public string SourceClipGuid => sourceClipGuid;
    public long SourceClipLocalId => sourceClipLocalId;
    public string ModelGuid => modelGuid;
    public string SourceDependencyHash => sourceDependencyHash;

#if UNITY_EDITOR
    public void Set(int version, int bakedSampleRate, string clipGuid, long clipLocalId, string bakedModelGuid, string dependencyHash)
    {
        bakeVersion = version;
        sampleRate = bakedSampleRate;
        sourceClipGuid = clipGuid;
        sourceClipLocalId = clipLocalId;
        modelGuid = bakedModelGuid;
        sourceDependencyHash = dependencyHash;
    }
#endif
}

[CreateAssetMenu(fileName = "PlayerMotionProfile", menuName = "Player/Motion/Profile")]
public sealed class PlayerMotionProfile : ScriptableObject
{
    public const int CurrentBakeVersion = 1;

    [Min(0f)] [SerializeField] private float duration;
    [Min(1)] [SerializeField] private int sampleRate = 60;
    [SerializeField] private Vector2[] cumulativePlanarPosition = Array.Empty<Vector2>();
    [SerializeField] private float[] cumulativeTravelDistance = Array.Empty<float>();
    [SerializeField] private float[] cumulativeYaw = Array.Empty<float>();
    [SerializeField] private PlayerMotionProfileMetadata editorMetadata = new PlayerMotionProfileMetadata();

    public float Duration => duration;
    public int SampleRate => sampleRate;
    public int SampleCount => cumulativePlanarPosition?.Length ?? 0;
    public bool HasPlanarPosition => SampleCount >= 2;
    public bool HasTravelDistance => cumulativeTravelDistance != null && cumulativeTravelDistance.Length == SampleCount;
    public bool HasYaw => cumulativeYaw != null && cumulativeYaw.Length == SampleCount;
    public PlayerMotionProfileMetadata EditorMetadata => editorMetadata;

    public Vector3 EvaluatePlanarPosition(float progress)
    {
        Vector2 value = Evaluate(cumulativePlanarPosition, progress);
        return new Vector3(value.x, 0f, value.y);
    }

    public float EvaluateTravelDistance(float progress) => Evaluate(cumulativeTravelDistance, progress);
    public float EvaluateYaw(float progress) => Evaluate(cumulativeYaw, progress);

    public bool Validate(ICollection<string> errors)
    {
        bool valid = true;
        if (!IsFinite(duration) || duration <= 0f) { errors?.Add(name + ": Duration 必须是大于 0 的有限值。"); valid = false; }
        if (sampleRate <= 0 || SampleCount < 2) { errors?.Add(name + ": SampleRate / SampleCount 无效。"); valid = false; }
        if (!HasTravelDistance || !HasYaw) { errors?.Add(name + ": Motion channel 的采样数量不一致。"); valid = false; }
        float previousDistance = float.NegativeInfinity;
        for (int i = 0; i < SampleCount; i++)
        {
            Vector2 position = cumulativePlanarPosition[i];
            float distance = cumulativeTravelDistance[i];
            float yaw = cumulativeYaw[i];
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(distance) || !IsFinite(yaw)) { errors?.Add(name + ": 采样数据包含 NaN 或 Infinity。"); valid = false; break; }
            if (distance + 0.0001f < previousDistance) { errors?.Add(name + ": CumulativeTravelDistance 出现反向下降。"); valid = false; break; }
            if (i > 0 && Mathf.Abs(yaw - cumulativeYaw[i - 1]) > 180.001f) { errors?.Add(name + ": CumulativeYaw 存在未展开的 ±180 跳变。"); valid = false; break; }
            previousDistance = distance;
        }
        return valid;
    }

#if UNITY_EDITOR
    public void SetBakedData(float bakedDuration, int bakedSampleRate, Vector2[] planarPosition, float[] travelDistance, float[] yaw, string clipGuid, long clipLocalId, string modelGuid, string dependencyHash)
    {
        duration = bakedDuration;
        sampleRate = bakedSampleRate;
        cumulativePlanarPosition = planarPosition ?? Array.Empty<Vector2>();
        cumulativeTravelDistance = travelDistance ?? Array.Empty<float>();
        cumulativeYaw = yaw ?? Array.Empty<float>();
        editorMetadata ??= new PlayerMotionProfileMetadata();
        editorMetadata.Set(CurrentBakeVersion, bakedSampleRate, clipGuid, clipLocalId, modelGuid, dependencyHash);
    }
#endif

    private static Vector2 Evaluate(Vector2[] samples, float progress)
    {
        if (samples == null || samples.Length == 0) return Vector2.zero;
        if (samples.Length == 1) return samples[0];
        float sample = Mathf.Clamp01(progress) * (samples.Length - 1);
        int index = Mathf.Min(Mathf.FloorToInt(sample), samples.Length - 2);
        return Vector2.LerpUnclamped(samples[index], samples[index + 1], sample - index);
    }

    private static float Evaluate(float[] samples, float progress)
    {
        if (samples == null || samples.Length == 0) return 0f;
        if (samples.Length == 1) return samples[0];
        float sample = Mathf.Clamp01(progress) * (samples.Length - 1);
        int index = Mathf.Min(Mathf.FloorToInt(sample), samples.Length - 2);
        return Mathf.LerpUnclamped(samples[index], samples[index + 1], sample - index);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
