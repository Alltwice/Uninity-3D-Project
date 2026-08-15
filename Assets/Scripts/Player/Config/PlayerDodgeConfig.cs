using UnityEngine;

[CreateAssetMenu(fileName = "PlayerDodgeConfig", menuName = "Player/Config/Dodge")]
public sealed class PlayerDodgeConfig : ScriptableObject
{
    [Header("闪避 Gameplay Rule")]
    [Min(0f)] [SerializeField] private float cooldown = 0.35f;

    public float Cooldown => cooldown;
}
