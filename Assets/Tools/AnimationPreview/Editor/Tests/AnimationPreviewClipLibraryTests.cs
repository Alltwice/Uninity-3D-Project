using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectTools.AnimationPreview.Tests
{
    public sealed class AnimationPreviewClipLibraryTests
    {
        [Test]
        public void Scan_FbxSource_ReturnsEmbeddedClipsWithoutPreviewClips()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FemaleMovementAnimsetPro/Animations/FemaleMovementAnimsetPro_1.fbx");
            Assert.That(source, Is.Not.Null);
            var results = AnimationPreviewClipLibrary.Scan(new Object[] { source }, false);
            Assert.That(results, Is.Not.Empty);
            Assert.That(results.All(entry => entry.Clip != null && !entry.Clip.name.StartsWith("__preview__")), Is.True);
        }

        [Test]
        public void PreviewSession_XBotAndHumanoidClip_CreatesPlayableGraph()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/X Bot.fbx");
            GameObject animationSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FemaleMovementAnimsetPro/Animations/FemaleMovementAnimsetPro_1.fbx");
            AnimationPreviewClipEntry entry = AnimationPreviewClipLibrary.Scan(new Object[] { animationSource }, false).First();
            using AnimationPreviewSession session = new AnimationPreviewSession();
            Assert.That(session.SetModel(model), Is.True, session.ModelError);
            Assert.That(session.SetClip(entry), Is.True, session.CompatibilityMessage);
            Assert.That(session.IsReady, Is.True);
        }
    }
}
