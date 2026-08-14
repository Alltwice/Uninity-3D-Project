using UnityEngine;

/// <summary>
/// 将 Animator 每次求值得到的 Root Motion 数据提交给 PlayerMotor
/// </summary>
public  class PlayerAnimationMotionSource : MonoBehaviour
{
    private Animator animator;
    private PlayerMotor playerMotor;
    private PlayerMotionCaptureRecorder captureRecorder;
    private PlayerMotionDebugOverlay debugOverlay;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerMotor = GetComponentInParent<PlayerMotor>();
        captureRecorder = GetComponentInParent<PlayerMotionCaptureRecorder>();
        debugOverlay = GetComponentInParent<PlayerMotionDebugOverlay>();
    }

    private void OnAnimatorMove()
    {
        Vector3 rootDelta = animator.deltaPosition;
        Vector3 positionBeforeMove = playerMotor.transform.position;
        playerMotor.SubmitAnimationMotion(rootDelta, animator.deltaRotation);
        Vector3 actualDisplacement = playerMotor.transform.position - positionBeforeMove;
        if (captureRecorder != null) captureRecorder.RecordAnimatorMotion(rootDelta, actualDisplacement, Time.deltaTime);
        if (debugOverlay != null) debugOverlay.RecordAnimatorMotion(rootDelta, actualDisplacement);
    }
}
