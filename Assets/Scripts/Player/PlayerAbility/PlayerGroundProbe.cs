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
    [Tooltip(("检测点向上偏移程度"))]
    [SerializeField] private float probeStartOffset = 0.2f;
    [SerializeField] private float probeDistance = 1.0f;
    [Tooltip("胶囊体检测半径")]
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
        //球形范围射线检测，参数分别为，检测中心，半径，方向，返回被碰撞体信息，最大检测范围，检测层级，是否检测trigger
        //该段代码用于时刻检测地面距离
        HasGround = Physics.SphereCast(
            origin,
            radius,
            -up,
            out RaycastHit hit,
            maxCastDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
        //如果存在地面，设定地面距离
        if (HasGround)
        {
            GroundDistance = Mathf.Max(0f, hit.distance - probeStartOffset);
        }
        //否则默认调整为最大值
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
        //如果正在下落，并且不在地面上，检测到了地面且地面距离小于了提前量距离
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
        //绘制圆形
        Gizmos.DrawWireSphere(origin, radius);
        //绘制线条
        Gizmos.DrawLine(origin, origin - up * (probeStartOffset + probeDistance));
    }
#endif
}