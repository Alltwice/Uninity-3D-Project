using UnityEngine;

public enum RunTransitionMotionMode
{
    RuntimeRootMotion,
    ProfileDriven
}

[CreateAssetMenu(fileName = "PlayerMotionConfig", menuName = "Player/Motion/Config")]
public sealed class PlayerMotionConfig : ScriptableObject
{
    [SerializeField] private RunTransitionMotionMode mode;
    [SerializeField] private PlayerMotionProfile runStartProfile;
    [SerializeField] private PlayerMotionProfile runStopProfile;

    public RunTransitionMotionMode Mode => mode;
    public PlayerMotionProfile RunStartProfile => runStartProfile;
    public PlayerMotionProfile RunStopProfile => runStopProfile;

    public void SetMode(RunTransitionMotionMode value)
    {
        mode = value;
    }
}
