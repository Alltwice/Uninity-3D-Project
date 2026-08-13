using System.Collections.Generic;
using UnityEngine;

namespace ProjectTools.AnimationPreview
{
    public enum AnimationPreviewRootMotionMode
    {
        Locked,
        Actual,
        Trajectory
    }

    [CreateAssetMenu(fileName = "AnimationPreviewProfile", menuName = "Animation/Preview Profile")]
    public sealed class AnimationPreviewProfile : ScriptableObject
    {
        [SerializeField] private GameObject modelAsset;
        [SerializeField] private List<Object> animationSources = new List<Object>();
        [SerializeField] private List<AnimationClip> favorites = new List<AnimationClip>();
        [SerializeField] private AnimationClip lastClip;
        [SerializeField] private Color backgroundColor = new Color(0.105f, 0.115f, 0.13f, 1f);
        [SerializeField, Range(0f, 5f)] private float lightIntensity = 1.2f;
        [SerializeField] private Vector2 lightRotation = new Vector2(35f, -35f);
        [SerializeField] private AnimationPreviewRootMotionMode rootMotionMode;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool showGrid = true;
        [SerializeField, Range(0.1f, 2f)] private float playbackSpeed = 1f;

        public GameObject ModelAsset => modelAsset;
        public IReadOnlyList<Object> AnimationSources => animationSources;
        public IReadOnlyList<AnimationClip> Favorites => favorites;
        public AnimationClip LastClip => lastClip;
        public Color BackgroundColor => backgroundColor;
        public float LightIntensity => lightIntensity;
        public Vector2 LightRotation => lightRotation;
        public AnimationPreviewRootMotionMode RootMotionMode => rootMotionMode;
        public bool Loop => loop;
        public bool ShowGrid => showGrid;
        public float PlaybackSpeed => playbackSpeed;

        internal void Store(GameObject model, IEnumerable<Object> sources, IEnumerable<AnimationClip> favoriteClips, AnimationClip clip, Color background, float intensity, Vector2 rotation, AnimationPreviewRootMotionMode motionMode, bool shouldLoop, bool shouldShowGrid, float speed)
        {
            modelAsset = model;
            animationSources.Clear();
            animationSources.AddRange(sources);
            favorites.Clear();
            favorites.AddRange(favoriteClips);
            lastClip = clip;
            backgroundColor = background;
            lightIntensity = intensity;
            lightRotation = rotation;
            rootMotionMode = motionMode;
            loop = shouldLoop;
            showGrid = shouldShowGrid;
            playbackSpeed = speed;
        }
    }
}
