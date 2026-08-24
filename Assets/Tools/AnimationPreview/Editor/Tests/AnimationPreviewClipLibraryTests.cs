using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectTools.AnimationPreview.Tests
{
    public sealed class AnimationPreviewClipLibraryTests
    {
        private const string IdleToRunSequencePath = "Assets/Tools/AnimationPreview/Editor/Tests/IdleToRunTest Sequence.asset";
        private const string PlayerIdleAnimationPath = "Assets/Animation/Player/Idle/Idle.fbx";
        private string temporarySequencePath;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(temporarySequencePath)) AssetDatabase.DeleteAsset(temporarySequencePath);
            temporarySequencePath = null;
        }

        [Test]
        public void Scan_FbxSource_ReturnsEmbeddedClipsWithoutPreviewClips()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerIdleAnimationPath);
            Assert.That(source, Is.Not.Null);
            var results = AnimationPreviewClipLibrary.Scan(new Object[] { source }, false);
            Assert.That(results, Is.Not.Empty);
            Assert.That(results.All(entry => entry.Clip != null && !entry.Clip.name.StartsWith("__preview__")), Is.True);
        }

        [Test]
        public void PreviewSession_XBotAndHumanoidClip_CreatesPlayableGraph()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/X Bot.fbx");
            GameObject animationSource = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerIdleAnimationPath);
            AnimationPreviewClipEntry entry = AnimationPreviewClipLibrary.Scan(new Object[] { animationSource }, false).First();
            using AnimationPreviewSession session = new AnimationPreviewSession();
            Assert.That(session.SetModel(model), Is.True, session.ModelError);
            Assert.That(session.SetClip(entry), Is.True, session.CompatibilityMessage);
            Assert.That(session.IsReady, Is.True);
        }

        [Test]
        public void PreviewSession_IdleToRunTestSequence_CreatesMixerBlendsAndReleasesGraph()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/X Bot.fbx");
            GameObject idleSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FemaleRunnerAnimset/Animations/Movements/Idle/Idle.fbx");
            GameObject startSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FemaleRunnerAnimset/Animations/Movements/Run/Run_Start_R0.fbx");
            GameObject locomotionSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FemaleRunnerAnimset/Animations/Movements/Run/Run_Rfoot.fbx");
            AnimationClip idle = LoadUsableClip("Assets/FemaleRunnerAnimset/Animations/Movements/Idle/Idle.fbx");
            AnimationClip idleToRun = LoadUsableClip("Assets/FemaleRunnerAnimset/Animations/Movements/Run/Run_Start_R0.fbx");
            AnimationClip groundLocomotion = LoadUsableClip("Assets/FemaleRunnerAnimset/Animations/Movements/Run/Run_Rfoot.fbx");
            Assert.That(idleSource, Is.Not.Null);
            Assert.That(startSource, Is.Not.Null);
            Assert.That(locomotionSource, Is.Not.Null);
            Assert.That(idle, Is.Not.Null);
            Assert.That(idleToRun, Is.Not.Null);
            Assert.That(groundLocomotion, Is.Not.Null);
            AnimationPreviewSequence sequence = ScriptableObject.CreateInstance<AnimationPreviewSequence>();
            sequence.name = "IdleToRunTest Sequence";
            sequence.SetEntries(new[]
            {
                new AnimationPreviewSequenceEntry(idleSource, idle, 0f, 1f, 0f),
                new AnimationPreviewSequenceEntry(startSource, idleToRun, 1f, 0.8f, 0.2f),
                new AnimationPreviewSequenceEntry(locomotionSource, groundLocomotion, 1.8f, float.PositiveInfinity, 0.2f)
            });
            temporarySequencePath = AssetDatabase.GenerateUniqueAssetPath(IdleToRunSequencePath);
            AssetDatabase.CreateAsset(sequence, temporarySequencePath);
            AssetDatabase.SaveAssetIfDirty(sequence);
            AnimationPreviewSession session = new AnimationPreviewSession();
            try
            {
                Assert.That(session.SetModel(model), Is.True, session.ModelError);
                Assert.That(session.SetSequence(sequence), Is.True, session.CompatibilityMessage);
                Assert.That(session.IsSequence, Is.True);
                Assert.That(session.SequenceInputCount, Is.EqualTo(3));
                Assert.That(session.HasFiniteLength, Is.False);
                session.SetTime(1.5d);
                Assert.That(session.GetSequenceInputWeight(0), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(session.GetSequenceInputWeight(1), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(session.GetSequenceInputWeight(2), Is.EqualTo(0f).Within(0.0001f));
                session.SetTime(1.7d);
                Assert.That(session.GetSequenceInputWeight(1), Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(session.GetSequenceInputWeight(2), Is.EqualTo(0.5f).Within(0.01f));
                Animator animator = session.Animator;
                session.Configure(new Color(0.105f, 0.115f, 0.13f, 1f), 1.2f, new Vector2(35f, -35f), true, 1f, AnimationPreviewRootMotionMode.Actual);
                Assert.DoesNotThrow(() =>
                {
                    session.SetPlaying(true);
                    session.Update(0.1d);
                });
                Assert.That(session.Animator, Is.SameAs(animator));
            }
            finally
            {
                session.Dispose();
            }
            Assert.That(session.IsReady, Is.False);
            Assert.That(session.SequenceInputCount, Is.EqualTo(0));
        }

        [Test]
        public void WindowLayout_ContainsSequenceControls()
        {
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Tools/AnimationPreview/Editor/UI/AnimationPreviewWindow.uxml");
            Assert.That(tree, Is.Not.Null);
            VisualElement root = tree.Instantiate();
            Assert.That(root.Q("mode-container"), Is.Not.Null);
            Assert.That(root.Q<ObjectField>("sequence-field"), Is.Not.Null);
            Assert.That(root.Q<ListView>("sequence-entry-list"), Is.Not.Null);
            Assert.That(root.Q<Button>("stop-button"), Is.Not.Null);
        }

        private static AnimationClip LoadUsableClip(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().FirstOrDefault(AnimationPreviewClipLibrary.IsUsableClip);
        }
    }
}
