using UnityEngine;

[RequireComponent(typeof(PlayerMotor), typeof(PlayerTransitionMotionController), typeof(PlayerAnimationController))]
public sealed class PlayerMotionDebugOverlay : MonoBehaviour
{
    [SerializeField] private bool showOverlay = true;

    private PlayerMotor playerMotor;
    private PlayerTransitionMotionController transitionMotionController;
    private PlayerAnimationController animationController;
    private Vector3 animatorRootMotionDelta;
    private Vector3 animationDrivenActualDelta;

    private void Awake()
    {
        playerMotor = GetComponent<PlayerMotor>();
        transitionMotionController = GetComponent<PlayerTransitionMotionController>();
        animationController = GetComponent<PlayerAnimationController>();
    }

    public void RecordAnimatorMotion(Vector3 rootMotionDelta, Vector3 actualDelta)
    {
        animatorRootMotionDelta = Vector3.ProjectOnPlane(rootMotionDelta, Vector3.up);
        animationDrivenActualDelta = Vector3.ProjectOnPlane(actualDelta, Vector3.up);
    }

    private void OnGUI()
    {
        if (!showOverlay) return;
        PlayerMotionConfig config = transitionMotionController.Config;
        Vector3 actualDelta = playerMotor.MotionMode == PlayerMotionMode.ProfileDriven ? transitionMotionController.LastActualFrameDisplacement : animationDrivenActualDelta;
        GUILayout.BeginArea(new Rect(12f, 12f, 470f, 370f), GUI.skin.box);
        GUILayout.Label("Run Transition Motion Experiment");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("A Runtime Root Motion")) config.SetMode(RunTransitionMotionMode.RuntimeRootMotion);
        if (GUILayout.Button("B Profile Driven")) config.SetMode(RunTransitionMotionMode.ProfileDriven);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("30 FPS")) SetTargetFrameRate(30);
        if (GUILayout.Button("60 FPS")) SetTargetFrameRate(60);
        if (GUILayout.Button("120 FPS")) SetTargetFrameRate(120);
        GUILayout.EndHorizontal();
        GUILayout.Label($"A/B Mode: {config.Mode}");
        GUILayout.Label($"Motor MotionMode: {playerMotor.MotionMode}");
        GUILayout.Label($"Profile Motion: {transitionMotionController.CurrentMotionType}");
        GUILayout.Label($"Current Profile: {(transitionMotionController.CurrentProfile == null ? "None" : transitionMotionController.CurrentProfile.name)}");
        GUILayout.Label($"Gameplay Progress: {transitionMotionController.Progress:F3}");
        GUILayout.Label($"Animation Transition Time: {animationController.DebugLocomotionTransitionNormalizedTime:F3}");
        GUILayout.Label($"Horizontal Speed: {playerMotor.HorizontalSpeed:F3}");
        GUILayout.Label($"Profile Cumulative Distance: {transitionMotionController.CurrentCumulativeDistance:F4}");
        GUILayout.Label($"Profile Frame Delta Distance: {transitionMotionController.LastFrameDeltaDistance:F4}");
        GUILayout.Label($"Character Actual Delta: {actualDelta:F4}");
        GUILayout.Label($"Animator Root Motion Delta: {animatorRootMotionDelta:F4}");
        GUILayout.Label($"Desired Move Direction: {playerMotor.DesiredMoveDirection:F3}");
        GUILayout.Label($"Target Frame Rate: {Application.targetFrameRate}");
        GUILayout.EndArea();
    }

    private static void SetTargetFrameRate(int frameRate)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = frameRate;
    }
}
