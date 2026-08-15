using UnityEngine;

/// <summary>
/// 玩家每帧唯一执行顺序；不包含任何具体动作分支。
/// </summary>
[RequireComponent(typeof(PlayerStateController), typeof(PlayerMotionPlanner), typeof(PlayerMotor))]
public sealed class PlayerSimulationDriver : MonoBehaviour
{
    [SerializeField] private Transform movementReference;

    private PlayerStateController stateController;
    private PlayerMotionPlanner motionPlanner;
    private PlayerMotor motor;
    private PlayerAnimationController animationController;
    private PlayerDodge dodge;
    private IPlayerInputSource inputSource;
    private IPlayerActionBuffer actionBuffer;
    private PlayerStateTransition? pendingTransition;

    private void Awake()
    {
        stateController = GetComponent<PlayerStateController>();
        motionPlanner = GetComponent<PlayerMotionPlanner>();
        motor = GetComponent<PlayerMotor>();
        animationController = GetComponent<PlayerAnimationController>();
        dodge = GetComponent<PlayerDodge>();
        if (movementReference == null) movementReference = Camera.main.transform;
    }

    public void Init(IPlayerInputSource playerInput, IPlayerActionBuffer playerActionBuffer)
    {
        inputSource = playerInput;
        actionBuffer = playerActionBuffer;
    }

    private void Start()
    {
        motor.EnsureInitialized();
        pendingTransition = stateController.Initialize(inputSource, actionBuffer);
        animationController.InitializeManualEvaluation();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        actionBuffer.Tick(deltaTime);
        motionPlanner.BeginFrame();
        dodge.TickCooldown(deltaTime);
        Vector3 desiredMoveDirection = ResolveWorldMoveDirection(inputSource.MoveInput);
        stateController.SetSimulationFacts(motor.CurrentResult, motionPlanner.Snapshot, desiredMoveDirection);
        PlayerStateTransition? transition = stateController.ProcessPreTickTransition();
        PlayerGameplayIntent intent = PlayerGameplayIntent.Create(desiredMoveDirection, transform.forward);
        intent.LocomotionMode = stateController.CurrentLocomotionMode;
        if (transition.HasValue) motionPlanner.HandleStateTransition(transition.Value, intent, motor.CurrentResult);
        else if (pendingTransition.HasValue) motionPlanner.HandleStateTransition(pendingTransition.Value, intent, motor.CurrentResult);
        stateController.Tick(deltaTime, ref intent);
        motionPlanner.ResolveContinuousMotion(stateController.CurrentState.GetType(), intent, motor.CurrentResult);
        PlayerMotionFrame motionFrame = motionPlanner.Advance(deltaTime, intent);
        PlayerMotorCommand command = PlayerMotionComposer.Compose(intent, motionFrame, motor.CurrentResult, motor.Config, deltaTime, transform.forward);
        PlayerMotorResult motorResult = motor.Simulate(command, deltaTime);
        stateController.SetSimulationFacts(motorResult, motionPlanner.Snapshot, desiredMoveDirection);
        PlayerStateTransition? resultTransition = stateController.ProcessPostTickTransition();
        if (resultTransition.HasValue)
        {
            PlayerGameplayIntent postTransitionIntent = PlayerGameplayIntent.Create(desiredMoveDirection, transform.forward);
            postTransitionIntent.LocomotionMode = stateController.CurrentLocomotionMode;
            motionPlanner.HandleStateTransition(resultTransition.Value, postTransitionIntent, motorResult);
        }
        PlayerStateTransition? presentationTransition = resultTransition ?? transition ?? pendingTransition;
        pendingTransition = null;
        animationController.Present(stateController.CurrentState.GetType(), presentationTransition, motionPlanner.Snapshot, motorResult, stateController.CurrentPresentationProgress);
        animationController.EvaluateGraph(deltaTime);
    }

    private Vector3 ResolveWorldMoveDirection(Vector2 moveInput)
    {
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        if (input.sqrMagnitude > 1f) input.Normalize();
        Vector3 forward = movementReference.forward;
        Vector3 right = movementReference.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        Vector3 worldDirection = forward * input.z + right * input.x;
        return worldDirection.sqrMagnitude > 1f ? worldDirection.normalized : worldDirection;
    }
}
