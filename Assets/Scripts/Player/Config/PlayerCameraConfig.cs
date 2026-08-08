using UnityEngine;

[CreateAssetMenu(fileName = "PlayerCameraConfig", menuName = "Player/Config/Camera")]
public sealed class PlayerCameraConfig : ScriptableObject
{
    [Header("鼠标灵敏度")]
    [SerializeField] private float mouseSensitivity = 0.12f;

    [Header("俯仰角")]
    [SerializeField] private float minPitch = -50f;
    [SerializeField] private float maxPitch = 60f;

    [Header("镜头距离")]
    [Min(0f)] [SerializeField] private float normalDistance = 4f;
    [Min(0f)] [SerializeField] private float topViewDistance = 6.5f;
    [Min(0f)] [SerializeField] private float distanceSmoothSpeed = 8f;
    [SerializeField] private float beginTopViewPitch = 10f;

    public float MouseSensitivity => mouseSensitivity;
    public float MinPitch => minPitch;
    public float MaxPitch => maxPitch;
    public float NormalDistance => normalDistance;
    public float TopViewDistance => topViewDistance;
    public float DistanceSmoothSpeed => distanceSmoothSpeed;
    public float BeginTopViewPitch => beginTopViewPitch;
}
