using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerFootCalibration", menuName = "Player/Motion/Foot Calibration")]
public class PlayerFootCalibration : ScriptableObject
{
    [SerializeField] private GameObject modelAsset;
    [SerializeField] private Vector3 leftFootSoleOffset;
    [SerializeField] private Vector3 rightFootSoleOffset;
    [SerializeField] private float virtualGroundHeight;
    [Min(0f)] [SerializeField] private float contactHeightThreshold = 0.04f;
    [Min(0f)] [SerializeField] private float releaseHeightThreshold = 0.07f;
    [Min(0f)] [SerializeField] private float verticalSpeedThreshold = 0.2f;
    [Min(0f)] [SerializeField] private float horizontalSpeedThreshold = 0.25f;
    [Min(0f)] [SerializeField] private float stableTimeThreshold = 0.05f;

    public GameObject ModelAsset => modelAsset;
    public Vector3 LeftFootSoleOffset => leftFootSoleOffset;
    public Vector3 RightFootSoleOffset => rightFootSoleOffset;
    public float VirtualGroundHeight => virtualGroundHeight;
    public float ContactHeightThreshold => contactHeightThreshold;
    public float ReleaseHeightThreshold => releaseHeightThreshold;
    public float VerticalSpeedThreshold => verticalSpeedThreshold;
    public float HorizontalSpeedThreshold => horizontalSpeedThreshold;
    public float StableTimeThreshold => stableTimeThreshold;
    public string SettingsHash => Hash128.Compute(string.Join("|", modelAsset == null ? string.Empty : modelAsset.name, leftFootSoleOffset.x, leftFootSoleOffset.y, leftFootSoleOffset.z, rightFootSoleOffset.x, rightFootSoleOffset.y, rightFootSoleOffset.z, virtualGroundHeight, contactHeightThreshold, releaseHeightThreshold, verticalSpeedThreshold, horizontalSpeedThreshold, stableTimeThreshold)).ToString();

    public bool Validate(GameObject expectedModel, ICollection<string> errors)
    {
        bool valid = true;
        if (modelAsset == null)
        {
            errors?.Add(name + ": 缺少校准模型引用。");
            valid = false;
        }
        else if (expectedModel != null && modelAsset != expectedModel)
        {
            errors?.Add(name + ": 校准模型与当前 Bake 模型不一致。");
            valid = false;
        }
        if (!IsFinite(virtualGroundHeight) || !IsFinite(contactHeightThreshold) || !IsFinite(releaseHeightThreshold) || !IsFinite(verticalSpeedThreshold) || !IsFinite(horizontalSpeedThreshold) || !IsFinite(stableTimeThreshold))
        {
            errors?.Add(name + ": 校准参数包含 NaN 或 Infinity。");
            valid = false;
        }
        if (releaseHeightThreshold <= contactHeightThreshold)
        {
            errors?.Add(name + ": ReleaseHeightThreshold 必须高于 ContactHeightThreshold。");
            valid = false;
        }
        if (stableTimeThreshold <= 0f || verticalSpeedThreshold <= 0f || horizontalSpeedThreshold <= 0f)
        {
            errors?.Add(name + ": 脚步检测速度和稳定时间阈值必须大于 0。");
            valid = false;
        }
        return valid;
    }

#if UNITY_EDITOR
    public void Configure(GameObject model, Vector3 leftOffset, Vector3 rightOffset, float groundHeight, float contactHeight, float releaseHeight, float verticalSpeed, float horizontalSpeed, float stableTime)
    {
        modelAsset = model;
        leftFootSoleOffset = leftOffset;
        rightFootSoleOffset = rightOffset;
        virtualGroundHeight = groundHeight;
        contactHeightThreshold = contactHeight;
        releaseHeightThreshold = releaseHeight;
        verticalSpeedThreshold = verticalSpeed;
        horizontalSpeedThreshold = horizontalSpeed;
        stableTimeThreshold = stableTime;
    }
#endif

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
