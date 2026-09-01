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
        private enum PreviewMode
        {
            SingleClip,
            Sequence
        }

        private const string UxmlPath = "Assets/Tools/AnimationPreview/Editor/UI/AnimationPreviewWindow.uxml";
        private const string UssPath = "Assets/Tools/AnimationPreview/Editor/UI/AnimationPreviewWindow.uss";
        private readonly List<UnityEngine.Object> animationSources = new List<UnityEngine.Object>();
        private readonly List<AnimationClip> favorites = new List<AnimationClip>();
        private readonly List<AnimationPreviewClipEntry> visibleClips = new List<AnimationPreviewClipEntry>();
        private readonly List<AnimationPreviewSequenceEntry> visibleSequenceEntries = new List<AnimationPreviewSequenceEntry>();
        private AnimationPreviewSession session;
        private AnimationPreviewProfile profile;
        private List<AnimationPreviewClipEntry> clipLibrary = new List<AnimationPreviewClipEntry>();
        private ObjectField profileField;
        private ObjectField modelField;
        private ObjectField sourceField;
        private ObjectField sequenceField;
        private ObjectField motionProfileField;
        private ObjectField footCalibrationField;
        private IntegerField motionSampleRateField;
        private EnumField footPlantDetectionModeField;
        private EnumField plantMarkerModeField;
        private Label motionValidationLabel;
        private Vector3Field leftFootOffsetField;
        private Vector3Field rightFootOffsetField;
        private FloatField virtualGroundField;
        private Toggle footProbesToggle;
        private Button markLeftPlantButton;
        private Button markRightPlantButton;
        private Button deletePlantButton;
        private ListView sourceList;
        private ListView clipList;
        private ListView sequenceEntryList;
        private ToolbarSearchField searchField;
        private Toggle scanAllToggle;
        private Toggle loopToggle;
        private Toggle gridToggle;
        private Slider speedSlider;
        private Slider timeSlider;
        private VisualElement plantMarkerTrack;
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
        private Button stopButton;
        private Button focusModeButton;
        private Button favoriteButton;
        private VisualElement leftPane;
        private VisualElement rightPane;
        private VisualElement sequencePanel;
        private RadioButtonGroup modeField;
        private PreviewMode previewMode;
        private bool profileDirty;
        private bool libraryDirty = true;
        private bool focusMode;
        private double lastEditorTime;
        private bool applyingFootCalibration;
        private AnimationClip motionSettingsClip;

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
            sequenceField = rootVisualElement.Q<ObjectField>("sequence-field");
            motionProfileField = rootVisualElement.Q<ObjectField>("motion-profile-field");
            footCalibrationField = rootVisualElement.Q<ObjectField>("foot-calibration-field");
            motionSampleRateField = rootVisualElement.Q<IntegerField>("motion-sample-rate-field");
            footPlantDetectionModeField = rootVisualElement.Q<EnumField>("foot-plant-detection-mode-field");
            plantMarkerModeField = rootVisualElement.Q<EnumField>("plant-marker-mode-field");
            motionValidationLabel = rootVisualElement.Q<Label>("motion-validation-label");
            leftFootOffsetField = rootVisualElement.Q<Vector3Field>("left-foot-offset-field");
            rightFootOffsetField = rootVisualElement.Q<Vector3Field>("right-foot-offset-field");
            virtualGroundField = rootVisualElement.Q<FloatField>("virtual-ground-field");
            footProbesToggle = rootVisualElement.Q<Toggle>("foot-probes-toggle");
            markLeftPlantButton = rootVisualElement.Q<Button>("mark-left-plant-button");
            markRightPlantButton = rootVisualElement.Q<Button>("mark-right-plant-button");
            deletePlantButton = rootVisualElement.Q<Button>("delete-plant-button");
            sourceList = rootVisualElement.Q<ListView>("source-list");
            clipList = rootVisualElement.Q<ListView>("clip-list");
            sequenceEntryList = rootVisualElement.Q<ListView>("sequence-entry-list");
            searchField = rootVisualElement.Q<ToolbarSearchField>("search-field");
            scanAllToggle = rootVisualElement.Q<Toggle>("scan-all-toggle");
            loopToggle = rootVisualElement.Q<Toggle>("loop-toggle");
            gridToggle = rootVisualElement.Q<Toggle>("grid-toggle");
            speedSlider = rootVisualElement.Q<Slider>("speed-slider");
            timeSlider = rootVisualElement.Q<Slider>("time-slider");
            plantMarkerTrack = rootVisualElement.Q("plant-marker-track");
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
            stopButton = rootVisualElement.Q<Button>("stop-button");
            focusModeButton = rootVisualElement.Q<Button>("focus-mode-button");
            favoriteButton = rootVisualElement.Q<Button>("favorite-button");
            leftPane = rootVisualElement.Q("left-pane");
            rightPane = rootVisualElement.Q("right-pane");
            sequencePanel = rootVisualElement.Q("sequence-panel");
            profileField.objectType = typeof(AnimationPreviewProfile);
            modelField.objectType = typeof(GameObject);
            modelField.allowSceneObjects = false;
            sourceField.objectType = typeof(UnityEngine.Object);
            sourceField.allowSceneObjects = false;
            sequenceField.objectType = typeof(AnimationPreviewSequence);
            sequenceField.allowSceneObjects = false;
            motionProfileField.objectType = typeof(PlayerMotionProfile);
            motionProfileField.allowSceneObjects = false;
            footCalibrationField.objectType = typeof(PlayerFootCalibration);
            footCalibrationField.allowSceneObjects = false;
            footPlantDetectionModeField.Init(PlayerFootPlantDetectionMode.Loop);
            plantMarkerModeField.Init(PlayerPlantMarkerMode.Auto);
            rootMotionField.Init(AnimationPreviewRootMotionMode.Locked);
            modeField = new RadioButtonGroup { label = "Mode" };
            modeField.Add(new RadioButton("Single Clip"));
            modeField.Add(new RadioButton("Sequence"));
            rootVisualElement.Q("mode-container").Add(modeField);
            UpdatePreviewModeUI();
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
            sequenceEntryList.itemsSource = visibleSequenceEntries;
            sequenceEntryList.selectionType = SelectionType.None;
            sequenceEntryList.makeItem = () =>
            {
                Label label = new Label();
                label.AddToClassList("sequence-entry-row");
                return label;
            };
            sequenceEntryList.bindItem = (element, index) => ((Label)element).text = FormatSequenceEntry(visibleSequenceEntries[index], index);
        }

        private void RegisterCallbacks()
        {
            profileField.RegisterValueChangedCallback(evt => LoadProfile(evt.newValue as AnimationPreviewProfile));
            modelField.RegisterValueChangedCallback(evt => SetModel(evt.newValue as GameObject));
            footCalibrationField.RegisterValueChangedCallback(evt => SetFootCalibration(evt.newValue as PlayerFootCalibration));
            motionProfileField.RegisterValueChangedCallback(_ => { motionSettingsClip = null; UpdateMotionToolsUI(); });
            footPlantDetectionModeField.RegisterValueChangedCallback(_ => ApplyPlantAuthoringSettings());
            plantMarkerModeField.RegisterValueChangedCallback(_ => ApplyPlantAuthoringSettings());
            sequenceField.RegisterValueChangedCallback(evt => SelectSequence(evt.newValue as AnimationPreviewSequence));
            modeField.RegisterValueChangedCallback(evt => SelectPreviewMode((PreviewMode)evt.newValue));
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
            leftFootOffsetField.RegisterValueChangedCallback(_ => ApplyFootCalibrationSettings());
            rightFootOffsetField.RegisterValueChangedCallback(_ => ApplyFootCalibrationSettings());
            virtualGroundField.RegisterValueChangedCallback(_ => ApplyFootCalibrationSettings());
            footProbesToggle.RegisterValueChangedCallback(evt =>
            {
                if (session != null) session.ShowFootProbes = evt.newValue;
                viewport?.MarkDirtyRepaint();
            });
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
            stopButton.clicked += () => { session.SetPlaying(false); UpdatePlaybackUI(); };
            rootVisualElement.Q<Button>("reset-button").clicked += () => { session.ResetPlayback(); UpdatePlaybackUI(); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("previous-frame-button").clicked += () => { session.Step(-1); UpdatePlaybackUI(); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("next-frame-button").clicked += () => { session.Step(1); UpdatePlaybackUI(); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("focus-button").clicked += () => { session.Focus(); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("front-view-button").clicked += () => { session.SetView(new Vector2(180f, 0f)); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("side-view-button").clicked += () => { session.SetView(new Vector2(90f, 0f)); viewport.MarkDirtyRepaint(); };
            rootVisualElement.Q<Button>("back-view-button").clicked += () => { session.SetView(new Vector2(0f, 0f)); viewport.MarkDirtyRepaint(); };
            focusModeButton.clicked += ToggleFocusMode;
            favoriteButton.clicked += ToggleFavorite;
            rootVisualElement.Q<Button>("new-sequence-button").clicked += CreateSequence;
            rootVisualElement.Q<Button>("motion-bake-button").clicked += () => BakeMotion(true);
            rootVisualElement.Q<Button>("motion-rebake-button").clicked += () => BakeMotion(false);
            rootVisualElement.Q<Button>("motion-validate-button").clicked += ValidateMotionProfile;
            rootVisualElement.Q<Button>("motion-trajectory-button").clicked += ShowMotionTrajectory;
            markLeftPlantButton.clicked += () => MarkPlant(PlayerFoot.Left);
            markRightPlantButton.clicked += () => MarkPlant(PlayerFoot.Right);
            deletePlantButton.clicked += DeletePlant;
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
            session.ShowFootProbes = footProbesToggle == null || footProbesToggle.value;
            LoadFootCalibrationForModel(model);
            viewport.OverlayMessage = ready ? null : session.ModelError;
            if (ready && previewMode == PreviewMode.Sequence && sequenceField.value is AnimationPreviewSequence selectedSequence)
            {
                if (!session.SetSequence(selectedSequence)) viewport.OverlayMessage = session.CompatibilityMessage;
            }
            else if (ready && clipList.selectedItem is AnimationPreviewClipEntry selected)
            {
                if (!session.SetClip(selected)) viewport.OverlayMessage = session.CompatibilityMessage;
            }
            MarkProfileDirty();
            UpdateModelInfo();
            UpdateClipInfo();
            UpdateSequenceEntries();
            UpdateFavoriteButton();
            UpdateMotionToolsUI();
            UpdatePlaybackUI();
            viewport.MarkDirtyRepaint();
        }

        private void SetFootCalibration(PlayerFootCalibration calibration)
        {
            session?.SetFootCalibration(calibration);
            if (calibration != null) PopulateFootCalibrationFields(calibration);
            viewport?.MarkDirtyRepaint();
        }

        private void LoadFootCalibrationForModel(GameObject model)
        {
            PlayerFootCalibration calibration = model == null ? null : PlayerMotionBaker.FindCalibration(model);
            footCalibrationField?.SetValueWithoutNotify(calibration);
            session?.SetFootCalibration(calibration);
            if (calibration != null) PopulateFootCalibrationFields(calibration);
        }

        private void PopulateFootCalibrationFields(PlayerFootCalibration calibration)
        {
            leftFootOffsetField?.SetValueWithoutNotify(calibration.LeftFootSoleOffset);
            rightFootOffsetField?.SetValueWithoutNotify(calibration.RightFootSoleOffset);
            virtualGroundField?.SetValueWithoutNotify(calibration.VirtualGroundHeight);
        }

        private void ApplyFootCalibrationSettings()
        {
            if (applyingFootCalibration || footCalibrationField?.value is not PlayerFootCalibration calibration || modelField?.value is not GameObject model) return;
            applyingFootCalibration = true;
            calibration.Configure(model, leftFootOffsetField.value, rightFootOffsetField.value, virtualGroundField.value);
            EditorUtility.SetDirty(calibration);
            AssetDatabase.SaveAssetIfDirty(calibration);
            session?.SetFootCalibration(calibration);
            applyingFootCalibration = false;
            MarkProfileDirty();
            viewport?.MarkDirtyRepaint();
        }

        private void SelectClip(AnimationPreviewClipEntry entry)
        {
            if (entry == null) return;
            previewMode = PreviewMode.SingleClip;
            UpdatePreviewModeUI();
            bool ready = session.SetClip(entry);
            viewport.OverlayMessage = ready ? null : session.CompatibilityMessage ?? session.ModelError ?? "请先选择有效模型。";
            MarkProfileDirty();
            UpdateClipInfo();
            UpdatePlaybackUI();
            UpdateMotionToolsUI();
            viewport.MarkDirtyRepaint();
        }

        private void SelectSequence(AnimationPreviewSequence value, bool markDirty = true)
        {
            previewMode = PreviewMode.Sequence;
            sequenceField.SetValueWithoutNotify(value);
            UpdatePreviewModeUI();
            bool ready = session.SetSequence(value);
            viewport.OverlayMessage = ready ? null : session.CompatibilityMessage ?? session.ModelError ?? "请先选择有效模型和 Animation Sequence。";
            UpdateSequenceEntries();
            if (markDirty) MarkProfileDirty();
            UpdateClipInfo();
            UpdatePlaybackUI();
            UpdateFavoriteButton();
            UpdateMotionToolsUI();
            viewport.MarkDirtyRepaint();
        }

        private void SelectPreviewMode(PreviewMode value)
        {
            previewMode = value;
            UpdatePreviewModeUI();
            if (previewMode == PreviewMode.Sequence)
            {
                SelectSequence(sequenceField.value as AnimationPreviewSequence);
                return;
            }
            if (clipList.selectedItem is AnimationPreviewClipEntry selected)
            {
                SelectClip(selected);
                return;
            }
            session.SetClip(null);
            MarkProfileDirty();
            UpdateClipInfo();
            UpdatePlaybackUI();
            UpdateFavoriteButton();
            UpdateMotionToolsUI();
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
            previewMode = PreviewMode.SingleClip;
            sequenceField.SetValueWithoutNotify(null);
            if (value != null)
            {
                animationSources.AddRange(value.AnimationSources.Where(source => source != null));
                favorites.AddRange(value.Favorites.Where(clip => clip != null));
                sequenceField.SetValueWithoutNotify(value.LastSequence);
                previewMode = value.LastSequence != null ? PreviewMode.Sequence : PreviewMode.SingleClip;
                loopToggle.SetValueWithoutNotify(value.Loop);
                gridToggle.SetValueWithoutNotify(value.ShowGrid);
                speedSlider.SetValueWithoutNotify(value.PlaybackSpeed);
                rootMotionField.SetValueWithoutNotify(value.RootMotionMode);
                backgroundField.SetValueWithoutNotify(value.BackgroundColor);
                lightIntensitySlider.SetValueWithoutNotify(value.LightIntensity);
                lightRotationField.SetValueWithoutNotify(value.LightRotation);
            }
            UpdatePreviewModeUI();
            UpdateSequenceEntries();
            sourceList?.Rebuild();
            SetModel(value?.ModelAsset);
            profileDirty = false;
            UpdateDirtyLabel();
            ApplySessionSettings();
            RefreshLibrary();
            if (value?.LastSequence != null)
            {
                SelectSequence(value.LastSequence, false);
            }
            else if (value?.LastClip != null)
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
            profile.Store(modelField.value as GameObject, animationSources, favorites, session.Clip, session.Sequence, backgroundField.value, lightIntensitySlider.value, lightRotationField.value, (AnimationPreviewRootMotionMode)rootMotionField.value, loopToggle.value, gridToggle.value, speedSlider.value);
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

        private void CreateSequence()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Animation Preview Sequence", "AnimationPreviewSequence", "asset", "选择 Sequence 保存位置");
            if (string.IsNullOrEmpty(path)) return;
            AnimationPreviewSequence created = CreateInstance<AnimationPreviewSequence>();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            SelectSequence(created);
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
            UpdatePreviewModeUI();
            UpdateSequenceEntries();
            UpdateModelInfo();
            UpdateClipInfo();
            UpdatePlaybackUI();
            UpdateFavoriteButton();
            UpdateMotionToolsUI();
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
            if (session?.Sequence != null)
            {
                AnimationPreviewSequence selectedSequence = session.Sequence;
                string length = session.HasFiniteLength ? $"{session.Length:F3}s" : "∞";
                clipInfo.text = $"Sequence：{selectedSequence.name}\n条目：{selectedSequence.Entries.Count}\n长度：{length}\n模式：顺序播放";
                compatibilityInfo.text = session.CompatibilityMessage ?? "Sequence 与当前模型兼容";
                compatibilityInfo.EnableInClassList("status-error", session.CompatibilityMessage != null);
                return;
            }
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
            bool canSeek = session.IsReady && session.HasFiniteLength;
            timeSlider.lowValue = 0f;
            timeSlider.highValue = canSeek ? Mathf.Max(0.001f, (float)session.Length) : 1f;
            timeSlider.SetEnabled(canSeek);
            timeSlider.SetValueWithoutNotify(canSeek ? (float)session.Time : 0f);
            timeLabel.text = session.HasFiniteLength ? $"{session.Time:F2} / {session.Length:F2}" : $"{session.Time:F2} / ∞";
            playButton.text = session.IsPlaying ? "暂停" : "播放";
            playButton.SetEnabled(session.IsReady);
            stopButton.SetEnabled(session.IsPlaying);
        }

        private void UpdatePreviewModeUI()
        {
            if (modeField != null) modeField.SetValueWithoutNotify((int)previewMode);
            if (sequencePanel != null) sequencePanel.style.display = previewMode == PreviewMode.Sequence ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateSequenceEntries()
        {
            visibleSequenceEntries.Clear();
            AnimationPreviewSequence selectedSequence = sequenceField?.value as AnimationPreviewSequence;
            if (selectedSequence != null) visibleSequenceEntries.AddRange(selectedSequence.Entries);
            sequenceEntryList?.Rebuild();
        }

        private void UpdateMotionToolsUI()
        {
            bool supportsSingleClip = session != null && session.IsReady && !session.IsSequence;
            rootVisualElement.Q<Button>("motion-bake-button")?.SetEnabled(supportsSingleClip);
            rootVisualElement.Q<Button>("motion-rebake-button")?.SetEnabled(supportsSingleClip);
            rootVisualElement.Q<Button>("motion-trajectory-button")?.SetEnabled(supportsSingleClip);
            PlayerMotionProfile profile = motionProfileField?.value as PlayerMotionProfile;
            if (profile != null)
            {
                footPlantDetectionModeField?.SetValueWithoutNotify(profile.FootPlantDetectionMode);
                plantMarkerModeField?.SetValueWithoutNotify(profile.PlantMarkerMode);
                motionSettingsClip = session?.Clip;
            }
            else if (motionSettingsClip != session?.Clip)
            {
                footPlantDetectionModeField?.SetValueWithoutNotify(session?.Clip != null && session.Clip.isLooping ? PlayerFootPlantDetectionMode.Loop : PlayerFootPlantDetectionMode.Start);
                plantMarkerModeField?.SetValueWithoutNotify(PlayerPlantMarkerMode.Auto);
                motionSettingsClip = session?.Clip;
            }
            footPlantDetectionModeField?.SetEnabled(supportsSingleClip);
            plantMarkerModeField?.SetEnabled(supportsSingleClip);
            bool canEditPlantMarkers = supportsSingleClip && profile != null && profile.PlantMarkerMode == PlayerPlantMarkerMode.ManualOverride && PlayerFootPlantMarkerEditor.IsProfileForClip(profile, session.Clip);
            markLeftPlantButton?.SetEnabled(canEditPlantMarkers);
            markRightPlantButton?.SetEnabled(canEditPlantMarkers);
            deletePlantButton?.SetEnabled(canEditPlantMarkers);
            RefreshPlantMarkerTrack();
        }

        private void RefreshPlantMarkerTrack()
        {
            if (plantMarkerTrack == null) return;
            plantMarkerTrack.Clear();
            PlayerMotionProfile profile = motionProfileField?.value as PlayerMotionProfile;
            if (profile == null)
            {
                plantMarkerTrack.style.display = DisplayStyle.None;
                return;
            }
            plantMarkerTrack.style.display = DisplayStyle.Flex;
            foreach (PlayerFootPlantMarkerEditor.MarkerValue markerValue in PlayerFootPlantMarkerEditor.Read(profile))
            {
                Label marker = new Label(markerValue.Foot == PlayerFoot.Left ? "L" : markerValue.Foot == PlayerFoot.Right ? "R" : "?");
                marker.tooltip = $"{markerValue.Foot} Plant {markerValue.NormalizedTime:F3} / Confidence {markerValue.Confidence:F2}";
                marker.AddToClassList("plant-marker");
                marker.style.left = new Length(markerValue.NormalizedTime * 100f, LengthUnit.Percent);
                plantMarkerTrack.Add(marker);
            }
        }

        private static string FormatSequenceEntry(AnimationPreviewSequenceEntry entry, int index)
        {
            if (entry == null) return $"{index + 1}. <Missing>";
            string clipName = entry.Clip == null ? entry.Source == null ? "<Missing>" : entry.Source.name : entry.Clip.name;
            string duration = float.IsPositiveInfinity(entry.Duration) ? "∞" : $"{entry.Duration:F2}s";
            return $"{index + 1}. {clipName}\nStart {entry.StartTime:F2}s  Duration {duration}  Blend {entry.BlendDuration:F2}s";
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

        private void MarkPlant(PlayerFoot foot)
        {
            PlayerMotionProfile profile = motionProfileField?.value as PlayerMotionProfile;
            if (!CanEditPlantMarkers(profile))
            {
                ShowPlantMarkerMessage("只有与当前 Single Clip 匹配的 Motion Profile 才能编辑 Plant Marker。");
                return;
            }
            float normalizedTime = Mathf.Clamp01((float)(session.Time / session.Length));
            if (!PlayerFootPlantMarkerEditor.TryAddForClip(profile, session.Clip, foot, normalizedTime)) return;
            motionValidationLabel.text = $"已标记 {foot} Plant：{normalizedTime:F3}";
            RefreshPlantMarkerTrack();
        }

        private void DeletePlant()
        {
            PlayerMotionProfile profile = motionProfileField?.value as PlayerMotionProfile;
            if (!CanEditPlantMarkers(profile))
            {
                ShowPlantMarkerMessage("只有与当前 Single Clip 匹配的 Motion Profile 才能编辑 Plant Marker。");
                return;
            }
            float normalizedTime = Mathf.Clamp01((float)(session.Time / session.Length));
            if (!PlayerFootPlantMarkerEditor.TryRemoveNearestForClip(profile, session.Clip, normalizedTime, out PlayerFoot removedFoot))
            {
                ShowPlantMarkerMessage("当前时间附近没有 Plant Marker。");
                return;
            }
            motionValidationLabel.text = $"已删除 {removedFoot} Plant：{normalizedTime:F3} 附近标记";
            RefreshPlantMarkerTrack();
        }

        private bool CanEditPlantMarkers(PlayerMotionProfile profile)
        {
            return session != null && session.IsReady && !session.IsSequence && profile != null && profile.PlantMarkerMode == PlayerPlantMarkerMode.ManualOverride && PlayerFootPlantMarkerEditor.IsProfileForClip(profile, session.Clip);
        }

        private void ShowPlantMarkerMessage(string message)
        {
            motionValidationLabel.text = message;
            ShowNotification(new GUIContent(message));
        }

        private void BakeMotion(bool createIfMissing)
        {
            if (session == null || !session.IsReady)
            {
                motionValidationLabel.text = "请先选择有效 Model/Avatar 和 AnimationClip";
                return;
            }
            if (session.IsSequence)
            {
                motionValidationLabel.text = "Motion Profile Bake 目前只支持 Single Clip。";
                return;
            }
            PlayerMotionProfile target = motionProfileField.value as PlayerMotionProfile;
            if (target == null && createIfMissing)
            {
                //旋转存储位置
                string path = EditorUtility.SaveFilePanelInProject("Create Motion Profile", session.Clip.name + "MotionProfile", "asset", "选择 MotionProfile 保存位置");
                if (string.IsNullOrEmpty(path)) return;
                //创建SO，在内存上
                target = CreateInstance<PlayerMotionProfile>();
                target.SetPlantAuthoringSettings((PlayerFootPlantDetectionMode)footPlantDetectionModeField.value, (PlayerPlantMarkerMode)plantMarkerModeField.value);
                //真正在磁盘上船舰文件
                AssetDatabase.CreateAsset(target, path);
                motionProfileField.SetValueWithoutNotify(target);
            }
            if (target == null)
            {
                motionValidationLabel.text = "Rebake 需要先选择已有 Profile";
                return;
            }
            PlayerMotionBaker.Bake(session, Mathf.Max(1, motionSampleRateField.value), target);
            rootMotionField.SetValueWithoutNotify(AnimationPreviewRootMotionMode.Trajectory);
            ApplySessionSettings();
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            bool valid = PlayerMotionBaker.Validate(target, errors, warnings);
            string summary = $"已 Bake：{target.SampleCount} samples / {target.Duration:F3}s / Travel {target.EvaluateTravelDistance(1f):F3}m / Yaw {target.EvaluateYaw(1f):F1}° / Plant {target.PlantMarkers.Count}";
            motionValidationLabel.text = FormatMotionValidation(summary, valid, errors, warnings);
            UpdateMotionToolsUI();
            Selection.activeObject = target;
        }

        private void ValidateMotionProfile()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            bool valid = PlayerMotionBaker.Validate(motionProfileField.value as PlayerMotionProfile, errors, warnings);
            motionValidationLabel.text = FormatMotionValidation("Profile 有效且 Source 未过期。", valid, errors, warnings);
            UpdateMotionToolsUI();
        }

        private void ApplyPlantAuthoringSettings()
        {
            PlayerMotionProfile selectedProfile = motionProfileField?.value as PlayerMotionProfile;
            if (selectedProfile == null || footPlantDetectionModeField?.value == null || plantMarkerModeField?.value == null) return;
            PlayerFootPlantDetectionMode detectionMode = (PlayerFootPlantDetectionMode)footPlantDetectionModeField.value;
            PlayerPlantMarkerMode markerMode = (PlayerPlantMarkerMode)plantMarkerModeField.value;
            if (selectedProfile.FootPlantDetectionMode == detectionMode && selectedProfile.PlantMarkerMode == markerMode) return;
            Undo.RecordObject(selectedProfile, "Change Plant Authoring Settings");
            selectedProfile.SetPlantAuthoringSettings(detectionMode, markerMode);
            EditorUtility.SetDirty(selectedProfile);
            AssetDatabase.SaveAssetIfDirty(selectedProfile);
            UpdateMotionToolsUI();
        }

        private static string FormatMotionValidation(string successMessage, bool valid, ICollection<string> errors, ICollection<string> warnings)
        {
            List<string> lines = new List<string>();
            if (valid) lines.Add(successMessage);
            else foreach (string error in errors) lines.Add("Error: " + error);
            foreach (string warning in warnings) lines.Add("Warning: " + warning);
            return string.Join("\n", lines);
        }

        private void ShowMotionTrajectory()
        {
            if (session == null || !session.IsReady) return;
            if (session.IsSequence)
            {
                motionValidationLabel.text = "Motion Trajectory 目前只支持 Single Clip。";
                return;
            }
            PlayerMotionBakeResult result = session.SampleMotion(Mathf.Max(1, motionSampleRateField.value));
            rootMotionField.SetValueWithoutNotify(AnimationPreviewRootMotionMode.Trajectory);
            ApplySessionSettings();
            int last = result.PlanarPosition.Length - 1;
            motionValidationLabel.text = $"Trajectory：XZ ({result.PlanarPosition[last].x:F3}, {result.PlanarPosition[last].y:F3}) / Travel {result.TravelDistance[last]:F3}m / Yaw {result.Yaw[last]:F1}°";
            viewport.MarkDirtyRepaint();
        }
    }
}
