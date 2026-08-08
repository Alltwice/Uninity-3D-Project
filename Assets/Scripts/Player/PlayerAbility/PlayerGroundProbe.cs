using UnityEngine;

/// <summary>
/// 玩家地面距离探测
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerGroundProbe : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private CharacterController characterController;
    [Header("配置")]
    [SerializeField] private PlayerGroundProbeConfig config;
    public bool HasGround { get; private set; }
    public bool HasWalkableGround { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public float GroundDistance { get; private set; } = float.PositiveInfinity;
    public bool CanSnapToGround => HasWalkableGround && GroundDistance <= config.GroundSnapDistance;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    /// <summary>
    /// 刷新地面距离
    /// </summary>
    public void Refresh()
    {
        Vector3 up = transform.up;

        Vector3 worldCenter = transform.TransformPoint(characterController.center);
        float halfHeight = characterController.height * 0.5f;
        float radius = characterController.radius * config.RadiusScale;
        float radiusDifference = characterController.radius - radius;

        // CharacterController 下半球中心
        Vector3 bottomSphereCenter = worldCenter - up * (halfHeight - characterController.radius);

        // 从脚底球心上方一点开始向下检测，避免初始位置贴地导致检测不稳定
        Vector3 origin = bottomSphereCenter + up * config.ProbeStartOffset;
        float maxCastDistance = config.ProbeStartOffset + radiusDifference + config.ProbeDistance;
        //球形范围射线检测，参数分别为，检测中心，半径，方向，返回被碰撞体信息，最大检测范围，检测层级，是否检测trigger
        //该段代码用于时刻检测地面距离
        HasGround = Physics.SphereCast(
            origin,
            radius,
            -up,
            out RaycastHit hit,
            maxCastDistance,
            config.GroundMask,
            QueryTriggerInteraction.Ignore
        );
        //如果存在地面，设定地面距离
        if (HasGround)
        {
            GroundDistance = Mathf.Max(0f, hit.distance - config.ProbeStartOffset - radiusDifference);
            //拿到法线
            GroundNormal = hit.normal;
            //计算法线和角色之间角度判断是否可以行走
            HasWalkableGround = Vector3.Angle(GroundNormal, up) <= characterController.slopeLimit;
        }
        //否则默认调整为最大值
        else
        {
            GroundDistance = float.PositiveInfinity;
            GroundNormal = up;
            HasWalkableGround = false;
        }
    }

    public bool IsNearGround(float verticalSpeed, bool isGrounded)
    {
        // 根据下落速度动态计算提前量。
        // 下落越快，越早进入落地预判。
        float anticipationDistance = Mathf.Clamp(
            -verticalSpeed * config.LandingAnticipationTime,
            config.MinAnticipationDistance,
            config.MaxAnticipationDistance
        );
        //如果正在下落，并且不在地面上，检测到了地面且地面距离小于了提前量距离
        return
            !isGrounded &&
            verticalSpeed < 0f &&
            HasWalkableGround &&
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
        float radius = characterController.radius * config.RadiusScale;
        float radiusDifference = characterController.radius - radius;
        Vector3 bottomSphereCenter = worldCenter - up * (halfHeight - characterController.radius);
        Vector3 origin = bottomSphereCenter + up * config.ProbeStartOffset;
        //绘制圆形
        Gizmos.DrawWireSphere(origin, radius);
        //绘制线条
        Gizmos.DrawLine(origin, origin - up * (config.ProbeStartOffset + radiusDifference + config.ProbeDistance));
    }
#endif
}
