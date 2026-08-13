using UnityEngine;

[CreateAssetMenu(fileName = "PlayerAnimationConfig", menuName = "Player/Config/Animation")]
public sealed class PlayerAnimationConfig : ScriptableObject
{
    [Header("Locomotion")]
    [Tooltip("当前运动方向与期望方向达到该夹角时，解析为 180 度动画表现。")]
    [Range(90f, 180f)]
    [SerializeField] private float turn180Threshold = 150f;

    [Tooltip("Turn180 表现触发后，当前移动意图偏离原请求方向超过该容差角度时立即取消表现。")]
    [Range(0f, 90f)]
    [SerializeField] private float turnPresentationIntentTolerance = 30f;

    [Header("重落地")]
    [Tooltip("重落地动画允许被移动打断的归一化时间")]
    [SerializeField] private float hardLandingInterruptNormalizedTime = 0.6f;

    public float Turn180Threshold => turn180Threshold;
    public float TurnPresentationIntentTolerance => turnPresentationIntentTolerance;
    public float HardLandingInterruptNormalizedTime => hardLandingInterruptNormalizedTime;
}
