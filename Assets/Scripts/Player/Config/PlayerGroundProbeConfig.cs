using UnityEngine;

[CreateAssetMenu(fileName = "PlayerGroundProbeConfig", menuName = "Player/Config/Ground Probe")]
public sealed class PlayerGroundProbeConfig : ScriptableObject
{
    [Header("地面检测")]
    [SerializeField] private LayerMask groundMask;
    [Tooltip("检测点向上偏移程度")]
    [SerializeField] private float probeStartOffset = 0.2f;
    [SerializeField] private float probeDistance = 1f;
    [SerializeField] private float groundSnapDistance = 0.3f;
    [Tooltip("胶囊体检测半径比例")]
     [SerializeField] private float radiusScale = 0.9f;

    [Header("落地预判")]
    [SerializeField] private float landingAnticipationTime = 0.12f;
    [SerializeField] private float minAnticipationDistance = 0.15f;
    [SerializeField] private float maxAnticipationDistance = 0.8f;

    public LayerMask GroundMask => groundMask;
    public float ProbeStartOffset => probeStartOffset;
    public float ProbeDistance => probeDistance;
    public float GroundSnapDistance => groundSnapDistance;
    public float RadiusScale => radiusScale;
    public float LandingAnticipationTime => landingAnticipationTime;
    public float MinAnticipationDistance => minAnticipationDistance;
    public float MaxAnticipationDistance => maxAnticipationDistance;
}
