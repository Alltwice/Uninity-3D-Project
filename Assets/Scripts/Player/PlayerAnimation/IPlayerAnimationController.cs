public interface IPlayerAnimationController
{
    bool IsHardLandingComplete { get; }
    bool CanInterruptHardLanding { get; }

    void PlayTransition(PlayerStateTransition transition);
}
