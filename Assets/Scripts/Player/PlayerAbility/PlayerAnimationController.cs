using Animancer;
using UnityEngine;
/// <summary>
/// 动画播放控制器
/// </summary>
public class PlayerAnimationController : MonoBehaviour, IPlayerAnimationController
{
    [Header("引用")]
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private PlayerAnimationDataSource dataSource;

    [Header("动画参数设置")]
    //该存在类似与BlendTree
    [SerializeField] private LinearMixerTransition locomotionTransition = new LinearMixerTransition();
    //管理动画数据（开始结束时间，过度，速度等）
    [SerializeField] private ClipTransition jumpTransition = new ClipTransition();
    //运行状态下的混合数
    private LinearMixerState locomotionState;
    private float locomotionParameter;


    private void Awake()
    { 
        animancer = GetComponent<AnimancerComponent>();
        dataSource = GetComponent<PlayerAnimationDataSource>();
    }

    private void Start()
    {
        RequestLocomotion();
    }

    private void LateUpdate()
    {
        PlayerAnimationFrame frame = dataSource.Capture();
        locomotionState.Parameter = frame.NormalizedMoveSpeed;
    }
    /// <summary>
    /// 请求播放移动动画
    /// </summary>
    public void RequestLocomotion()
    {
        //将父类AnimancerState转换为具体的LinearMixerState
        locomotionState = (LinearMixerState)animancer.Play(locomotionTransition);
        locomotionState.Parameter = locomotionParameter;
    }
    /// <summary>
    /// 请求播放跳跃动画
    /// </summary>
    public void RequestJump()
    {
        AnimancerState jumpState = animancer.Play(jumpTransition);
    }
}
