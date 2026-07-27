    using System;
    using Unity.Cinemachine;
    using UnityEngine;
/// <summary>
/// 由鼠标控制角色身上子物体的旋转以控制摄像机位置
/// </summary>
public class PlayerCameraOrbitTarget : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private CinemachineThirdPersonFollow thirdPersonFollow;
    [Header("鼠标灵敏度")]
    [SerializeField] private float mouseSensitivity=0.12f;
    [Header("最大俯仰角")]
    [SerializeField] private float minPitch=-50;
    [SerializeField] private float maxPitch=60;
    [Header("镜头远近")] 
    [SerializeField] private float normalDistance = 4f;
    [SerializeField] private float topViewDistance = 6.5f;
    [SerializeField] private float distanceSmoothSpeed = 8f;
    [SerializeField] private float begainToTopViewDistance = 10f;
    private IPlayerInputSource inputSource;
    //偏航角（左右）和俯仰角（上下）旋转角度
    private float yaw;
    private float pitch;
    public float Yaw => yaw;
    public float Pitch => pitch;
    private float currentDistance;

    private void Awake()
    {
        currentDistance = normalDistance;
    }
    /// <summary>
    /// 外部主动依赖注入
    /// </summary>
    /// <param name="inputSource">输入源</param>
    public void Init(IPlayerInputSource inputSource)
    {
        this.inputSource = inputSource;
    }

    private void LateUpdate()
    {
        UpdateRotation();
        UpdateDistanceByPitch();
    }
    /// <summary>
    /// 刷新物体旋转角度
    /// </summary>
    private void UpdateRotation()
    {
        Vector2 lookInput = inputSource.LookInput;
        yaw += lookInput.x * mouseSensitivity;
        pitch += lookInput.y * mouseSensitivity*-1;
        //限定其大小最小不超过min最大不超过max
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        //创建一个旋转且不会出现倾斜
        transform.rotation = Quaternion.Euler(pitch, yaw, 0.0f);
    }
    /// <summary>
    /// 处理摄像机俯视拉高效果
    /// </summary>
    private void UpdateDistanceByPitch()
    {
        // 假设 pitch 越大越偏俯视,这里设置了y轴翻转*-1即可
        //这个mathf方法会计算c在a到b之间的比例并返回
        float topViewRate = Mathf.InverseLerp(begainToTopViewDistance, maxPitch, pitch);
        //拿到比例之后算出具体的当前值是多少
        float targetDistance = Mathf.Lerp(normalDistance, topViewDistance, topViewRate);
        //当前的距离由当前值到目标值并由设置的速度移动
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, distanceSmoothSpeed * Time.deltaTime);
        //设置位置即可
        thirdPersonFollow.CameraDistance = currentDistance;
    }
}
