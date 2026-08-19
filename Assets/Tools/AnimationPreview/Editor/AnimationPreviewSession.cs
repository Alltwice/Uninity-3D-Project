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
    /// <summary>
    /// 动画预览采样数据，当继承了IDisposable意味着必须实现Dispose()，清理资源滞空引用
    /// </summary>
    internal class AnimationPreviewSession : IDisposable
    {
        //独立渲染预览工具
        private PreviewRenderUtility preview;
        //模型实例
        private GameObject previewInstance;
        private GameObject gridObject;
        //底侧的辅助绘制
        private Mesh gridMesh;
        //配合mesh绘制地面
        private Material lineMaterial;
        //动画播放支持
        private Animator animator;
        //动画播放管线，比AnimatorController更底层的动画组织方式
        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        private AnimationClip clip;
        //创建一个盒子，表示空间信息
        private Bounds modelBounds;
        private Vector3 modelOrigin;
        private Vector3 trajectoryEnd;
        //预览窗口的坐标点
        private readonly List<Vector3> trajectoryPoints = new List<Vector3>();
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
        /// <summary>
        /// 清理资源，当使用using字样时异常或正常结束均会触发或被手动调用
        /// </summary>
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
        /// <summary>
        /// 设定模型
        /// </summary>
        public bool SetModel(GameObject modelAsset)
        {
            ModelAsset = modelAsset;
            ModelError = null;
            CompatibilityMessage = null;
            //清理场地
            DestroyGraph();
            DestroyGuideGeometry();
            if (preview != null) preview.Cleanup();
            preview = new PreviewRenderUtility();
            //摄像机视野角度
            preview.cameraFieldOfView = 30f;
            ConfigureLights();
            //重置模型状态
            previewInstance = null;
            animator = null;
            RendererCount = 0;
            TransformCount = 0;
            BoneCount = 0;
            ModelImportType = null;
            if (modelAsset == null)
            {
                ModelError = "请拖入一个模型 FBX 或 Prefab";
                return false;
            }
            //检查是否时持久化的Asset
            if (!EditorUtility.IsPersistent(modelAsset))
            {
                ModelError = "只接受 Project 中的模型资源，不接受场景实例";
                return false;
            }
            //拿到文件路径
            string path = AssetDatabase.GetAssetPath(modelAsset);
            //外部文件导入时会存在Importer，这里获取文件并尝试将其转为ModelImporter
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            //模型类型Legacy，Humanoid等
            if (importer != null) ModelImportType = importer.animationType;
            previewInstance = preview.InstantiatePrefabInScene(modelAsset);
            if (previewInstance == null)
            {
                ModelError = "无法在预览场景中实例化该资源";
                return false;
            }
            //控制对对象的行为，HideAndDontSave表示不显示也不保存为临时对象
            previewInstance.hideFlags = HideFlags.HideAndDontSave;
            StripNonPreviewComponents(previewInstance);
            //true表示能拿到失活节点，统计渲染数和节点数
            Renderer[] renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
            RendererCount = renderers.Length;
            TransformCount = previewInstance.GetComponentsInChildren<Transform>(true).Length;
            if (RendererCount == 0)
            {
                ModelError = "该资源不包含可渲染的模型";
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
                ModelError = "模型没有 Avatar请在 ModelImporter 的 Rig 页面配置动画类型和 Avatar";
                return false;
            }
            if (!animator.avatar.isValid)
            {
                ModelError = "模型 Avatar 无效，请检查骨骼映射";
                return false;
            }
            //不使用AnimatorController
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = true;
            //统计骨骼数量
            BoneCount = animator.isHuman ? Enumerable.Range(0, (int)HumanBodyBones.LastBone).Count(index => animator.GetBoneTransform((HumanBodyBones)index) != null) : TransformCount;
            modelOrigin = previewInstance.transform.position;
            trajectoryEnd = modelOrigin;
            modelRotation = previewInstance.transform.rotation;
            //整个角色的总骨骼
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
                CompatibilityMessage = "模型标记为 Humanoid，但当前 Avatar 无法生成人形 Animator";
                return false;
            }
            if (entry.ImportType == ModelImporterAnimationType.Human && !animator.isHuman)
            {
                CompatibilityMessage = "该动画需要有效的 Humanoid 模型";
                return false;
            }
            graph = PlayableGraph.Create("Model Animation Preview");
            //设置时间推进模式为手动
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            //放动画节点，关闭IK
            clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetApplyPlayableIK(false);
            //PlayableGraph的动画输出端口
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            //设置了数据来源
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
        /// <summary>
        /// 正真的烘焙算法
        /// </summary>
        internal PlayerMotionBakeResult SampleMotion(int requestedSampleRate)
        {
            if (!IsReady) throw new InvalidOperationException("请先选择有效 Model/Avatar 和 AnimationClip");
            int rate = Math.Max(1, requestedSampleRate);
            //被采样的数量，+1意味着算上起始点，CeilToInt向上取整，确保动画非整时也能取得精确的开始点和结束点从而控制取样间隔均等
            int count = Math.Max(2, Mathf.CeilToInt(clip.length * rate) + 1);
            //XZ
            Vector2[] positions = new Vector2[count];
            //距离
            float[] distances = new float[count];
            //角度
            float[] yaws = new float[count];
            //给预览窗口用的，bake后能保持原行为
            double previousSessionTime = time;
            bool wasPlaying = playing;
            playing = false;
            previewInstance.transform.SetPositionAndRotation(modelOrigin, modelRotation);
            //设定动画时间为0f
            clipPlayable.SetTime(0d);
            clipPlayable.SetDone(false);
            //正真执行动画时间为0f
            graph.Evaluate(0f);
            //变化量置0
            Vector3 accumulatedPosition = Vector3.zero;
            float accumulatedYaw = 0f;
            double previousSampleTime = 0d;
            trajectoryPoints.Clear();
            trajectoryPoints.Add(modelOrigin);
            for (int i = 1; i < count; i++)
            {
                //每一份动画长度除以采样点从而平均铺满整个动画
                double sampleTime = clip.length * i / (count - 1);
                //真正前进每次推进的时间
                graph.Evaluate((float)(sampleTime - previousSampleTime));
                //烘焙不依赖模型朝向，用于撤销模型的初始旋转
                Vector3 localDelta = Quaternion.Inverse(modelRotation) * animator.deltaPosition;
                //去除y轴分量影响投影到xz平面上
                accumulatedPosition += Vector3.ProjectOnPlane(localDelta, Vector3.up);
                //四元数转为欧拉角并用DeltaAngle始终记录有符号的最小值（350°和-10°取后者）
                accumulatedYaw += Mathf.DeltaAngle(0f, animator.deltaRotation.eulerAngles.y);
                positions[i] = new Vector2(accumulatedPosition.x, accumulatedPosition.z);
                //Vector3.ProjectOnPlane将三维向量投影到法线平面上并开方以计算距离
                distances[i] = distances[i - 1] + Vector3.ProjectOnPlane(localDelta, Vector3.up).magnitude;
                yaws[i] = accumulatedYaw;
                //这里不是乘法，四元数*Vector3代表将Vector3向四元数方向旋转
                trajectoryPoints.Add(modelOrigin + modelRotation * accumulatedPosition);
                previousSampleTime = sampleTime;
            }
            //记录轨迹终点
            trajectoryEnd = trajectoryPoints[trajectoryPoints.Count - 1];
            //恢复模型到起点
            previewInstance.transform.SetPositionAndRotation(modelOrigin, modelRotation);
            //回复之前时间
            time = Math.Max(0d, Math.Min(Length, previousSessionTime));
            clipPlayable.SetTime(time);
            clipPlayable.SetDone(time >= Length);
            graph.Evaluate(0f);
            if (rootMotionMode != AnimationPreviewRootMotionMode.Actual) previewInstance.transform.SetPositionAndRotation(modelOrigin, modelRotation);
            playing = wasPlaying;
            return new PlayerMotionBakeResult(clip.length, rate, positions, distances, yaws);
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
        /// <summary>
        /// 设定距离
        /// </summary>
        public void Focus()
        {
            pan = Vector3.zero;
            //计算从中心到一个盒子角落的距离，也就是让相机在一个外接圆的范围内移动
            float radius = Math.Max(0.1f, modelBounds.extents.magnitude);
            //tan为正切，此刻在求三角的邻边，Deg2Rad三角函数单位弧度
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
        /// <summary>
        /// 将动画设定为0秒位置
        /// </summary>
        private void EvaluatePose()
        {
            if (!IsReady) return;
            //不实际移动时设定其到原位置
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
        /// <summary>
        /// 设置灯光
        /// </summary>
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
            if (rootMotionMode == AnimationPreviewRootMotionMode.Trajectory && previewInstance != null)
            {
                if (trajectoryPoints.Count > 1)
                {
                    for (int i = 1; i < trajectoryPoints.Count; i++) AddLine(vertices, indices, trajectoryPoints[i - 1], trajectoryPoints[i]);
                }
                else AddLine(vertices, indices, modelOrigin, trajectoryEnd);
            }
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
        /// <summary>
        /// 拿Avatar，优先模型的
        /// </summary>
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
        //剥除所有组件，保留Animator
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
