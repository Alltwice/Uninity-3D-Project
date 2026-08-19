using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerMotionId
{
    IdleToWalk,
    WalkToIdle,
    IdleToRun,
    RunToIdle,
    FastRunToIdle,
    WalkStart180Left,
    WalkStart180Right,
    RunStart180Left,
    RunStart180Right,
    WalkTurn180Left,
    WalkTurn180Right,
    RunTurn180Left,
    RunTurn180Right,
    FastRunTurn180Left,
    FastRunTurn180Right,
    Dodge
}
/// <summary>
/// 组织每一份动画数据
/// </summary>
[Serializable]
public struct PlayerMotionCatalogEntry
{
    [SerializeField] private PlayerMotionId id;
    [SerializeField] private PlayerMotionDefinition definition;

    public PlayerMotionCatalogEntry(PlayerMotionId motionId, PlayerMotionDefinition motionDefinition)
    {
        id = motionId;
        definition = motionDefinition;
    }

    public PlayerMotionId Id => id;
    public PlayerMotionDefinition Definition => definition;
}

[CreateAssetMenu(fileName = "PlayerMotionCatalog", menuName = "Player/Motion/Catalog")]
public class PlayerMotionCatalog : ScriptableObject
{
    [SerializeField] private List<PlayerMotionCatalogEntry> motions = new List<PlayerMotionCatalogEntry>();
    [Range(90f, 180f)] [SerializeField] private float turn180Threshold = 150f;
    [Range(0f, 180f)] [SerializeField] private float turnIntentTolerance = 30f;
    [Range(0f, 180f)] [SerializeField] private float turnRotationUnlockAngle = 120f;

    public IReadOnlyList<PlayerMotionCatalogEntry> Motions => motions;
    public float Turn180Threshold => turn180Threshold;
    public float TurnIntentTolerance => turnIntentTolerance;
    public float TurnRotationUnlockAngle => turnRotationUnlockAngle;

    public bool TryGet(PlayerMotionId id, out PlayerMotionDefinition definition)
    {
        for (int i = 0; i < motions.Count; i++)
        {
            if (motions[i].Id != id) continue;
            definition = motions[i].Definition;
            return definition != null;
        }
        definition = null;
        return false;
    }

#if UNITY_EDITOR
    public void Configure(IEnumerable<PlayerMotionCatalogEntry> entries, float turnThreshold, float intentTolerance, float rotationUnlockAngle)
    {
        motions.Clear();
        motions.AddRange(entries);
        turn180Threshold = turnThreshold;
        turnIntentTolerance = intentTolerance;
        turnRotationUnlockAngle = rotationUnlockAngle;
    }
#endif
}
