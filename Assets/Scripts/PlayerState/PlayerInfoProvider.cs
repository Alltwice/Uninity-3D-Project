using System;
using UnityEngine;
/// <summary>
/// 提供具体玩家数据给状态机用
/// </summary>
public class PlayerInfoProvider : MonoBehaviour
{ 
    [Header("需求引用")]
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private Transform cameraTransform;
    [Header("移动信息")] 
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;
    [Header("重力信息")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickVelocity = -2f;
    //具体信息容器
    private PlayerContext context;
    public PlayerContext Context => context;
    //输入能力接口获取输入信息
    //接口中字段的数据是需要具体实现它的类提供的，所以使用接口一定要找到实现它的类
    //否则数据就是空壳子，这个寻找方式可以是get，也可以是依赖注入的传参，依靠了里氏替换原则
    private IPlayerInputSource input;

    private void Awake()
    {
        
    }
    /// <summary>
    /// 写在Update中调用，实时传入参数
    /// </summary>
    public void RefreshContext()
    {
        
    }
}
