using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Bake后的数据证明，用于数据校验
/// </summary>
[Serializable]
public class PlayerMotionProfileMetadata
{
    //bake算法版本控制，用于数据校验
    [SerializeField] private int bakeVersion;
    [SerializeField] private int sampleRate;
    [SerializeField] private string sourceClipGuid;
    [SerializeField] private long sourceClipLocalId;
    [SerializeField] private string modelGuid;
    [SerializeField] private string sourceDependencyHash;
    [SerializeField] private string footCalibrationGuid;
    [SerializeField] private string footCalibrationHash;

    public int BakeVersion => bakeVersion;
    public int SampleRate => sampleRate;
    public string SourceClipGuid => sourceClipGuid;
    public long SourceClipLocalId => sourceClipLocalId;
    public string ModelGuid => modelGuid;
    public string SourceDependencyHash => sourceDependencyHash;
    public string FootCalibrationGuid => footCalibrationGuid;
    public string FootCalibrationHash => footCalibrationHash;

#if UNITY_EDITOR
    //保存设定数据
    public void Set(int version, int bakedSampleRate, string clipGuid, long clipLocalId, string bakedModelGuid, string dependencyHash, string calibrationGuid, string calibrationHash)
    {
        bakeVersion = version;
        sampleRate = bakedSampleRate;
        sourceClipGuid = clipGuid;
        sourceClipLocalId = clipLocalId;
        modelGuid = bakedModelGuid;
        sourceDependencyHash = dependencyHash;
        footCalibrationGuid = calibrationGuid;
        footCalibrationHash = calibrationHash;
    }
#endif
}
/// <summary>
/// 被烘焙后的动画数据文件
/// </summary>
[CreateAssetMenu(fileName = "PlayerMotionProfile", menuName = "Player/Motion/Profile")]
public class PlayerMotionProfile : ScriptableObject
{
    public const int CurrentBakeVersion = 2;
    //动画持续时间
    [Min(0f)] [SerializeField] private float duration;
    [Min(1)] [SerializeField] private int sampleRate = 60;
    [SerializeField] private Vector2[] cumulativePlanarPosition = Array.Empty<Vector2>();
    [SerializeField] private float[] cumulativeTravelDistance = Array.Empty<float>();
    [SerializeField] private float[] cumulativeYaw = Array.Empty<float>();
    [SerializeField] private PlayerFootMotionChannel leftFoot = new PlayerFootMotionChannel();
    [SerializeField] private PlayerFootMotionChannel rightFoot = new PlayerFootMotionChannel();
    //保存元数据
    [SerializeField] private PlayerMotionProfileMetadata editorMetadata = new PlayerMotionProfileMetadata();

    public float Duration => duration;
    public int SampleRate => sampleRate;
    public int SampleCount => cumulativePlanarPosition?.Length ?? 0;
    public bool HasPlanarPosition => SampleCount >= 2;
    public bool HasTravelDistance => cumulativeTravelDistance != null && cumulativeTravelDistance.Length == SampleCount;
    public bool HasYaw => cumulativeYaw != null && cumulativeYaw.Length == SampleCount;
    public PlayerFootMotionChannel LeftFoot => leftFoot;
    public PlayerFootMotionChannel RightFoot => rightFoot;
    public bool HasFootData => leftFoot != null && rightFoot != null && leftFoot.HasData && rightFoot.HasData;
    public PlayerMotionProfileMetadata EditorMetadata => editorMetadata;
    /// <summary>
    /// 查询此刻的移动数据
    /// </summary>
    public Vector3 EvaluatePlanarPosition(float progress)
    {
        Vector2 value = Evaluate(cumulativePlanarPosition, progress);
        return new Vector3(value.x, 0f, value.y);
    }

    public float EvaluateTravelDistance(float progress) => Evaluate(cumulativeTravelDistance, progress);
    public float EvaluateYaw(float progress) => Evaluate(cumulativeYaw, progress);

    public PlayerFootContact EvaluateFootContact(float progress)
    {
        PlayerFootContactMarker leftMarker = leftFoot == null ? default : leftFoot.EvaluateMarker(progress);
        PlayerFootContactMarker rightMarker = rightFoot == null ? default : rightFoot.EvaluateMarker(progress);
        return new PlayerFootContact(leftMarker.Contact, rightMarker.Contact, leftMarker.Plant, rightMarker.Plant, leftMarker.Lift, rightMarker.Lift);
    }

    public PlayerFoot ResolveSupportFoot(float progress, PlayerFoot fallback)
    {
        PlayerFootContact contact = EvaluateFootContact(progress);
        if (contact.HasSingleContact) return contact.SingleContactFoot;
        return fallback == PlayerFoot.Unknown ? PlayerFoot.Right : fallback;
    }

    public PlayerFootContact GetFootPhase(float normalizedTime) => EvaluateFootContact(normalizedTime);

    public PlayerFoot EntrySupportFoot => ResolveEntrySupportFoot();
    public PlayerFoot ExitSupportFoot => ResolveExitSupportFoot();

    public float GetSupportPhase(PlayerFoot foot)
    {
        if (foot == PlayerFoot.Left) return leftFoot == null ? 0f : leftFoot.GetSupportPhase();
        if (foot == PlayerFoot.Right) return rightFoot == null ? 0f : rightFoot.GetSupportPhase();
        return 0f;
    }

    public bool Validate(ICollection<string> errors)
    {
        bool valid = true;
        if (!IsFinite(duration) || duration <= 0f) { errors?.Add(name + ": Duration 必须是大于 0 的有限值。"); valid = false; }
        if (sampleRate <= 0 || SampleCount < 2) { errors?.Add(name + ": SampleRate / SampleCount 无效。"); valid = false; }
        if (!HasTravelDistance || !HasYaw) { errors?.Add(name + ": Motion channel 的采样数量不一致。"); valid = false; }
        if (leftFoot != null && rightFoot != null && (leftFoot.HasData || rightFoot.HasData))
        {
            valid &= leftFoot.Validate(name + ".LeftFoot", SampleCount, errors);
            valid &= rightFoot.Validate(name + ".RightFoot", SampleCount, errors);
        }
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
        SetBakedData(bakedDuration, bakedSampleRate, planarPosition, travelDistance, yaw, clipGuid, clipLocalId, modelGuid, dependencyHash, null, null, null, null);
    }

    public void SetBakedData(float bakedDuration, int bakedSampleRate, Vector2[] planarPosition, float[] travelDistance, float[] yaw, string clipGuid, long clipLocalId, string modelGuid, string dependencyHash, PlayerFootMotionBakeData leftFootData, PlayerFootMotionBakeData rightFootData, string calibrationGuid, string calibrationHash)
    {
        duration = bakedDuration;
        sampleRate = bakedSampleRate;
        cumulativePlanarPosition = planarPosition ?? Array.Empty<Vector2>();
        cumulativeTravelDistance = travelDistance ?? Array.Empty<float>();
        cumulativeYaw = yaw ?? Array.Empty<float>();
        leftFoot ??= new PlayerFootMotionChannel();
        rightFoot ??= new PlayerFootMotionChannel();
        if (leftFootData != null) leftFoot.SetBakedData(leftFootData);
        if (rightFootData != null) rightFoot.SetBakedData(rightFootData);
        editorMetadata ??= new PlayerMotionProfileMetadata();
        editorMetadata.Set(CurrentBakeVersion, bakedSampleRate, clipGuid, clipLocalId, modelGuid, dependencyHash, calibrationGuid, calibrationHash);
    }

    public void CopyAutoFootMarkersToManual(PlayerFoot foot)
    {
        ResolveChannel(foot)?.CopyAutoToManual();
    }

    public void RestoreAutomaticFootMarkers(PlayerFoot foot)
    {
        ResolveChannel(foot)?.RestoreAutomatic();
    }
#endif
    /// <summary>
    /// 拿到实际的移动位置
    /// </summary>
    private static Vector2 Evaluate(Vector2[] samples, float progress)
    {
        if (samples == null || samples.Length == 0) return Vector2.zero;
        if (samples.Length == 1) return samples[0];
        //动画播放进程归一化后和采样点相乘得到更加细致的播放比例
        float sample = Mathf.Clamp01(progress) * (samples.Length - 1);
        //拿到左右两个下标
        int index = Mathf.Min(Mathf.FloorToInt(sample), samples.Length - 2);
        //表示在左右两个中间的第0.x的位置
        return Vector2.LerpUnclamped(samples[index], samples[index + 1], sample - index);
    }
    /// <summary>
    /// 拿到移动距离和角度
    /// </summary>
    private static float Evaluate(float[] samples, float progress)
    {
        if (samples == null || samples.Length == 0) return 0f;
        if (samples.Length == 1) return samples[0];
        float sample = Mathf.Clamp01(progress) * (samples.Length - 1);
        int index = Mathf.Min(Mathf.FloorToInt(sample), samples.Length - 2);
        return Mathf.LerpUnclamped(samples[index], samples[index + 1], sample - index);
    }
    /// <summary>
    /// 验证某个数字是否是有效数据
    /// </summary>
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private PlayerFootMotionChannel ResolveChannel(PlayerFoot foot) => foot == PlayerFoot.Left ? leftFoot : foot == PlayerFoot.Right ? rightFoot : null;

    private PlayerFoot ResolveEntrySupportFoot()
    {
        if (!HasFootData) return PlayerFoot.Unknown;
        PlayerFootContact entry = EvaluateFootContact(0f);
        if (entry.HasSingleContact) return entry.SingleContactFoot;
        if (!entry.HasAnyContact) return PlayerFoot.Unknown;
        int leftLift = FindFirstMarkerIndex(leftFoot, true, false);
        int rightLift = FindFirstMarkerIndex(rightFoot, true, false);
        if (leftLift == rightLift) return PlayerFoot.Right;
        return leftLift > rightLift ? PlayerFoot.Left : PlayerFoot.Right;
    }

    private PlayerFoot ResolveExitSupportFoot()
    {
        if (!HasFootData) return PlayerFoot.Unknown;
        PlayerFootContact exit = EvaluateFootContact(1f);
        if (exit.HasSingleContact) return exit.SingleContactFoot;
        if (!exit.HasAnyContact) return PlayerFoot.Unknown;
        int leftPlant = FindLastMarkerIndex(leftFoot, false, true);
        int rightPlant = FindLastMarkerIndex(rightFoot, false, true);
        if (leftPlant == rightPlant) return PlayerFoot.Right;
        return leftPlant > rightPlant ? PlayerFoot.Left : PlayerFoot.Right;
    }

    private static int FindFirstMarkerIndex(PlayerFootMotionChannel channel, bool lift, bool plant)
    {
        if (channel == null || !channel.HasData) return -1;
        for (int index = 0; index < channel.SampleCount; index++)
        {
            PlayerFootContactMarker marker = channel.GetMarker(index);
            if (lift && marker.Lift || plant && marker.Plant) return index;
        }
        return channel.SampleCount;
    }

    private static int FindLastMarkerIndex(PlayerFootMotionChannel channel, bool lift, bool plant)
    {
        if (channel == null || !channel.HasData) return -1;
        for (int index = channel.SampleCount - 1; index >= 0; index--)
        {
            PlayerFootContactMarker marker = channel.GetMarker(index);
            if (lift && marker.Lift || plant && marker.Plant) return index;
        }
        return -1;
    }
}
