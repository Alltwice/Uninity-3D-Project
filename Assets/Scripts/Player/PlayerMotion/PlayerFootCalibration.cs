using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 模型足部校准数据
/// </summary>
[CreateAssetMenu(fileName = "PlayerFootCalibration", menuName = "Player/Motion/Foot Calibration")]
public class PlayerFootCalibration : ScriptableObject
{
    [SerializeField] private GameObject modelAsset;
    //从骨骼位置到真实鞋底的偏移点
    [SerializeField] private Vector3 leftFootSoleOffset;
    [SerializeField] private Vector3 rightFootSoleOffset;
    //定义地面高度
    [SerializeField] private float virtualGroundHeight;

    public GameObject ModelAsset => modelAsset;
    public Vector3 LeftFootSoleOffset => leftFootSoleOffset;
    public Vector3 RightFootSoleOffset => rightFootSoleOffset;
    public float VirtualGroundHeight => virtualGroundHeight;
    //哈希值
    public string SettingsHash => Hash128.Compute(string.Join("|", modelAsset == null ? string.Empty : modelAsset.name, leftFootSoleOffset.x, leftFootSoleOffset.y, leftFootSoleOffset.z, rightFootSoleOffset.x, rightFootSoleOffset.y, rightFootSoleOffset.z, virtualGroundHeight)).ToString();

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
        if (!IsFinite(leftFootSoleOffset) || !IsFinite(rightFootSoleOffset) || !IsFinite(virtualGroundHeight))
        {
            errors?.Add(name + ": 校准参数包含 NaN 或 Infinity。");
            valid = false;
        }
        return valid;
    }

#if UNITY_EDITOR
    public void Configure(GameObject model, Vector3 leftOffset, Vector3 rightOffset, float groundHeight)
    {
        modelAsset = model;
        leftFootSoleOffset = leftOffset;
        rightFootSoleOffset = rightOffset;
        virtualGroundHeight = groundHeight;
    }
#endif

    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
