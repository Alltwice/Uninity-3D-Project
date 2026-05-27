using UnityEngine;

/// <summary>
/// 玩家地面距离探测
/// </summary>
public class PlayerGroundProbe : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private CharacterController characterController;
    [Header("地面检测")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float probeStartOffset = 0.2f;
    [SerializeField] private float probeDistance = 1.0f;
    [SerializeField] private float radiusScale = 0.9f;
    [Header("落地预判")]
    [SerializeField] private float landingAnticipationTime = 0.12f;
    [SerializeField] private float minAnticipationDistance = 0.15f;
    [SerializeField] private float maxAnticipationDistance = 0.8f;
    public bool HasGround { get; private set; }
    public bool IsNearGround { get; private set; }
    public float GroundDistance { get; private set; } = float.PositiveInfinity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    /// <summary>
    /// 刷新地面距离
    /// </summary>
    public void Refresh(float verticalSpeed, bool isGrounded)
    {
        Vector3 up = transform.up;

        Vector3 worldCenter = transform.TransformPoint(characterController.center);
        float halfHeight = characterController.height * 0.5f;
        float radius = characterController.radius * radiusScale;

        // CharacterController 下半球中心
        Vector3 bottomSphereCenter = worldCenter - up * (halfHeight - characterController.radius);

        // 从脚底球心上方一点开始向下检测，避免初始位置贴地导致检测不稳定
        Vector3 origin = bottomSphereCenter + up * probeStartOffset;
        float maxCastDistance = probeStartOffset + probeDistance;

        HasGround = Physics.SphereCast(
            origin,
            radius,
            -up,
            out RaycastHit hit,
            maxCastDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (HasGround)
        {
            GroundDistance = Mathf.Max(0f, hit.distance - probeStartOffset);
        }
        else
        {
            GroundDistance = float.PositiveInfinity;
        }

        // 根据下落速度动态计算提前量。
        // 下落越快，越早进入落地预判。
        float anticipationDistance = Mathf.Clamp(
            -verticalSpeed * landingAnticipationTime,
            minAnticipationDistance,
            maxAnticipationDistance
        );

        IsNearGround =
            !isGrounded &&
            verticalSpeed < 0f &&
            HasGround &&
            GroundDistance <= anticipationDistance;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
        if (characterController == null)
        {
            return;
        }
        Vector3 up = transform.up;
        Vector3 worldCenter = transform.TransformPoint(characterController.center);
        float halfHeight = characterController.height * 0.5f;
        float radius = characterController.radius * radiusScale;

        Vector3 bottomSphereCenter = worldCenter - up * (halfHeight - characterController.radius);
        Vector3 origin = bottomSphereCenter + up * probeStartOffset;

        Gizmos.DrawWireSphere(origin, radius);
        Gizmos.DrawLine(origin, origin - up * (probeStartOffset + probeDistance));
    }
#endif
}