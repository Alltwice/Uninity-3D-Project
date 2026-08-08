using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMotorConfig", menuName = "Player/Config/Motor")]
public sealed class PlayerMotorConfig : ScriptableObject
{
    [Header("移动速度")]
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float runSpeed = 5f; 
    [SerializeField] private float fastRunSpeed = 7.5f;
    [SerializeField] private float airMoveSpeed = 3f;

    [Header("移动加速度")]
    [SerializeField] private float groundAcceleration = 20f;
    [SerializeField] private float groundDeceleration = 15f;
    [SerializeField] private float groundTurnAcceleration = 80f;
    [SerializeField] private float airAcceleration = 10f;

    [Header("其他")]
    [Tooltip("变向时旋转速度")]
    [SerializeField] private float rotationSmoothSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("为了让角色稳稳压在地上给一个向下的速度")]
    [SerializeField] private float groundedVerticalVelocity = -2f;

    [Header("落地判定")]
    [SerializeField] private float hardLandingMinImpactSpeed = 10f;

    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float FastRunSpeed => fastRunSpeed;
    public float AirMoveSpeed => airMoveSpeed;
    public float GroundAcceleration => groundAcceleration;
    public float GroundDeceleration => groundDeceleration;
    public float GroundTurnAcceleration => groundTurnAcceleration;
    public float AirAcceleration => airAcceleration;
    public float RotationSmoothSpeed => rotationSmoothSpeed;
    public float Gravity => gravity;
    public float GroundedVerticalVelocity => groundedVerticalVelocity;
    public float HardLandingMinImpactSpeed => hardLandingMinImpactSpeed;
}
