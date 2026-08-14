using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMotionProfile", menuName = "Player/Motion/Profile")]
public sealed class PlayerMotionProfile : ScriptableObject
{
    [Min(0f)]
    [SerializeField] private float duration;
    [SerializeField] private AnimationCurve cumulativeLocalX = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    [SerializeField] private AnimationCurve cumulativeLocalZ = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    [SerializeField] private AnimationCurve cumulativeTravelDistance = AnimationCurve.Linear(0f, 0f, 1f, 0f);

    public float Duration => duration;

    public Vector3 EvaluateLocalPosition(float normalizedTime)
    {
        float progress = Mathf.Clamp01(normalizedTime);
        return new Vector3(cumulativeLocalX.Evaluate(progress), 0f, cumulativeLocalZ.Evaluate(progress));
    }

    public float EvaluateTravelDistance(float normalizedTime)
    {
        return cumulativeTravelDistance.Evaluate(Mathf.Clamp01(normalizedTime));
    }
}
