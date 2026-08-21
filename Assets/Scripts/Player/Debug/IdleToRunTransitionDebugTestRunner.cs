using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 以随机 Idle/移动持续时间重复驱动 Idle → IdleToRun → GroundLocomotion → Idle 实验。
/// </summary>
[DefaultExecutionOrder(-10000)]
[RequireComponent(typeof(PlayerInputReader), typeof(PlayerSimulationDriver), typeof(PlayerStateController))]
public class IdleToRunTransitionDebugTestRunner : MonoBehaviour
{
    private enum TestPhase
    {
        Stopped,
        WaitingForIdle,
        IdleWait,
        Moving,
        WaitingForReturnIdle,
        Completed
    }

    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly PropertyInfo MoveInputProperty = typeof(PlayerInputReader).GetProperty(nameof(PlayerInputReader.MoveInput), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingMemberException(typeof(PlayerInputReader).FullName, nameof(PlayerInputReader.MoveInput));
    private static readonly PropertyInfo WalkModeProperty = typeof(PlayerInputReader).GetProperty(nameof(PlayerInputReader.IsWalkMode), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingMemberException(typeof(PlayerInputReader).FullName, nameof(PlayerInputReader.IsWalkMode));
    private static readonly FieldInfo MovementReferenceField = typeof(PlayerSimulationDriver).GetField("movementReference", InstancePrivate) ?? throw new MissingFieldException(typeof(PlayerSimulationDriver).FullName, "movementReference");

    [Header("实验")]
    [Min(30)] [SerializeField] private int totalTests = 30;
    [SerializeField] private bool runOnStart = true;
    [Tooltip("0 使用当前时钟作为随机种子；非 0 可复现实验时序。")]
    [SerializeField] private int randomSeed;
    [SerializeField] private Vector2 idleWaitRange = new Vector2(0.5f, 2f);
    [SerializeField] private Vector2 moveDurationRange = new Vector2(2f, 5f);
    [Min(0f)] [SerializeField] private float returnIdleStableDuration = 0.25f;

    [Header("主观卡顿标记")]
    [SerializeField] private Key observedStallMarkerKey = Key.F8;

    [Header("采集组件")]
    [SerializeField] private IdleToRunTransitionDebugProbe probe;
    [SerializeField] private IdleToRunTransitionDebugLogger logger;

    private PlayerInputReader inputReader;
    private PlayerSimulationDriver simulationDriver;
    private PlayerStateController stateController;
    private PlayerMotionPlanner motionPlanner;
    private Transform movementReference;
    private System.Random random;
    private TestPhase phase;
    private float phaseElapsed;
    private float currentIdleWait;
    private float currentMoveDuration;
    private bool inputReaderWasEnabled;
    private bool sessionStarted;
    private int observedStallMarkerFrame = -1;

    public int CurrentTestId { get; private set; }
    public int CurrentTestFrame { get; private set; }
    public bool IsCapturing => sessionStarted && CurrentTestId > 0 && phase != TestPhase.Completed && phase != TestPhase.Stopped;
    public bool ObservedStallMarkedThisFrame => observedStallMarkerFrame == Time.frameCount;
    public int ObservedStallMarkerId { get; private set; }
    public string OutputDirectory => logger == null ? string.Empty : logger.LastOutputDirectory;

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        simulationDriver = GetComponent<PlayerSimulationDriver>();
        stateController = GetComponent<PlayerStateController>();
        motionPlanner = GetComponent<PlayerMotionPlanner>();
        logger = logger == null ? GetComponent<IdleToRunTransitionDebugLogger>() : logger;
        if (logger == null) logger = gameObject.AddComponent<IdleToRunTransitionDebugLogger>();
        probe = probe == null ? GetComponent<IdleToRunTransitionDebugProbe>() : probe;
        if (probe == null) probe = gameObject.AddComponent<IdleToRunTransitionDebugProbe>();
        probe.Configure(this, logger);
    }

    private void Start()
    {
        movementReference = (Transform)MovementReferenceField.GetValue(simulationDriver);
        if (runOnStart) BeginExperiment();
    }

    [ContextMenu("Run IdleToRun Transition Experiment")]
    public void BeginExperiment()
    {
        if (sessionStarted) return;
        int seed = randomSeed == 0 ? Environment.TickCount : randomSeed;
        random = new System.Random(seed);
        inputReaderWasEnabled = inputReader.enabled;
        inputReader.enabled = false;
        SetInput(Vector2.zero);
        CurrentTestId = 0;
        CurrentTestFrame = 0;
        phaseElapsed = 0f;
        observedStallMarkerFrame = -1;
        ObservedStallMarkerId = 0;
        phase = TestPhase.WaitingForIdle;
        sessionStarted = true;
        logger.StartSession(totalTests, seed);
    }

    private void Update()
    {
        if (!sessionStarted) return;
        if (Keyboard.current != null && Keyboard.current[observedStallMarkerKey].wasPressedThisFrame)
        {
            observedStallMarkerFrame = Time.frameCount;
            ObservedStallMarkerId++;
        }
        if (IsCapturing) CurrentTestFrame++;
        switch (phase)
        {
            case TestPhase.WaitingForIdle:
                SetInput(Vector2.zero);
                if (IsIdleAndMotionComplete()) BeginNextTest();
                break;
            case TestPhase.IdleWait:
                SetInput(Vector2.zero);
                phaseElapsed += Time.deltaTime;
                if (phaseElapsed >= currentIdleWait) { phase = TestPhase.Moving; phaseElapsed = 0f; SetInput(ResolveForwardInput()); }
                break;
            case TestPhase.Moving:
                SetInput(ResolveForwardInput());
                phaseElapsed += Time.deltaTime;
                if (phaseElapsed >= currentMoveDuration) { phase = TestPhase.WaitingForReturnIdle; phaseElapsed = 0f; SetInput(Vector2.zero); }
                break;
            case TestPhase.WaitingForReturnIdle:
                SetInput(Vector2.zero);
                if (!IsIdleAndMotionComplete()) { phaseElapsed = 0f; break; }
                phaseElapsed += Time.deltaTime;
                if (phaseElapsed >= returnIdleStableDuration) CompleteCurrentTest();
                break;
        }
    }

    private void BeginNextTest()
    {
        CurrentTestId++;
        CurrentTestFrame = 0;
        currentIdleWait = Range(idleWaitRange);
        currentMoveDuration = Range(moveDurationRange);
        phaseElapsed = 0f;
        phase = TestPhase.IdleWait;
        logger.BeginTest(CurrentTestId, currentIdleWait, currentMoveDuration);
    }

    private void CompleteCurrentTest()
    {
        logger.EndTest(CurrentTestId);
        if (CurrentTestId >= totalTests)
        {
            phase = TestPhase.Completed;
            logger.FinishSession(CurrentTestId);
            RestoreInputReader();
            sessionStarted = false;
            return;
        }
        phase = TestPhase.WaitingForIdle;
        phaseElapsed = 0f;
    }

    private bool IsIdleAndMotionComplete()
    {
        return stateController.CurrentLocomotionMode == PlayerLocomotionMode.Idle && !motionPlanner.Snapshot.IsActive;
    }

    private Vector2 ResolveForwardInput()
    {
        Transform reference = movementReference == null ? Camera.main.transform : movementReference;
        Vector3 desired = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 referenceForward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
        Vector3 referenceRight = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
        Vector2 input = new Vector2(Vector3.Dot(desired, referenceRight), Vector3.Dot(desired, referenceForward));
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private void SetInput(Vector2 moveInput)
    {
        MoveInputProperty.SetValue(inputReader, moveInput);
        WalkModeProperty.SetValue(inputReader, false);
    }

    private float Range(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private void RestoreInputReader()
    {
        SetInput(Vector2.zero);
        inputReader.enabled = inputReaderWasEnabled;
    }

    private void OnDisable()
    {
        if (!sessionStarted) return;
        logger.FinishSession(CurrentTestId);
        RestoreInputReader();
        sessionStarted = false;
        phase = TestPhase.Stopped;
    }

    private void OnValidate()
    {
        totalTests = Mathf.Max(30, totalTests);
        idleWaitRange.x = Mathf.Max(0f, idleWaitRange.x);
        idleWaitRange.y = Mathf.Max(idleWaitRange.x, idleWaitRange.y);
        moveDurationRange.x = Mathf.Max(0f, moveDurationRange.x);
        moveDurationRange.y = Mathf.Max(moveDurationRange.x, moveDurationRange.y);
    }
}
