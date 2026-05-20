using UnityEngine;

/// <summary>
/// 在拿到输入数据之后具体要处理的输入内容
/// </summary>
public class PlayerMotor : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("变向时旋转速度")]
    [SerializeField] private float rotationSmoothSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("为了让角色稳稳压在地上给一个向下的速度")]
    [SerializeField] private float groundedVerticalVelocity = -2f;
    //能够处理碰撞，斜坡，台阶，贴地哦
    private CharacterController characterController;
    private IPlayerInputSource inputSource;
    private Transform cameraTransform;
    //主要用于处理角色竖直方向上的下落和跳跃
    private Vector3 verticalVelocity;
    //安全锁，防止注入还没进行完毕就处理逻辑
    private bool initialized;
}
