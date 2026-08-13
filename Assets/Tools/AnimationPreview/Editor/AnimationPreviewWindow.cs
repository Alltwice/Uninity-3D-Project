using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectTools.AnimationPreview
{
    public sealed class AnimationPreviewWindow : EditorWindow
    {
        private const string UxmlPath = "Assets/Tools/AnimationPreview/Editor/UI/AnimationPreviewWindow.uxml";
        private const string UssPath = "Assets/Tools/AnimationPreview/Editor/UI/AnimationPreviewWindow.uss";
        private readonly List<UnityEngine.Object> animationSources = new List<UnityEngine.Object>();
        private readonly List<AnimationClip> favorites = new List<AnimationClip>();
        private readonly List<AnimationPreviewClipEntry> visibleClips = new List<AnimationPreviewClipEntry>();
        private AnimationPreviewSession session;
        private AnimationPreviewProfile profile;
        private List<AnimationPreviewClipEntry> clipLibrary = new List<AnimationPreviewClipEntry>();
        private ObjectField profileField;
        private ObjectField modelField;
        private ObjectField sourceField;
        private ListView sourceList;
        private ListView clipList;
        private ToolbarSearchField searchField;
        private Toggle scanAllToggle;
        private Toggle loopToggle;
        private Toggle gridToggle;
        private Slider speedSlider;
        private Slider timeSlider;
        private EnumField rootMotionField;
        private ColorField backgroundField;
        private Slider lightIntensitySlider;
        private Vector2Field lightRotationField;
        private Label timeLabel;
        private Label modelInfo;
        private Label clipInfo;
        private Label compatibilityInfo;
        private Label dirtyLabel;
        private AnimationPreviewViewport viewport;
        private Button playButton;
        private Button focusModeButton;
        private Button favoriteButton;
        private VisualElement leftPane;
        private VisualElement rightPane;
        private bool profileDirty;
        private bool libraryDirty = true;
        private bool focusMode;
        private double lastEditorTime;

        [MenuItem("Window/Animation/Model Animation Preview")]
        public static void Open()
        {
            AnimationPreviewWindow window = GetWindow<AnimationPreviewWindow>();
            window.titleContent = new GUIContent("Animation Preview");
            window.minSize = new Vector2(850f, 520f);
            window.Show();
        }

        internal static void Open(AnimationPreviewProfile previewProfile)
        {
            Open();
            AnimationPreviewWindow window = GetWindow<AnimationPreviewWindow>();
            window.LoadProfile(previewProfile);
        }

        private void OnEnable()
        {
            session = new AnimationPreviewSession();
            lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeSession;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AnimationPreviewAssetChangeTracker.Changed += OnAssetsChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeSession;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AnimationPreviewAssetChangeTracker.Changed -= OnAssetsChanged;
            DisposeSession();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (tree == null || style == null)
            {
                rootVisualElement.Add(new HelpBox("Animation Preview 的 UXML 或 USS 资源缺失。", HelpBoxMessageType.Error));
                return;
            }
            tree.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(style);
            CacheElements();
            ConfigureLists();
            RegisterCallbacks();
            BindViewport();
            if (profile != null) LoadProfile(profile);
            else
            {
                ApplySessionSettings();
                RefreshAllUI();
            }
        }

        private void CacheElements()
        {
            profileField = rootVisualElement.Q<ObjectField>("profile-field");
            modelField = rootVisualElement.Q<ObjectField>("model-field");
            sourceField = rootVisualElement.Q<ObjectField>("source-field");
            sourceList = rootVisualElement.Q<ListView>("source-list");
            clipList = rootVisualElement.Q<ListView>("clip-list");
            searchField = rootVisualElement.Q<ToolbarSearchField>("search-field");
            scanAllToggle = rootVisualElement.Q<Toggle>("scan-all-toggle");
            loopToggle = rootVisualElement.Q<Toggle>("loop-toggle");
            gridToggle = rootVisualElement.Q<Toggle>("grid-toggle");
            speedSlider = rootVisualElement.Q<Slider>("speed-slider");
            timeSlider = rootVisualElement.Q<Slider>("time-slider");
            rootMotionField = rootVisualElement.Q<EnumField>("root-motion-field");
            backgroundField = rootVisualElement.Q<ColorField>("background-field");
            lightIntensitySlider = rootVisualElement.Q<Slider>("light-intensity-slider");
            lightRotationField = rootVisualElement.Q<Vector2Field>("light-rotation-field");
            timeLabel = rootVisualElement.Q<Label>("time-label");
            modelInfo = rootVisualElement.Q<Label>("model-info");
            clipInfo = rootVisualElement.Q<Label>("clip-info");
            compatibilityInfo = rootVisualElement.Q<Label>("compatibility-info");
            dirtyLabel = rootVisualElement.Q<Label>("dirty-label");
            VisualElement viewportContainer = rootVisualElement.Q("preview-container");
            viewport = new AnimationPreviewViewport();
            viewport.AddToClassList("preview-viewport");
            viewportContainer.Add(viewport);
            playButton = rootVisualElement.Q<Button>("play-button");
            focusModeButton = rootVisualElement.Q<Button>("focus-mode-button");
            favoriteButton = rootVisualElement.Q<Button>("favorite-button");
            leftPane = rootVisualElement.Q("left-pane");
            rightPane = rootVisualElement.Q("right-pane");
            profileField.objectType = typeof(AnimationPreviewProfile);
            modelField.objectType = typeof(GameObject);
            modelField.allowSceneObjects = false;
            sourceField.objectType = typeof(UnityEngine.Object);
            sourceField.allowSceneObjects = false;
            rootMotionField.Init(AnimationPreviewRootMotionMode.Locked);
        }

        private void ConfigureLists()
        {
            sourceList.itemsSource = animationSources;
            sourceList.selectionType = SelectionType.Single;
            sourceList.makeItem = () => new Label();
            sourceList.bindItem = (element, index) => ((Label)element).text = animationSources[index] == null ? "<Missing>" : AssetDatabase.GetAssetPath(animationSources[index]);
            clipList.itemsSource = visibleClips;
            clipList.selectionType = SelectionType.Single;
            clipList.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            clipList.makeItem = () =>
            {
                VisualElement row = new VisualElement();
                row.AddToClassList("clip-row");
                row.Add(new Label { name = "clip-name" });
                row.Add(new Label { name = "clip-group" });
                return row;
            };
            clipList.bindItem = (element, index) =>
            {
                AnimationPreviewClipEntry entry = visibleClips[index];
                element.Q<Label>("clip-name").text = entry.Clip.name;
                element.Q<Label>("clip-group").text = entry.Group;
            };
            clipList.selectionChanged += selection => SelectClip(selection.OfType<AnimationPreviewClipEntry>().FirstOrDefault());
        }

        private void RegisterCallbacks()
        {
            profileField.RegisterValueChangedCallback(evt => LoadProfile(evt.newValue as AnimationPreviewProfile));
            modelField.RegisterValueChangedCallback(evt => SetModel(evt.newValue as GameObject));
            sourceField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == null) return;
                AddAnimationSource(evt.newValue);
                sourceField.SetValueWithoutNotify(null);
            });
            searchField.RegisterValueChangedCallback(_ => FilterClips());
            scanAllToggle.RegisterValueChangedCallback(_ => RefreshLibrary());
            loopToggle.RegisterValueChangedCallback(_ => OnSettingsChanged());
            gridToggle.RegisterValueChangedCallback(_ => OnSettingsChanged());
            speedSlider.RegisterValueChangedCallback(_ => OnSettingsChanged());
            rootMotionField.RegisterValueChangedCallback(_ => OnSettingsChanged());
            backgroundField.RegisterValueChangedCallback(_ => OnSettingsChanged());
            lightIntensitySlider.RegisterValueChangedCallback(_ => OnSettingsChanged());
            lightRotationField.RegisterValueChangedCallback(_ => OnSettingsChanged());
            timeSlider.RegisterValueChangedCallback(evt =>
            {
                if (session == null || !session.IsReady) return;
                session.SetTime(evt.newValue);
                UpdatePlaybackUI();
                viewport.MarkDirtyRepaint();
            });
            rootVisualElement.Q<Button>("remove-source-button").clicked += RemoveSelectedSource;
            rootVisualElement.Q<Button>("refresh-button").clicked += RefreshLibrary;
            rootVisualElement.Q<Button>("new-profile-button").clicked += CreateProfile;
            rootVisualElement.Q<Button>("save-profile-button").clicked += SaveProfile;
            playButton.clicked += () => { session.TogglePlayback(); UpdatePlaybackUI(); };
            rootVisualElement.Q<Button>("reset-button").clicked += () => { session.ResetPlayback(); UpdatePlaybackUI(); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("previous-frame-button").clicked += () => { session.Step(-1); UpdatePlaybackUI(); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("next-frame-button").clicked += () => { session.Step(1); UpdatePlaybackUI(); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("focus-button").clicked += () => { session.Focus(); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("front-view-button").clicked += () => { session.SetView(new Vector2(180f, 0f)); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("side-view-button").clicked += () => { session.SetView(new Vector2(90f, 0f)); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("back-view-button").clicked += () => { session.SetView(new Vector2(0f, 0f)); viewport.MarkDirtyRepaint(); };
            focusModeButton.clicked += ToggleFocusMode;
            favoriteButton.clicked += ToggleFavorite;
            RegisterSourceDropArea(rootVisualElement.Q("animation-drop-zone"));
        }

        private void BindViewport()
        {
            viewport.Render = rect => session.Render(rect, gridToggle.value);
            viewport.Orbit = session.Orbit;
            viewport.Pan = session.Pan;
            viewport.Zoom = session.Zoom;
            viewport.Focus = session.Focus;
            viewport.ModelDropped = SetModel;
        }

        private void SetModel(GameObject model)
        {
            EnsureSession();
            modelField.SetValueWithoutNotify(model);
            bool ready = session.SetModel(model);
            viewport.OverlayMessage = ready ? null : session.ModelError;
            if (ready && clipList.selectedItem is AnimationPreviewClipEntry selected) session.SetClip(selected);
            MarkProfileDirty();
            UpdateModelInfo();
            UpdateClipInfo();
            UpdateFavoriteButton();
            viewport.MarkDirtyRepaint();
        }

        private void SelectClip(AnimationPreviewClipEntry entry)
        {
            if (entry == null) return;
            bool ready = session.SetClip(entry);
            viewport.OverlayMessage = ready ? null : session.CompatibilityMessage ?? session.ModelError ?? "请先选择有效模型。";
            timeSlider.lowValue = 0f;
            timeSlider.highValue = Math.Max(0.001f, entry.Clip.length);
            timeSlider.SetValueWithoutNotify(0f);
            MarkProfileDirty();
            UpdateClipInfo();
            UpdatePlaybackUI();
            viewport.MarkDirtyRepaint();
        }

        private void AddAnimationSource(UnityEngine.Object source)
        {
            if (!AnimationPreviewClipLibrary.CanUseAsAnimationSource(source))
            {
                ShowNotification(new GUIContent("该资源不包含可用 AnimationClip。"));
                return;
            }
            if (animationSources.Contains(source)) return;
            animationSources.Add(source);
            sourceList.Rebuild();
            MarkProfileDirty();
            RefreshLibrary();
        }

        private void RefreshLibrary()
        {
            libraryDirty = false;
            clipLibrary = AnimationPreviewClipLibrary.Scan(animationSources, scanAllToggle.value);
            FilterClips();
        }

        private void FilterClips()
        {
            string query = searchField?.value?.Trim();
            visibleClips.Clear();
            visibleClips.AddRange(string.IsNullOrEmpty(query) ? clipLibrary : clipLibrary.Where(entry => entry.Clip.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || entry.Group.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || entry.AssetPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
            clipList?.Rebuild();
            rootVisualElement.Q<Label>("clip-count-label").text = $"{visibleClips.Count} Clips";
        }

        private void ApplySessionSettings()
        {
            if (session == null || loopToggle == null) return;
            session.Configure(backgroundField.value, lightIntensitySlider.value, lightRotationField.value, loopToggle.value, speedSlider.value, (AnimationPreviewRootMotionMode)rootMotionField.value);
            viewport?.MarkDirtyRepaint();
        }

        private void OnSettingsChanged()
        {
            ApplySessionSettings();
            MarkProfileDirty();
        }

        private void LoadProfile(AnimationPreviewProfile value)
        {
            if (loopToggle == null)
            {
                profile = value;
                return;
            }
            profile = value;
            if (profileField != null) profileField.SetValueWithoutNotify(value);
            animationSources.Clear();
            favorites.Clear();
            if (value != null)
            {
                animationSources.AddRange(value.AnimationSources.Where(source => source != null));
                favorites.AddRange(value.Favorites.Where(clip => clip != null));
                loopToggle.SetValueWithoutNotify(value.Loop);
                gridToggle.SetValueWithoutNotify(value.ShowGrid);
                speedSlider.SetValueWithoutNotify(value.PlaybackSpeed);
                rootMotionField.SetValueWithoutNotify(value.RootMotionMode);
                backgroundField.SetValueWithoutNotify(value.BackgroundColor);
                lightIntensitySlider.SetValueWithoutNotify(value.LightIntensity);
                lightRotationField.SetValueWithoutNotify(value.LightRotation);
            }
            sourceList?.Rebuild();
            SetModel(value?.ModelAsset);
            profileDirty = false;
            UpdateDirtyLabel();
            ApplySessionSettings();
            RefreshLibrary();
            if (value?.LastClip != null)
            {
                int index = visibleClips.FindIndex(entry => entry.Clip == value.LastClip);
                if (index >= 0) clipList.SetSelection(index);
            }
        }

        private void SaveProfile()
        {
            if (profile == null)
            {
                CreateProfile();
                if (profile == null) return;
            }
            profile.Store(modelField.value as GameObject, animationSources, favorites, session.Clip, backgroundField.value, lightIntensitySlider.value, lightRotationField.value, (AnimationPreviewRootMotionMode)rootMotionField.value, loopToggle.value, gridToggle.value, speedSlider.value);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            profileDirty = false;
            UpdateDirtyLabel();
        }

        private void CreateProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Animation Preview Profile", "AnimationPreviewProfile", "asset", "选择配置资源保存位置");
            if (string.IsNullOrEmpty(path)) return;
            AnimationPreviewProfile created = CreateInstance<AnimationPreviewProfile>();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            profile = created;
            profileField.SetValueWithoutNotify(created);
            MarkProfileDirty();
            SaveProfile();
            Selection.activeObject = created;
        }

        private void RemoveSelectedSource()
        {
            int index = sourceList.selectedIndex;
            if (index < 0 || index >= animationSources.Count) return;
            animationSources.RemoveAt(index);
            sourceList.Rebuild();
            MarkProfileDirty();
            RefreshLibrary();
        }

        private void RegisterSourceDropArea(VisualElement dropZone)
        {
            dropZone.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDrop.objectReferences.Any(AnimationPreviewClipLibrary.CanUseAsAnimationSource) ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                evt.StopPropagation();
            });
            dropZone.RegisterCallback<DragPerformEvent>(evt =>
            {
                UnityEngine.Object[] sources = DragAndDrop.objectReferences.Where(AnimationPreviewClipLibrary.CanUseAsAnimationSource).ToArray();
                if (sources.Length == 0) return;
                DragAndDrop.AcceptDrag();
                foreach (UnityEngine.Object source in sources) AddAnimationSource(source);
                evt.StopPropagation();
            });
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            double deltaTime = Math.Min(0.1d, now - lastEditorTime);
            lastEditorTime = now;
            if (libraryDirty && rootVisualElement.panel != null) RefreshLibrary();
            if (session != null && session.Update(deltaTime))
            {
                UpdatePlaybackUI();
                viewport?.MarkDirtyRepaint();
            }
        }

        private void OnAssetsChanged()
        {
            libraryDirty = true;
            rootVisualElement.Q<Label>("clip-count-label").text = "资源已变化，等待刷新";
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) DisposeSession();
            else if (change == PlayModeStateChange.EnteredEditMode && session == null)
            {
                session = new AnimationPreviewSession();
                SetModel(modelField?.value as GameObject);
                BindViewport();
            }
        }

        private void ToggleFocusMode()
        {
            focusMode = !focusMode;
            leftPane.style.display = focusMode ? DisplayStyle.None : DisplayStyle.Flex;
            rightPane.style.display = focusMode ? DisplayStyle.None : DisplayStyle.Flex;
            focusModeButton.text = focusMode ? "退出专注" : "专注预览";
            viewport.MarkDirtyRepaint();
        }

        private void RefreshAllUI()
        {
            sourceList.Rebuild();
            if (libraryDirty) RefreshLibrary();
            UpdateModelInfo();
            UpdateClipInfo();
            UpdatePlaybackUI();
            UpdateFavoriteButton();
            UpdateDirtyLabel();
        }

        private void UpdateModelInfo()
        {
            if (session == null || session.ModelAsset == null)
            {
                modelInfo.text = "未选择模型";
                return;
            }
            string importType = session.ModelImportType?.ToString() ?? "Prefab";
            string avatar = session.Animator?.avatar == null ? "缺失" : session.Animator.avatar.isValid ? session.Animator.isHuman ? "有效 Humanoid" : "有效 Generic" : "无效";
            modelInfo.text = $"模型：{session.ModelAsset.name}\n导入类型：{importType}\nAvatar：{avatar}\nRenderer：{session.RendererCount}\n骨骼：{session.BoneCount}";
            compatibilityInfo.text = session.ModelError ?? session.CompatibilityMessage ?? "模型可以预览";
            compatibilityInfo.EnableInClassList("status-error", session.ModelError != null || session.CompatibilityMessage != null);
        }

        private void UpdateClipInfo()
        {
            AnimationClip selected = session?.Clip;
            if (selected == null)
            {
                clipInfo.text = "未选择动画";
                return;
            }
            string path = AssetDatabase.GetAssetPath(selected);
            clipInfo.text = $"动画：{selected.name}\n来源：{Path.GetFileName(path)}\n长度：{selected.length:F3}s\n帧率：{selected.frameRate:F1}\n循环：{selected.isLooping}\nRoot Curves：{selected.hasRootCurves}";
            compatibilityInfo.text = session.CompatibilityMessage ?? "动画与当前模型兼容";
            compatibilityInfo.EnableInClassList("status-error", session.CompatibilityMessage != null);
        }

        private void UpdatePlaybackUI()
        {
            if (session == null || timeSlider == null) return;
            timeSlider.SetValueWithoutNotify((float)session.Time);
            timeLabel.text = $"{session.Time:F2} / {session.Length:F2}";
            playButton.text = session.IsPlaying ? "暂停" : "播放";
            playButton.SetEnabled(session.IsReady);
        }

        private void MarkProfileDirty()
        {
            profileDirty = true;
            UpdateDirtyLabel();
        }

        private void UpdateDirtyLabel()
        {
            if (dirtyLabel == null) return;
            dirtyLabel.text = profileDirty ? "会话有未保存更改" : profile == null ? "临时会话" : "已保存";
        }

        private void DisposeSession()
        {
            session?.Dispose();
            session = null;
        }

        private void ToggleFavorite()
        {
            AnimationClip selected = session?.Clip;
            if (selected == null) return;
            if (favorites.Contains(selected)) favorites.Remove(selected);
            else favorites.Add(selected);
            MarkProfileDirty();
            UpdateFavoriteButton();
        }

        private void UpdateFavoriteButton()
        {
            if (favoriteButton == null) return;
            AnimationClip selected = session?.Clip;
            favoriteButton.SetEnabled(selected != null);
            favoriteButton.text = selected != null && favorites.Contains(selected) ? "取消收藏" : "收藏";
        }

        private void EnsureSession()
        {
            if (session != null) return;
            session = new AnimationPreviewSession();
            lastEditorTime = EditorApplication.timeSinceStartup;
            if (viewport != null) BindViewport();
        }
    }
}
