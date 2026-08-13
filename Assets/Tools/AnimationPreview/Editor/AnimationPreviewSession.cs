using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace ProjectTools.AnimationPreview
{
    internal sealed class AnimationPreviewSession : IDisposable
    {
        private PreviewRenderUtility preview;
        private GameObject previewInstance;
        private GameObject gridObject;
        private Mesh gridMesh;
        private Material lineMaterial;
        private Animator animator;
        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        private AnimationClip clip;
        private Bounds modelBounds;
        private Vector3 modelOrigin;
        private Vector3 trajectoryEnd;
        private Quaternion modelRotation;
        private Vector2 orbit = new Vector2(135f, 12f);
        private Vector2 lightRotation = new Vector2(35f, -35f);
        private Vector3 pan;
        private float distance = 3f;
        private float lightIntensity = 1.2f;
        private double time;
        private bool playing;
        private bool loop = true;
        private float playbackSpeed = 1f;
        private Color backgroundColor = new Color(0.105f, 0.115f, 0.13f, 1f);
        private AnimationPreviewRootMotionMode rootMotionMode;

        public GameObject ModelAsset { get; private set; }
        public AnimationClip Clip => clip;
        public Animator Animator => animator;
        public bool IsPlaying => playing;
        public bool IsReady => animator != null && clip != null && graph.IsValid();
        public double Time => time;
        public double Length => clip == null ? 0d : clip.length;
        public Bounds ModelBounds => modelBounds;
        public int RendererCount { get; private set; }
        public int TransformCount { get; private set; }
        public int BoneCount { get; private set; }
        public string ModelError { get; private set; }
        public string CompatibilityMessage { get; private set; }
        public ModelImporterAnimationType? ModelImportType { get; private set; }

        public void Dispose()
        {
            DestroyGraph();
            DestroyGuideGeometry();
            if (preview != null)
            {
                preview.Cleanup();
                preview = null;
            }
            previewInstance = null;
            animator = null;
        }

        public bool SetModel(GameObject modelAsset)
        {
            ModelAsset = modelAsset;
            ModelError = null;
            CompatibilityMessage = null;
            DestroyGraph();
            DestroyGuideGeometry();
            if (preview != null) preview.Cleanup();
            preview = new PreviewRenderUtility();
            preview.cameraFieldOfView = 30f;
            ConfigureLights();
            previewInstance = null;
            animator = null;
            RendererCount = 0;
            TransformCount = 0;
            BoneCount = 0;
            ModelImportType = null;
            if (modelAsset == null)
            {
                ModelError = "请拖入一个模型 FBX 或 Prefab。";
                return false;
            }
            if (!EditorUtility.IsPersistent(modelAsset))
            {
                ModelError = "只接受 Project 中的模型资源，不接受场景实例。";
                return false;
            }
            string path = AssetDatabase.GetAssetPath(modelAsset);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null) ModelImportType = importer.animationType;
            previewInstance = preview.InstantiatePrefabInScene(modelAsset);
            if (previewInstance == null)
            {
                ModelError = "无法在预览场景中实例化该资源。";
                return false;
            }
            previewInstance.hideFlags = HideFlags.HideAndDontSave;
            StripNonPreviewComponents(previewInstance);
            Renderer[] renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
            RendererCount = renderers.Length;
            TransformCount = previewInstance.GetComponentsInChildren<Transform>(true).Length;
            if (RendererCount == 0)
            {
                ModelError = "该资源不包含可渲染的模型。";
                return false;
            }
            animator = previewInstance.GetComponentInChildren<Animator>(true);
            Avatar avatar = FindAvatar(path, animator);
            if (animator == null)
            {
                animator = previewInstance.AddComponent<Animator>();
                animator.avatar = avatar;
            }
            else if (animator.avatar == null)
            {
                animator.avatar = avatar;
            }
            if (animator.avatar == null)
            {
                ModelError = "模型没有 Avatar。请在 ModelImporter 的 Rig 页面配置动画类型和 Avatar。";
                return false;
            }
            if (!animator.avatar.isValid)
            {
                ModelError = "模型 Avatar 无效，请检查骨骼映射。";
                return false;
            }
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = true;
            BoneCount = animator.isHuman ? Enumerable.Range(0, (int)HumanBodyBones.LastBone).Count(index => animator.GetBoneTransform((HumanBodyBones)index) != null) : TransformCount;
            modelOrigin = previewInstance.transform.position;
            trajectoryEnd = modelOrigin;
            modelRotation = previewInstance.transform.rotation;
            modelBounds = CalculateBounds(renderers);
            Focus();
            return true;
        }

        public bool SetClip(AnimationPreviewClipEntry entry)
        {
            DestroyGraph();
            clip = entry?.Clip;
            CompatibilityMessage = null;
            time = 0d;
            playing = false;
            if (animator == null || clip == null) return false;
            if (ModelImportType == ModelImporterAnimationType.Generic && entry.ImportType == ModelImporterAnimationType.Generic && !AnimationPreviewClipLibrary.HasMatchingTransformBindings(clip, animator.transform, out string missingPath))
            {
                CompatibilityMessage = "Generic 骨架路径不匹配：" + missingPath;
                return false;
            }
            if (ModelImportType == ModelImporterAnimationType.Human && !animator.isHuman)
            {
                CompatibilityMessage = "模型标记为 Humanoid，但当前 Avatar 无法生成人形 Animator。";
                return false;
            }
            if (entry.ImportType == ModelImporterAnimationType.Human && !animator.isHuman)
            {
                CompatibilityMessage = "该动画需要有效的 Humanoid 模型。";
                return false;
            }
            graph = PlayableGraph.Create("Model Animation Preview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetApplyPlayableIK(false);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            output.SetSourcePlayable(clipPlayable);
            graph.Play();
            EvaluatePose();
            return true;
        }

        public void TogglePlayback()
        {
            if (IsReady) playing = !playing;
        }

        public void SetPlaying(bool value)
        {
            playing = value && IsReady;
        }

        public void ResetPlayback()
        {
            playing = false;
            SetTime(0d);
        }

        public void Step(int direction)
        {
            if (!IsReady) return;
            playing = false;
            double frameDuration = 1d / Math.Max(1d, clip.frameRate);
            SetTime(time + frameDuration * Math.Sign(direction));
        }

        public void SetTime(double value)
        {
            if (!IsReady) return;
            time = Math.Max(0d, Math.Min(Length, value));
            EvaluatePose();
        }

        public bool Update(double deltaTime)
        {
            if (!playing || !IsReady) return false;
            time += deltaTime * playbackSpeed;
            if (time >= Length)
            {
                if (loop && Length > 0d) time %= Length;
                else
                {
                    time = Length;
                    playing = false;
                }
            }
            EvaluatePose();
            return true;
        }

        public Texture Render(Rect rect, bool showGrid)
        {
            if (preview == null) return null;
            preview.BeginPreview(rect, GUIStyle.none);
            Camera camera = preview.camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Math.Max(100f, distance * 20f);
            Vector3 target = modelBounds.center + pan;
            Quaternion cameraRotation = Quaternion.Euler(-orbit.y, orbit.x, 0f);
            camera.transform.position = target + cameraRotation * new Vector3(0f, 0f, -distance);
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
            ConfigureLights();
            UpdateGuideGeometry(showGrid);
            preview.Render(true);
            return preview.EndPreview();
        }

        public void Orbit(Vector2 delta)
        {
            orbit.x += delta.x * 0.4f;
            orbit.y = Mathf.Clamp(orbit.y - delta.y * 0.3f, -85f, 85f);
        }

        public void Pan(Vector2 delta)
        {
            if (preview == null) return;
            float scale = distance * 0.0025f;
            pan += (-preview.camera.transform.right * delta.x + preview.camera.transform.up * delta.y) * scale;
        }

        public void Zoom(float delta)
        {
            distance = Mathf.Clamp(distance * Mathf.Exp(delta * 0.03f), 0.05f, 500f);
        }

        public void Focus()
        {
            pan = Vector3.zero;
            float radius = Math.Max(0.1f, modelBounds.extents.magnitude);
            distance = radius / Mathf.Tan(preview.cameraFieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
        }

        public void SetView(Vector2 viewOrbit)
        {
            orbit = viewOrbit;
            Focus();
        }

        public void Configure(Color background, float intensity, Vector2 rotation, bool shouldLoop, float speed, AnimationPreviewRootMotionMode motionMode)
        {
            backgroundColor = background;
            lightIntensity = intensity;
            lightRotation = rotation;
            loop = shouldLoop;
            playbackSpeed = speed;
            rootMotionMode = motionMode;
            ConfigureLights();
            EvaluatePose();
        }

        private void EvaluatePose()
        {
            if (!IsReady) return;
            if (rootMotionMode != AnimationPreviewRootMotionMode.Actual)
            {
                previewInstance.transform.SetPositionAndRotation(modelOrigin, modelRotation);
            }
            clipPlayable.SetTime(time);
            clipPlayable.SetDone(time >= Length);
            graph.Evaluate(0f);
            trajectoryEnd = previewInstance.transform.position;
            if (rootMotionMode != AnimationPreviewRootMotionMode.Actual) previewInstance.transform.SetPositionAndRotation(modelOrigin, modelRotation);
        }

        private void DestroyGraph()
        {
            if (graph.IsValid()) graph.Destroy();
            clipPlayable = default;
        }

        private void ConfigureLights()
        {
            if (preview?.lights == null || preview.lights.Length < 2) return;
            preview.lights[0].intensity = lightIntensity;
            preview.lights[0].transform.rotation = Quaternion.Euler(lightRotation.x, lightRotation.y, 0f);
            preview.lights[1].intensity = lightIntensity * 0.45f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 135f, 0f);
            preview.ambientColor = new Color(0.25f, 0.25f, 0.25f);
        }

        private void UpdateGuideGeometry(bool showGrid)
        {
            if (gridObject == null) CreateGuideGeometry();
            if (gridObject == null) return;
            gridObject.SetActive(showGrid || rootMotionMode == AnimationPreviewRootMotionMode.Trajectory);
            if (!gridObject.activeSelf) return;
            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();
            float height = modelBounds.min.y;
            float radius = Math.Max(1f, modelBounds.extents.magnitude);
            int lines = 10;
            float step = Math.Max(0.1f, radius / 5f);
            float extent = lines * step;
            if (showGrid)
            {
                for (int index = -lines; index <= lines; index++)
                {
                    float offset = index * step;
                    AddLine(vertices, indices, new Vector3(-extent, height, offset), new Vector3(extent, height, offset));
                    AddLine(vertices, indices, new Vector3(offset, height, -extent), new Vector3(offset, height, extent));
                }
            }
            if (rootMotionMode == AnimationPreviewRootMotionMode.Trajectory && previewInstance != null) AddLine(vertices, indices, modelOrigin, trajectoryEnd);
            gridMesh.Clear();
            gridMesh.SetVertices(vertices);
            gridMesh.SetIndices(indices, MeshTopology.Lines, 0);
            gridMesh.RecalculateBounds();
        }

        private void CreateGuideGeometry()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return;
            lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lineMaterial.SetColor("_Color", new Color(0.32f, 0.36f, 0.42f, 0.65f));
            gridMesh = new Mesh { name = "Animation Preview Guides", hideFlags = HideFlags.HideAndDontSave };
            gridObject = new GameObject("Animation Preview Guides") { hideFlags = HideFlags.HideAndDontSave };
            MeshFilter filter = gridObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = gridObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = gridMesh;
            renderer.sharedMaterial = lineMaterial;
            preview.AddSingleGO(gridObject);
        }

        private void DestroyGuideGeometry()
        {
            if (gridMesh != null) UnityEngine.Object.DestroyImmediate(gridMesh);
            if (lineMaterial != null) UnityEngine.Object.DestroyImmediate(lineMaterial);
            gridMesh = null;
            lineMaterial = null;
            gridObject = null;
        }

        private static void AddLine(ICollection<Vector3> vertices, ICollection<int> indices, Vector3 start, Vector3 end)
        {
            int index = vertices.Count;
            vertices.Add(start);
            vertices.Add(end);
            indices.Add(index);
            indices.Add(index + 1);
        }

        private static Avatar FindAvatar(string assetPath, Animator existingAnimator)
        {
            if (existingAnimator != null && existingAnimator.avatar != null) return existingAnimator.avatar;
            return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Avatar>().FirstOrDefault(avatar => avatar.isValid);
        }

        private static Bounds CalculateBounds(IReadOnlyList<Renderer> renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Count; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void StripNonPreviewComponents(GameObject root)
        {
            foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour is Animator) continue;
                UnityEngine.Object.DestroyImmediate(behaviour);
            }
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (Rigidbody rigidbody in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(rigidbody);
        }
    }
}
