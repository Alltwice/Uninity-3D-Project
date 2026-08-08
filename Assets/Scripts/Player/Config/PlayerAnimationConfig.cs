using UnityEngine;

[CreateAssetMenu(fileName = "PlayerAnimationConfig", menuName = "Player/Config/Animation")]
public sealed class PlayerAnimationConfig : ScriptableObject
{
    [Header("重落地")]
    [Tooltip("重落地动画允许被移动打断的归一化时间")]
    [SerializeField] private float hardLandingInterruptNormalizedTime = 0.6f;

    public float HardLandingInterruptNormalizedTime => hardLandingInterruptNormalizedTime;
}
