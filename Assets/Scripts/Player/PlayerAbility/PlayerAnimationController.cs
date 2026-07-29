using Animancer;
using UnityEngine;

public interface IPlayerAnimationController
{
    void RequestLocomotion();
    void RequestJump();
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AnimancerComponent))]
[RequireComponent(typeof(PlayerAnimationDataSource))]
[DefaultExecutionOrder(100)]
public sealed class PlayerAnimationController : MonoBehaviour, IPlayerAnimationController
{
    [Header("References")]
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private PlayerAnimationDataSource dataSource;

    [Header("Transitions")]
    [SerializeField] private LinearMixerTransition locomotionTransition = new();
    [SerializeField] private ClipTransition jumpTransition = new();

    [Header("Locomotion")]
    [Min(0f)]
    [SerializeField] private float moveSpeedDampTime = 0.1f;

    private LinearMixerState locomotionState;
    private float locomotionParameter;
    private float locomotionParameterVelocity;

    private void Awake()
    {
        if (animancer == null)
        {
            animancer = GetComponent<AnimancerComponent>();
        }

        if (dataSource == null)
        {
            dataSource = GetComponent<PlayerAnimationDataSource>();
        }

        Animator animator = animancer != null ? animancer.Animator : null;
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animancer != null)
            {
                animancer.Animator = animator;
            }
        }

        if (animancer == null || dataSource == null || animator == null)
        {
            Debug.LogError(
                $"{nameof(PlayerAnimationController)} requires an Animator, " +
                $"{nameof(AnimancerComponent)}, and {nameof(PlayerAnimationDataSource)}.",
                this);
            enabled = false;
            return;
        }

        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
    }

    private void Start()
    {
        RequestLocomotion();
    }

    private void Update()
    {
        PlayerAnimationFrame frame = dataSource.Capture();
        locomotionParameter = Mathf.SmoothDamp(
            locomotionParameter,
            frame.NormalizedMoveSpeed,
            ref locomotionParameterVelocity,
            moveSpeedDampTime);

        if (locomotionState != null)
        {
            locomotionState.Parameter = locomotionParameter;
        }
    }

    public void RequestLocomotion()
    {
        if (!isActiveAndEnabled || animancer == null || !locomotionTransition.IsValid)
        {
            return;
        }

        locomotionState = animancer.Play(locomotionTransition) as LinearMixerState;
        if (locomotionState != null)
        {
            locomotionState.Parameter = locomotionParameter;
        }
    }

    public void RequestJump()
    {
        if (!isActiveAndEnabled || animancer == null || !jumpTransition.IsValid)
        {
            return;
        }

        AnimancerState jumpState = animancer.Play(jumpTransition);
        jumpState.Time = 0f;
    }
}
