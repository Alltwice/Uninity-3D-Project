using UnityEngine;

/// <summary>
/// 将 Animator 每次求值得到的 Root Motion 数据提交给 PlayerMotor
/// </summary>
public  class PlayerAnimationMotionSource : MonoBehaviour
{
    private Animator animator;
    private PlayerMotor playerMotor;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerMotor = GetComponentInParent<PlayerMotor>();
    }

    private void OnAnimatorMove()
    {
        playerMotor.SubmitAnimationMotion(animator.deltaPosition, animator.deltaRotation);
    }
}
