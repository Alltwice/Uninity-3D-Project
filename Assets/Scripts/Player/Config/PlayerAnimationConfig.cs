using UnityEngine;

[CreateAssetMenu(fileName = "PlayerAnimationConfig", menuName = "Player/Config/Animation")]
public sealed class PlayerAnimationConfig : ScriptableObject
{
    [Header("Locomotion")]
    [Tooltip("当前运动方向与期望方向达到该夹角时，解析为 180 度动画表现。")]
    [Range(90f, 180f)]
    [SerializeField] private float turn180Threshold = 150f;

    [Header("重落地")]
    [Tooltip("重落地动画允许被移动打断的归一化时间")]
    [SerializeField] private float hardLandingInterruptNormalizedTime = 0.6f;

    public float Turn180Threshold => turn180Threshold;
    public float HardLandingInterruptNormalizedTime => hardLandingInterruptNormalizedTime;
}
