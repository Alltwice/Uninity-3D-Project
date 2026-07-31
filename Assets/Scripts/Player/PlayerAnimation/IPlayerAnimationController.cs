using UnityEngine;

public interface IPlayerAnimationController
{
    bool IsHardLandingComplete { get; }

    void RequestLocomotion();
    void RequestJumpUp();
    void RequestHardLanding();
    void ReleaseHardLanding();
}
