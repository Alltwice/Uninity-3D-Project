using UnityEngine;

[CreateAssetMenu(fileName = "PlayerActionBufferConfig", menuName = "Player/Config/Action Buffer")]
public sealed class PlayerActionBufferConfig : ScriptableObject
{
    [Header("输入缓冲")]
    [SerializeField] private float jumpBufferTime = 0.12f;
    [SerializeField] private float dodgeBufferTime = 0.12f;

    public float JumpBufferTime => jumpBufferTime;
    public float DodgeBufferTime => dodgeBufferTime;
}
