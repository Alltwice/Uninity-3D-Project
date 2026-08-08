using UnityEngine;

[CreateAssetMenu(fileName = "PlayerJumpConfig", menuName = "Player/Config/Jump")]
public sealed class PlayerJumpConfig : ScriptableObject
{
    [Header("跳跃设置")]
    [SerializeField] private float jumpHeight = 1.5f;

    public float JumpHeight => jumpHeight;
}
