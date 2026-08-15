using UnityEngine;

/// <summary>
/// 只维护 Dodge 的进入规则与冷却；位移和时长由通用 Motion Runtime 负责。
/// </summary>
public sealed class PlayerDodge : MonoBehaviour
{
    [Header("闪避配置")]
    [SerializeField] private PlayerDodgeConfig config;

    private float cooldownRemaining;
    private bool active;

    public bool CanDodge => !active && cooldownRemaining <= 0f;

    public void TickCooldown(float deltaTime) => cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Mathf.Max(0f, deltaTime));
    public void Begin() => active = true;

    public void End()
    {
        if (!active) return;
        active = false;
        cooldownRemaining = config.Cooldown;
    }
}
