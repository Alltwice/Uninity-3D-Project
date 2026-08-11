using UnityEngine;

[CreateAssetMenu(fileName = "PlayerDodgeConfig", menuName = "Player/Config/Dodge")]
public sealed class PlayerDodgeConfig : ScriptableObject
{
    [Header("闪避")]
    [Min(0.01f)] [SerializeField] private float duration = 0.6f;
    [Min(0f)] [SerializeField] private float distance = 5f;
    [Min(0f)] [SerializeField] private float cooldown = 0.35f;
    [SerializeField] private AnimationCurve distanceProgress = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float Duration => duration;
    public float Distance => distance;
    public float Cooldown => cooldown;
    public AnimationCurve DistanceProgress => distanceProgress;
}
