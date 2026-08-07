using UnityEngine;

public interface IPlayerAnimationController
{
    bool IsHardLandingComplete { get; }

    void RequestLocomotion();
    void RequestFastRunStop();
    void RequestJumpUp();
    void RequestHardLanding();
    void ReleaseHardLanding();
}
