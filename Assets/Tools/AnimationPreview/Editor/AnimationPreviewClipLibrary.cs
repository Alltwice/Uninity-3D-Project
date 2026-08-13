using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectTools.AnimationPreview
{
    internal sealed class AnimationPreviewClipEntry
    {
        public AnimationClip Clip { get; }
        public string AssetPath { get; }
        public string Group { get; }
        public ModelImporterAnimationType? ImportType { get; }

        public AnimationPreviewClipEntry(AnimationClip clip, string assetPath)
        {
            Clip = clip;
            AssetPath = assetPath;
            Group = Path.GetFileNameWithoutExtension(assetPath);
            ImportType = (AssetImporter.GetAtPath(assetPath) as ModelImporter)?.animationType;
        }
    }

    internal static class AnimationPreviewClipLibrary
    {
        public static List<AnimationPreviewClipEntry> Scan(IReadOnlyList<UnityEngine.Object> sources, bool scanAllAssets)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (scanAllAssets)
            {
                AddGuids(AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" }), paths);
            }
            foreach (UnityEngine.Object source in sources)
            {
                if (source == null) continue;
                string path = AssetDatabase.GetAssetPath(source);
                if (string.IsNullOrEmpty(path)) continue;
                if (AssetDatabase.IsValidFolder(path))
                {
                    AddGuids(AssetDatabase.FindAssets("t:AnimationClip", new[] { path }), paths);
                }
                else
                {
                    paths.Add(path);
                }
            }
            Dictionary<string, AnimationPreviewClipEntry> entries = new Dictionary<string, AnimationPreviewClipEntry>(StringComparer.Ordinal);
            foreach (string path in paths.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (!IsUsableClip(clip)) continue;
                    string key = AssetDatabase.AssetPathToGUID(path) + ":" + clip.GetInstanceID();
                    entries[key] = new AnimationPreviewClipEntry(clip, path);
                }
            }
            return entries.Values.OrderBy(entry => entry.Group, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Clip.name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static bool IsUsableClip(AnimationClip clip)
        {
            return clip != null && !clip.legacy && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasMatchingTransformBindings(AnimationClip clip, Transform animatorRoot, out string missingPath)
        {
            missingPath = null;
            IEnumerable<string> paths = AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)).Where(binding => binding.type == typeof(Transform) || typeof(Renderer).IsAssignableFrom(binding.type)).Select(binding => binding.path).Where(path => !string.IsNullOrEmpty(path)).Distinct(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                if (animatorRoot.Find(path) != null) continue;
                missingPath = path;
                return false;
            }
            return true;
        }

        public static bool CanUseAsAnimationSource(UnityEngine.Object asset)
        {
            if (asset == null || !EditorUtility.IsPersistent(asset)) return false;
            string path = AssetDatabase.GetAssetPath(asset);
            return asset is AnimationClip || AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Any(IsUsableClip);
        }

        private static void AddGuids(IEnumerable<string> guids, ISet<string> paths)
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
        }
    }

    internal static class AnimationPreviewAssetChangeTracker
    {
        public static event Action Changed;
        public static void NotifyChanged() => Changed?.Invoke();
    }

    internal sealed class AnimationPreviewAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (ContainsRelevantAsset(importedAssets) || ContainsRelevantAsset(deletedAssets) || ContainsRelevantAsset(movedAssets) || ContainsRelevantAsset(movedFromAssetPaths)) AnimationPreviewAssetChangeTracker.NotifyChanged();
        }

        private static bool ContainsRelevantAsset(IEnumerable<string> paths)
        {
            return paths.Any(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
        }
    }
}
