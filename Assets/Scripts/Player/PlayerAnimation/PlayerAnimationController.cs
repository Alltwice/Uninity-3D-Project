using Animancer;
using UnityEngine;
/// <summary>
/// 动画播放控制器
/// </summary>
public class PlayerAnimationController : MonoBehaviour, IPlayerAnimationController
{
    private enum ActiveAnimation
    {
        None,
        Locomotion,
        FastRun,
        JumpUp,
        JumpIdle,
        HardLanding
    }

    [Header("引用")]
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private PlayerAnimationDataSource dataSource;

    [Header("动画参数设置")]
    [SerializeField] private LinearMixerTransition locomotionTransition = new LinearMixerTransition();
    [SerializeField] private ClipTransition fastRunTransition = new ClipTransition();
    [SerializeField] private ClipTransition jumpUpTransition = new ClipTransition();
    [SerializeField] private ClipTransition jumpIdleTransition = new ClipTransition();
    [SerializeField] private ClipTransition hardLandingTransition = new ClipTransition();
    private LinearMixerState locomotionState;
    private AnimancerState jumpUpState;
    private AnimancerState hardLandingState;
    private ActiveAnimation activeAnimation;

    public bool IsHardLandingComplete => activeAnimation == ActiveAnimation.HardLanding && hardLandingState.NormalizedTime >= 0.6f;

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
        //防止CC首帧出现触地从而导致无法正常触发动画
        bool shouldUseAirAnimation = !frame.IsGrounded || (activeAnimation == ActiveAnimation.JumpUp && frame.VerticalSpeed > 0f);

        if (shouldUseAirAnimation)
        {
            if (activeAnimation != ActiveAnimation.JumpUp || jumpUpState.NormalizedTime >= 1f)
            {
                RequestJumpIdle();
            }
            return;
        }

        if (frame.IsHardLandingImpact)
        {
            RequestHardLanding();
            return;
        }

        if (activeAnimation == ActiveAnimation.HardLanding)
        {
            return;
        }

        if (frame.LocomotionMode == PlayerLocomotionMode.FastRun)
        {
            RequestFastRun();
            return;
        }

        RequestLocomotion();
        locomotionState.Parameter = frame.HorizontalSpeed;
    }

    public void RequestLocomotion()
    {
        if (activeAnimation == ActiveAnimation.Locomotion)
        {
            return;
        }

        //将父类AnimancerState转换为具体的LinearMixerState
        locomotionState = (LinearMixerState)animancer.Play(locomotionTransition);
        activeAnimation = ActiveAnimation.Locomotion;
    }

    private void RequestFastRun()
    {
        if (activeAnimation == ActiveAnimation.FastRun)
        {
            return;
        }
        animancer.Play(fastRunTransition);
        activeAnimation = ActiveAnimation.FastRun;
    }

    public void RequestJumpUp()
    {
        jumpUpState = animancer.Play(jumpUpTransition);
        activeAnimation = ActiveAnimation.JumpUp;
    }

    private void RequestJumpIdle()
    {
        if (activeAnimation == ActiveAnimation.JumpIdle)
        {
            return;
        }

        animancer.Play(jumpIdleTransition);
        activeAnimation = ActiveAnimation.JumpIdle;
    }

    public void RequestHardLanding()
    {
        if (activeAnimation == ActiveAnimation.HardLanding)
        {
            return;
        }

        hardLandingState = animancer.Play(hardLandingTransition);
        activeAnimation = ActiveAnimation.HardLanding;
    }

    public void ReleaseHardLanding()
    {
        if (activeAnimation == ActiveAnimation.HardLanding)
        {
            activeAnimation = ActiveAnimation.None;
        }
    }
}
