using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Splines;

namespace GruelTerraSplines
{
    public partial class TerraSplinesWindow
    {
        // Operation toggle UI
        VisualElement globalOperationContainer;
        Toggle globalOpHeightToggle;
        Toggle globalOpPaintToggle;
        Toggle globalOpHoleToggle;
        Toggle globalOpFillToggle;
        Toggle globalOpAddDetailToggle;
        Toggle globalOpRemoveDetailToggle;

        readonly Color opSelectedColor = Color.white;
        // Non-selected/available icon tint
        readonly Color opAvailableColor = new Color(0.47f, 0.47f, 0.47f, 1f);
        // Blocked/disabled icon tint (darker gray)
        readonly Color opBlockedColor = new Color(1f, 0.32f, 0.29f, 1f);

        readonly Color noiseInvertOnColor = Color.white;
        readonly Color noiseInvertOffColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        Toggle CreateOperationIconToggle(string iconName, string tooltip)
        {
            var toggle = new Toggle { tooltip = tooltip };
            toggle.AddToClassList("operation-icon-toggle");

            var icon = new Image();
            icon.AddToClassList("operation-icon");
            icon.image = LoadSplineIcon(iconName);
            toggle.Add(icon);

            RefreshOperationToggleVisual(toggle);
            return toggle;
        }

        void RefreshOperationToggleVisual(Toggle toggle)
        {
            if (toggle == null) return;
            var icon = toggle.Q<Image>(className: "operation-icon");
            if (icon == null) return;

            bool isBlocked = !toggle.enabledInHierarchy || !toggle.enabledSelf;
            bool isSelected = toggle.value;
            icon.tintColor = isBlocked ? opBlockedColor : (isSelected ? opSelectedColor : opAvailableColor);
        }

        void UpdateNoiseInvertToggleVisual(Toggle toggle)
        {
            if (toggle == null) return;
            var icon = toggle.Q<Image>(className: "noise-invert-icon");
            if (icon == null) return;

            bool isOn = toggle.value;
            icon.image = LoadSplineIcon(isOn ? "invert" : "uninvert");
            icon.tintColor = isOn ? noiseInvertOnColor : noiseInvertOffColor;
        }

        VisualElement globalDetailLayerPalette;
        Slider globalDetailStrengthSlider;
        FloatField globalDetailStrengthField;
        SliderInt globalDetailTargetDensitySlider;
        IntegerField globalDetailTargetDensityField;
        Slider globalDetailSlopeLimitSlider;
        FloatField globalDetailSlopeLimitField;
        Slider globalDetailFalloffPowerSlider;
        FloatField globalDetailFalloffPowerField;
        SliderInt globalDetailSpreadRadiusSlider;
        IntegerField globalDetailSpreadRadiusField;
        Slider globalDetailRemoveThresholdSlider;
        FloatField globalDetailRemoveThresholdField;
        ObjectField globalDetailNoiseTextureField;
        Slider globalDetailNoiseSizeSlider;
        FloatField globalDetailNoiseSizeField;
        Vector2Field globalDetailNoiseOffsetField;
        Slider globalDetailNoiseThresholdSlider;
        FloatField globalDetailNoiseThresholdField;
        Toggle globalDetailNoiseInvertToggle;
        Foldout globalDetailNoiseFoldout;

        ObjectField globalPaintNoiseTextureField;
        Slider globalPaintNoiseStrengthSlider;
        FloatField globalPaintNoiseStrengthField;
        Slider globalPaintNoiseEdgeSlider;
        FloatField globalPaintNoiseEdgeField;
        Slider globalPaintNoiseSizeSlider;
        FloatField globalPaintNoiseSizeField;
        Vector2Field globalPaintNoiseOffsetField;
        Toggle globalPaintNoiseInvertToggle;
        Foldout globalPaintNoiseFoldout;

        ObjectField globalBrushNoiseTextureField;
        Slider globalBrushNoiseStrengthSlider;
        FloatField globalBrushNoiseStrengthField;
        Slider globalBrushNoiseEdgeSlider;
        FloatField globalBrushNoiseEdgeField;
        Slider globalBrushNoiseSizeSlider;
        FloatField globalBrushNoiseSizeField;
        Vector2Field globalBrushNoiseOffsetField;
        Toggle globalBrushNoiseInvertToggle;
        Foldout globalBrushNoiseFoldout;

        void QueryUIElements()
        {
            // Main controls
            targetsFoldout = root.Q<Foldout>("targets-foldout");
            targetsWarningIcon = null; // Initialize to null, will be created dynamically
            terrainField = root.Q<ObjectField>("terrain-field");
            splineGroupField = root.Q<ObjectField>("spline-group-field");
            levelingValueSlider = root.Q<Slider>("leveling-value-slider");
            levelingValueField = root.Q<FloatField>("leveling-value-field");
            offsetButton = root.Q<Button>("offset-button");
            levelButton = root.Q<Button>("level-button");
            fillHolesButton = root.Q<Button>("fill-holes-button");
            clearPaintButton = root.Q<Button>("clear-paint-button");
            clearDetailButton = root.Q<Button>("clear-detail-button");

            // Global tab controls
            globalModeTab = root.Q<Button>("global-mode-tab");
            globalBrushTab = root.Q<Button>("global-brush-tab");
            globalPaintTab = root.Q<Button>("global-paint-tab");
            globalDetailTab = root.Q<Button>("global-detail-tab");
            globalModeContent = root.Q<VisualElement>("global-mode-content");
            globalBrushContent = root.Q<VisualElement>("global-brush-content");
            globalPaintContent = root.Q<VisualElement>("global-paint-content");
            globalDetailContent = root.Q<VisualElement>("global-detail-content");

            // Global controls
            pathModeButton = root.Q<Button>("path-mode-button");
            shapeModeButton = root.Q<Button>("shape-mode-button");
            EnsureModeButtonContent(pathModeButton, "path", "Path");
            EnsureModeButtonContent(shapeModeButton, "shape", "Shape");
            globalOperationContainer = root.Q<VisualElement>("global-operation-dropdown");

            globalOperationWarningLabel = root.Q<Label>("global-operation-warning");
            globalBrushPreviewImage = root.Q<Image>("global-brush-preview-image");
            globalBrushHardnessSlider = root.Q<Slider>("global-brush-hardness-slider");
            globalBrushHardnessField = root.Q<FloatField>("global-brush-hardness-field");
            globalBrushSizeSlider = root.Q<Slider>("global-brush-size-slider");
            globalBrushSizeField = root.Q<FloatField>("global-brush-size-field");
            globalStrengthSlider = root.Q<Slider>("global-strength-slider");
            globalStrengthField = root.Q<FloatField>("global-strength-field");
            globalSampleStepSlider = root.Q<Slider>("global-sample-step-slider");
            globalSampleStepField = root.Q<FloatField>("global-sample-step-field");
            globalBrushNoiseTextureField = root.Q<ObjectField>("global-brush-noise-texture");
            globalBrushNoiseStrengthSlider = root.Q<Slider>("global-brush-noise-strength-slider");
            globalBrushNoiseStrengthField = root.Q<FloatField>("global-brush-noise-strength-field");
            globalBrushNoiseEdgeSlider = root.Q<Slider>("global-brush-noise-edge-slider");
            globalBrushNoiseEdgeField = root.Q<FloatField>("global-brush-noise-edge-field");
            globalBrushNoiseSizeSlider = root.Q<Slider>("global-brush-noise-size-slider");
            globalBrushNoiseSizeField = root.Q<FloatField>("global-brush-noise-size-field");
            globalBrushNoiseOffsetField = root.Q<Vector2Field>("global-brush-noise-offset-field");
            globalBrushNoiseInvertToggle = root.Q<Toggle>("global-brush-noise-invert-toggle");
            globalBrushNoiseFoldout = root.Q<Foldout>("global-brush-noise-foldout");
            InsertNoiseFoldoutIcon(globalBrushNoiseFoldout);

            // Paint controls
            globalPaintLayerPalette = root.Q<VisualElement>("global-paint-layer-palette");
            globalPaintStrengthSlider = root.Q<Slider>("global-paint-strength-slider");
            globalPaintStrengthField = root.Q<FloatField>("global-paint-strength-field");
            paintAllButton = root.Q<Button>("paint-all-button");
            globalPaintNoiseTextureField = root.Q<ObjectField>("global-paint-noise-texture");
            globalPaintNoiseStrengthSlider = root.Q<Slider>("global-paint-noise-strength-slider");
            globalPaintNoiseStrengthField = root.Q<FloatField>("global-paint-noise-strength-field");
            globalPaintNoiseEdgeSlider = root.Q<Slider>("global-paint-noise-edge-slider");
            globalPaintNoiseEdgeField = root.Q<FloatField>("global-paint-noise-edge-field");
            globalPaintNoiseSizeSlider = root.Q<Slider>("global-paint-noise-size-slider");
            globalPaintNoiseSizeField = root.Q<FloatField>("global-paint-noise-size-field");
            globalPaintNoiseOffsetField = root.Q<Vector2Field>("global-paint-noise-offset-field");
            globalPaintNoiseInvertToggle = root.Q<Toggle>("global-paint-noise-invert-toggle");
            globalPaintNoiseFoldout = root.Q<Foldout>("global-paint-noise-foldout");
            InsertNoiseFoldoutIcon(globalPaintNoiseFoldout);

            // Detail controls (formerly grass)
            globalDetailLayerPalette = root.Q<VisualElement>("global-detail-layer-palette");
            globalDetailStrengthSlider = root.Q<Slider>("global-detail-strength-slider");
            globalDetailStrengthField = root.Q<FloatField>("global-detail-strength-field");
            var detailModeContainer = root.Q<VisualElement>("global-detail-mode-dropdown");
            detailModeContainer?.RemoveFromHierarchy();

            globalDetailTargetDensitySlider = root.Q<SliderInt>("global-detail-density-slider");
            globalDetailTargetDensityField = root.Q<IntegerField>("global-detail-density-field");
            globalDetailSlopeLimitSlider = root.Q<Slider>("global-detail-slope-slider");
            globalDetailSlopeLimitField = root.Q<FloatField>("global-detail-slope-field");
            globalDetailFalloffPowerSlider = root.Q<Slider>("global-detail-falloff-slider");
            globalDetailFalloffPowerField = root.Q<FloatField>("global-detail-falloff-field");
            globalDetailSpreadRadiusSlider = root.Q<SliderInt>("global-detail-spread-slider");
            globalDetailSpreadRadiusField = root.Q<IntegerField>("global-detail-spread-field");
            globalDetailRemoveThresholdSlider = root.Q<Slider>("global-detail-remove-threshold-slider");
            globalDetailRemoveThresholdField = root.Q<FloatField>("global-detail-remove-threshold-field");
            globalDetailNoiseTextureField = root.Q<ObjectField>("global-detail-noise-texture");
            globalDetailNoiseSizeSlider = root.Q<Slider>("global-detail-noise-size-slider");
            globalDetailNoiseSizeField = root.Q<FloatField>("global-detail-noise-size-field");
            globalDetailNoiseOffsetField = root.Q<Vector2Field>("global-detail-noise-offset-field");
            globalDetailNoiseThresholdSlider = root.Q<Slider>("global-detail-noise-threshold-slider");
            globalDetailNoiseThresholdField = root.Q<FloatField>("global-detail-noise-threshold-field");
            globalDetailNoiseInvertToggle = root.Q<Toggle>("global-detail-noise-invert-toggle");
            globalDetailNoiseFoldout = root.Q<Foldout>("global-detail-noise-foldout");
            InsertNoiseFoldoutIcon(globalDetailNoiseFoldout);

            undoButton = root.Q<Button>("undo-button");
            applyButton = root.Q<Button>("apply-button");
            redoButton = root.Q<Button>("redo-button");

            // Debug controls
            updateIntervalSlider = root.Q<Slider>("update-interval-slider");
            updateIntervalField = root.Q<FloatField>("update-interval-field");
            pauseResumeButton = root.Q<Button>("pause-resume-button");
            updatePreviewOnceButton = root.Q<Button>("update-preview-once-button");
            baselineTexture = root.Q<Image>("baseline-texture");
            previewTexture = root.Q<Image>("preview-texture");
            performanceTimingLabel = root.Q<Label>("performance-timing-label");
            rebuildPreviewButton = root.Q<Button>("rebuild-preview-button");
            clearCacheButton = root.Q<Button>("clear-cache-button");
            cacheInfoLabel = root.Q<Label>("cache-info-label");
            backendStatusLabel = root.Q<Label>("backend-status-label");
            cpuFallbackButton = root.Q<Button>("cpu-fallback-button");
            gpuPerformantButton = root.Q<Button>("gpu-performant-button");
            heightmapPreviewToggle = root.Q<Toggle>("heightmap-preview-toggle");
            supportOutsideChangesToggle = root.Q<Toggle>("support-outside-changes-toggle");
            featureHoleToggle = root.Q<Toggle>("feature-hole-toggle");
            featurePaintToggle = root.Q<Toggle>("feature-paint-toggle");
            featureDetailToggle = root.Q<Toggle>("feature-detail-toggle");
            benchmarkCacheButton = root.Q<Button>("benchmark-cache-button");
            creditsContainer = root.Q<VisualElement>("credits-container");
            creditsLinkLabel = root.Q<Label>("credits-link-label");
            if (creditsLinkLabel != null)
            {
                creditsLinkLabel.pickingMode = PickingMode.Ignore;
                creditsLinkLabel.tooltip = "Join GRUELSCUM discord";
            }
            if (creditsContainer != null)
            {
                creditsContainer.tooltip = "Join GRUELSCUM discord";
            }

            // Splines ListView
            splinesListView = root.Q<ListView>("splines-list");
            heightRangeLabel = root.Q<Label>("height-range-label");
            findRefsButton = root.Q<Button>("find-refs-button");
            collapseAllButton = root.Q<Button>("collapse-all-button");
            expandAllButton = root.Q<Button>("expand-all-button");
            splineCountLabel = root.Q<Label>("spline-count-label");
        }

        void InitializeUIValues()
        {
            // Initialize UI values from serialized fields
            if (targetsFoldout != null)
                targetsFoldout.value = foldoutTargets;

            // Initialize debug foldout state
            var debugFoldout = root.Q<Foldout>("debug-foldout");
            UpdateGlobalBrushNoiseUI();
            UpdateGlobalPaintNoiseUI();
            UpdateGlobalDetailNoiseUI();
            if (debugFoldout != null)
                debugFoldout.value = foldoutDebug;

            if (terrainField != null)
            {
                // Set object type to GameObject to accept terrain groups
                terrainField.objectType = typeof(GameObject);
                terrainField.value = targetTerrainGroup;
            }

            if (splineGroupField != null)
                splineGroupField.value = splineGroup;

            if (levelingValueSlider != null)
                levelingValueSlider.value = levelingValue;

            if (levelingValueField != null)
                levelingValueField.value = levelingValue;

            if (pathModeButton != null)
                SetModeButtonSelected(pathModeButton, globalMode == SplineApplyMode.Path);

            if (shapeModeButton != null)
                SetModeButtonSelected(shapeModeButton, globalMode == SplineApplyMode.Shape);

            // Build operation toggle UI and set initial values
            BuildGlobalOperationToggles();

            if (globalBrushPreviewImage != null)
                globalBrushPreviewImage.image = BrushFalloffUtils.GenerateBrushPreviewTexture(64, globalBrushHardness);

            if (globalBrushHardnessSlider != null)
                globalBrushHardnessSlider.value = globalBrushHardness;

            if (globalBrushHardnessField != null)
                globalBrushHardnessField.value = globalBrushHardness;

            if (globalBrushSizeSlider != null)
                globalBrushSizeSlider.value = globalBrushSize;

            if (globalBrushSizeField != null)
                globalBrushSizeField.value = globalBrushSize;

            if (globalStrengthSlider != null)
                globalStrengthSlider.value = globalStrength;

            if (globalStrengthField != null)
                globalStrengthField.value = globalStrength;

            if (globalSampleStepSlider != null)
                globalSampleStepSlider.value = globalSampleStep;

            if (globalSampleStepField != null)
                globalSampleStepField.value = globalSampleStep;

            if (globalPaintStrengthSlider != null)
                globalPaintStrengthSlider.value = globalPaintStrength;

            if (globalPaintStrengthField != null)
                globalPaintStrengthField.value = globalPaintStrength;

            if (globalDetailStrengthSlider != null)
                globalDetailStrengthSlider.value = globalDetailStrength;

            if (globalDetailStrengthField != null)
                globalDetailStrengthField.value = globalDetailStrength;

            if (globalDetailTargetDensitySlider != null)
                globalDetailTargetDensitySlider.value = globalDetailTargetDensity;

            if (globalDetailTargetDensityField != null)
                globalDetailTargetDensityField.value = globalDetailTargetDensity;

            if (globalDetailSlopeLimitSlider != null)
                globalDetailSlopeLimitSlider.value = globalDetailSlopeLimitDegrees;

            if (globalDetailSlopeLimitField != null)
                globalDetailSlopeLimitField.value = globalDetailSlopeLimitDegrees;

            if (globalDetailFalloffPowerSlider != null)
                globalDetailFalloffPowerSlider.value = DetailFalloffMapping.ToNormalized(globalDetailFalloffPower);

            if (globalDetailFalloffPowerField != null)
                globalDetailFalloffPowerField.value = DetailFalloffMapping.ToNormalized(globalDetailFalloffPower);

            if (globalDetailSpreadRadiusSlider != null)
                globalDetailSpreadRadiusSlider.value = globalDetailSpreadRadius;

            if (globalDetailSpreadRadiusField != null)
                globalDetailSpreadRadiusField.value = globalDetailSpreadRadius;

            if (globalDetailRemoveThresholdSlider != null)
                globalDetailRemoveThresholdSlider.value = globalDetailRemoveThreshold;

            if (globalDetailRemoveThresholdField != null)
                globalDetailRemoveThresholdField.value = globalDetailRemoveThreshold;

            UpdateGlobalDetailNoiseUI();

            if (updateIntervalSlider != null)
                updateIntervalSlider.value = updateInterval;

            if (updateIntervalField != null)
                updateIntervalField.value = updateInterval;

            if (pauseResumeButton != null)
                pauseResumeButton.text = updatesPaused ? "Resume Updates" : "Pause Updates";

            if (updatePreviewOnceButton != null)
                updatePreviewOnceButton.SetEnabled(updatesPaused);

            if (heightmapPreviewToggle != null)
            {
                heightmapPreviewToggle.value = heightmapPreviewEnabled;
                // Update visibility on initialization
                UpdatePreviewTextureVisibility();
            }

            if (supportOutsideChangesToggle != null)
                supportOutsideChangesToggle.value = supportOutsideChanges;

            if (featureHoleToggle != null)
                featureHoleToggle.value = featureHoleEnabled;

            if (featurePaintToggle != null)
                featurePaintToggle.value = featurePaintEnabled;

            if (featureDetailToggle != null)
                featureDetailToggle.value = featureDetailEnabled;

            // Initialize global tab state
            OnGlobalTabClicked(globalActiveTab);

            // Initialize spline count display
            UpdateSplineCountLabel();

            // Initialize layer palettes
            PopulateLayerPalette();
            PopulateDetailLayerPalette();

            // Initialize warning state
            UpdateTargetsWarning();

            // Create and setup global icons
            CreateDebugDocsButton();
            CreateGlobalIcons();
            UpdateGlobalIcons();
            UpdateDebugStatusIcon();
        }

        void UpdateOperationToggleUI()
        {
            var toggles = new[]
            {
                globalOpHeightToggle,
                globalOpPaintToggle,
                globalOpHoleToggle,
                globalOpFillToggle,
                globalOpAddDetailToggle,
                globalOpRemoveDetailToggle
            };

            globalOpHeightToggle?.SetValueWithoutNotify(globalOpHeight);
            globalOpPaintToggle?.SetValueWithoutNotify(globalOpPaint);
            globalOpHoleToggle?.SetValueWithoutNotify(globalOpHole);
            globalOpFillToggle?.SetValueWithoutNotify(globalOpFill);
            globalOpAddDetailToggle?.SetValueWithoutNotify(globalOpAddDetail);
            globalOpRemoveDetailToggle?.SetValueWithoutNotify(globalOpRemoveDetail);

            UpdateGlobalOperationInterlocks();

            foreach (var toggle in toggles)
            {
                RefreshOperationToggleVisual(toggle);
            }
        }

        void BuildGlobalOperationToggles()
        {
            if (globalOperationContainer == null) return;

            globalOperationContainer.Clear();
            globalOperationContainer.RemoveFromClassList("global-operation-dropdown");
            globalOperationContainer.style.flexDirection = FlexDirection.Row;
            globalOperationContainer.style.flexWrap = Wrap.Wrap;

            globalOpHeightToggle = CreateOperationIconToggle("height", "Height");
            globalOpPaintToggle = CreateOperationIconToggle("paint", "Paint");
            globalOpHoleToggle = CreateOperationIconToggle("hole", "Hole");
            globalOpFillToggle = CreateOperationIconToggle("fill", "Fill");
            globalOpAddDetailToggle = CreateOperationIconToggle("grass", "Add Detail");
            globalOpRemoveDetailToggle = CreateOperationIconToggle("cut", "Remove Detail");

            var toggles = new[]
            {
                globalOpHeightToggle,
                globalOpPaintToggle,
                globalOpHoleToggle,
                globalOpFillToggle,
                globalOpAddDetailToggle,
                globalOpRemoveDetailToggle
            };

            foreach (var toggle in toggles)
            {
                toggle.style.display = DisplayStyle.Flex;
                globalOperationContainer.Add(toggle);
            }

            UpdateOperationToggleUI();

            foreach (var toggle in toggles)
            {
                toggle.RegisterValueChangedCallback(_ => OnOperationToggleChanged());
            }
        }

        void OnOperationToggleChanged()
        {
            globalOpHeight = globalOpHeightToggle?.value ?? false;
            globalOpPaint = globalOpPaintToggle?.value ?? false;
            globalOpHole = globalOpHoleToggle?.value ?? false;
            globalOpFill = globalOpFillToggle?.value ?? false;
            globalOpAddDetail = globalOpAddDetailToggle?.value ?? false;
            globalOpRemoveDetail = globalOpRemoveDetailToggle?.value ?? false;

            // Enforce mutual exclusivity and single-op modes
            if (globalOpHole)
            {
                // Hole is exclusive: disable everything else
                globalOpHeight = false;
                globalOpPaint = false;
                globalOpFill = false;
                globalOpAddDetail = false;
                globalOpRemoveDetail = false;
            }

            // Fill cannot coexist with Hole
            if (globalOpFill)
            {
                globalOpHole = false;
            }

            // Detail modes are exclusive
            if (globalOpAddDetail)
            {
                globalOpRemoveDetail = false;
            }
            else if (globalOpRemoveDetail)
            {
                globalOpAddDetail = false;
            }

            // Ensure at least one operation remains active
            if (!globalOpHeight && !globalOpPaint && !globalOpHole && !globalOpFill && !globalOpAddDetail && !globalOpRemoveDetail)
            {
                globalOpHeight = true;
            }

            UpdateOperationToggleUI();

            // Sync detail mode from toggles
            globalDetailMode = globalOpRemoveDetail ? DetailOperationMode.Remove : DetailOperationMode.Add;

            UpdateGlobalIcons();
            UpdateGlobalOperationWarning();
            RefreshPreview();
        }

        void SetToggleEnabled(Toggle toggle, bool enabled)
        {
            if (toggle == null) return;
            toggle.SetEnabled(enabled);
            toggle.style.opacity = enabled ? 1f : 0.35f;
            RefreshOperationToggleVisual(toggle);
        }

        void UpdateGlobalOperationInterlocks()
        {
            // When Hole is active, all other operations are blocked
            if (globalOpHole)
            {
                SetToggleEnabled(globalOpHeightToggle, false);
                SetToggleEnabled(globalOpPaintToggle, false);
                SetToggleEnabled(globalOpFillToggle, false);
                SetToggleEnabled(globalOpAddDetailToggle, false);
                SetToggleEnabled(globalOpRemoveDetailToggle, false);
                return;
            }

            // Hole blocked when Fill is active
            bool holeBlocked = globalOpFill;
            SetToggleEnabled(globalOpHoleToggle, !holeBlocked);
            SetToggleEnabled(globalOpFillToggle, true);

            // Detail modes block each other
            SetToggleEnabled(globalOpAddDetailToggle, !globalOpRemoveDetail);
            SetToggleEnabled(globalOpRemoveDetailToggle, !globalOpAddDetail);

            // Height/Paint remain enabled unless Hole is active
            SetToggleEnabled(globalOpHeightToggle, true);
            SetToggleEnabled(globalOpPaintToggle, true);
        }

        void EnsureToggleCheckmarkVisible(Toggle toggle)
        {
            if (toggle == null) return;

            var input = toggle.Q<VisualElement>(className: "unity-toggle__input");
            if (input != null) input.style.display = DisplayStyle.Flex;

            var check = toggle.Q<VisualElement>(className: "unity-toggle__checkmark");
            if (check != null) check.style.display = DisplayStyle.Flex;
        }

        void PopulateLayerPalette()
        {
            if (globalPaintLayerPalette == null) return;

            // Clear existing layer buttons
            globalPaintLayerPalette.Clear();

            var terrains = GetAllTerrains();
            if (terrains.Count == 0)
            {
                var noLayersLabel = new Label("No terrain selected");
                noLayersLabel.AddToClassList("no-layers-label");
                globalPaintLayerPalette.Add(noLayersLabel);
                return;
            }

            // Use the first terrain that actually has paint layers
            var firstTerrain = terrains.FirstOrDefault(t => t != null && t.terrainData != null && t.terrainData.terrainLayers != null && t.terrainData.terrainLayers.Length > 0);
            if (firstTerrain == null)
            {
                var noLayersLabel = new Label("No layers configured");
                noLayersLabel.AddToClassList("no-layers-label");
                globalPaintLayerPalette.Add(noLayersLabel);
                return;
            }

            var terrainLayers = firstTerrain.terrainData.terrainLayers;
            if (globalSelectedLayerIndex < 0 || globalSelectedLayerIndex >= terrainLayers.Length)
            {
                globalSelectedLayerIndex = Mathf.Clamp(globalSelectedLayerIndex, 0, terrainLayers.Length - 1);
            }

            // Create layer buttons
            for (int i = 0; i < terrainLayers.Length; i++)
            {
                var layer = terrainLayers[i];
                if (layer == null) continue;

                var layerButton = new Button();
                layerButton.AddToClassList("layer-button");
                layerButton.AddToClassList(i == globalSelectedLayerIndex ? "layer-button-selected" : "layer-button-unselected");
                layerButton.tooltip = $"Click to select terrain layer '{layer.name}' for painting";

                // Create thumbnail
                var thumbnail = new Image();
                thumbnail.AddToClassList("layer-thumbnail");

                if (layer.diffuseTexture != null)
                {
                    thumbnail.image = layer.diffuseTexture;
                }
                else
                {
                    // Create a placeholder texture
                    var placeholder = new Texture2D(32, 32);
                    var colors = new Color32[32 * 32];
                    for (int j = 0; j < colors.Length; j++)
                    {
                        colors[j] = new Color32(128, 128, 128, 255);
                    }

                    placeholder.SetPixels32(colors);
                    placeholder.Apply();
                    thumbnail.image = placeholder;
                }

                layerButton.Add(thumbnail);

                var noiseEntry = GetPaintNoiseLayer(globalPaintNoiseLayers, i);
                if (noiseEntry != null && noiseEntry.noiseTexture != null)
                {
                    var noiseBadge = new Image();
                    noiseBadge.AddToClassList("layer-noise-badge");
                    noiseBadge.image = LoadSplineIcon("noise");
                    noiseBadge.scaleMode = ScaleMode.ScaleToFit;
                    layerButton.Add(noiseBadge);
                }

                // Add layer name
                var layerName = new Label(layer.name);
                layerName.AddToClassList("layer-name");
                layerButton.Add(layerName);

                // Store layer index
                layerButton.userData = i;

                // Bind click event
                layerButton.clicked += () => OnLayerSelected((int)layerButton.userData);

                globalPaintLayerPalette.Add(layerButton);
            }

            UpdateGlobalBrushNoiseUI();
            UpdateGlobalPaintNoiseUI();
        }

        void OnLayerSelected(int layerIndex)
        {
            var terrains = GetAllTerrains();
            if (terrains.Count == 0) return;

            // Use first terrain's layers for validation
            var firstTerrain = terrains[0];
            if (firstTerrain == null || firstTerrain.terrainData == null) return;

            var terrainLayers = firstTerrain.terrainData.terrainLayers;
            if (layerIndex < 0 || layerIndex >= terrainLayers.Length) return;

            globalSelectedLayerIndex = layerIndex;

            // Update UI
            PopulateLayerPalette();
            UpdateGlobalBrushNoiseUI();
            UpdateGlobalPaintNoiseUI();

            // Mark as dirty for serialization
            EditorUtility.SetDirty(this);
        }

        void PopulateDetailLayerPalette()
        {
            if (globalDetailLayerPalette == null) return;

            // Clear existing layer buttons
            globalDetailLayerPalette.Clear();

            var terrains = GetAllTerrains();
            if (terrains.Count == 0)
            {
                var noLayersLabel = new Label("No terrain selected");
                noLayersLabel.AddToClassList("no-layers-label");
                globalDetailLayerPalette.Add(noLayersLabel);
                return;
            }

            // Use the first terrain that actually has detail prototypes
            var firstTerrain = terrains.FirstOrDefault(t => t != null && t.terrainData != null && t.terrainData.detailPrototypes != null && t.terrainData.detailPrototypes.Length > 0);
            if (firstTerrain == null)
            {
                var noLayersLabel = new Label("No detail layers configured");
                noLayersLabel.AddToClassList("no-layers-label");
                globalDetailLayerPalette.Add(noLayersLabel);
                return;
            }

            var detailPrototypes = firstTerrain.terrainData.detailPrototypes;
            if (globalSelectedDetailLayerIndices == null)
                globalSelectedDetailLayerIndices = new List<int>();

            // Clamp any cached selections to available layers
            globalSelectedDetailLayerIndex = Mathf.Clamp(globalSelectedDetailLayerIndex, 0, detailPrototypes.Length - 1);
            globalSelectedDetailLayerIndices = globalSelectedDetailLayerIndices.Where(i => i >= 0 && i < detailPrototypes.Length).ToList();

            // Create layer buttons
            for (int i = 0; i < detailPrototypes.Length; i++)
            {
                var prototype = detailPrototypes[i];
                if (prototype == null) continue;

                var layerButton = new Button();
                layerButton.AddToClassList("layer-button");
                bool isSelected = (globalSelectedDetailLayerIndices != null && globalSelectedDetailLayerIndices.Count > 0)
                    ? globalSelectedDetailLayerIndices.Contains(i)
                    : i == globalSelectedDetailLayerIndex;
                layerButton.AddToClassList(isSelected ? "layer-button-selected" : "layer-button-unselected");
                string detailLayerName = prototype.prototype != null ? prototype.prototype.name : $"Layer {i}";
                layerButton.tooltip = $"Click to select detail layer '{detailLayerName}'. Shift+Click to toggle multiple layers.";

                // Create thumbnail
                var thumbnail = new Image();
                thumbnail.AddToClassList("layer-thumbnail");

                if (prototype.prototypeTexture != null)
                {
                    thumbnail.image = prototype.prototypeTexture;
                }
                else
                {
                    thumbnail.image = Texture2D.grayTexture;
                }

                layerButton.Add(thumbnail);

                var noiseEntry = GetDetailNoiseLayer(globalDetailNoiseLayers, i);
                if (noiseEntry != null && noiseEntry.noiseTexture != null)
                {
                    var noiseBadge = new Image();
                    noiseBadge.AddToClassList("layer-noise-badge");
                    noiseBadge.image = LoadSplineIcon("noise");
                    noiseBadge.scaleMode = ScaleMode.ScaleToFit;
                    layerButton.Add(noiseBadge);
                }

                // Add layer name
                var layerName = new Label(detailLayerName);
                layerName.AddToClassList("layer-name");
                layerButton.Add(layerName);

                // Store layer index
                layerButton.userData = i;

                // Bind click event (supports Shift+Click multi-select)
                layerButton.RegisterCallback<ClickEvent>(evt => OnDetailLayerSelected((int)layerButton.userData, evt.shiftKey));

                globalDetailLayerPalette.Add(layerButton);
            }
        }

        void OnDetailLayerSelected(int layerIndex, bool toggleMulti)
        {
            var terrains = GetAllTerrains();
            if (terrains.Count == 0) return;

            // Use first terrain's detail prototypes for validation
            var firstTerrain = terrains[0];
            if (firstTerrain == null || firstTerrain.terrainData == null) return;

            var detailPrototypes = firstTerrain.terrainData.detailPrototypes;
            if (layerIndex < 0 || layerIndex >= detailPrototypes.Length) return;

            if (globalSelectedDetailLayerIndices == null)
                globalSelectedDetailLayerIndices = new List<int>();

            if (!toggleMulti)
            {
                globalSelectedDetailLayerIndices.Clear();
                globalSelectedDetailLayerIndex = layerIndex;
            }
            else
            {
                if (globalSelectedDetailLayerIndices.Count == 0)
                    globalSelectedDetailLayerIndices.Add(globalSelectedDetailLayerIndex);

                if (globalSelectedDetailLayerIndices.Contains(layerIndex))
                    globalSelectedDetailLayerIndices.Remove(layerIndex);
                else
                    globalSelectedDetailLayerIndices.Add(layerIndex);

                if (globalSelectedDetailLayerIndices.Count == 0)
                    globalSelectedDetailLayerIndices.Add(layerIndex);

                globalSelectedDetailLayerIndex = layerIndex;
                globalSelectedDetailLayerIndices.Sort();
            }

            // Update UI
            PopulateDetailLayerPalette();

            UpdateGlobalDetailNoiseUI();

            // Mark as dirty for serialization
            EditorUtility.SetDirty(this);
            RefreshPreview();
        }

        PaintNoiseLayerSettings GetPaintNoiseLayer(List<PaintNoiseLayerSettings> list, int layerIndex)
        {
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry != null && entry.paintLayerIndex == layerIndex)
                    return entry;
            }
            return null;
        }

        PaintNoiseLayerSettings GetOrCreatePaintNoiseLayer(List<PaintNoiseLayerSettings> list, int layerIndex)
        {
            if (list == null)
                list = new List<PaintNoiseLayerSettings>();

            var entry = GetPaintNoiseLayer(list, layerIndex);
            if (entry != null) return entry;

            entry = new PaintNoiseLayerSettings { paintLayerIndex = layerIndex };
            list.Add(entry);
            return entry;
        }

        void UpdateGlobalPaintNoiseUI()
        {
            if (globalPaintNoiseTextureField == null && globalPaintNoiseStrengthSlider == null
                && globalPaintNoiseEdgeSlider == null && globalPaintNoiseOffsetField == null
                && globalPaintNoiseSizeSlider == null && globalPaintNoiseInvertToggle == null)
                return;

            int layerIndex = globalSelectedLayerIndex;
            var entry = GetPaintNoiseLayer(globalPaintNoiseLayers, layerIndex);

            var noiseTexture = entry != null ? entry.noiseTexture : null;
            float noiseStrength = entry != null ? entry.noiseStrength : 1f;
            float noiseEdge = entry != null ? entry.noiseEdge : 0f;
            float noiseSize = entry != null ? entry.noiseWorldSizeMeters : 10f;
            Vector2 noiseOffset = entry != null ? entry.noiseOffset : Vector2.zero;
            bool noiseInvert = entry != null && entry.noiseInvert;

            if (globalPaintNoiseFoldout != null)
                globalPaintNoiseFoldout.text = $"Noise {GetPaintLayerDisplayName(layerIndex)}";

            if (globalPaintNoiseTextureField != null)
                globalPaintNoiseTextureField.SetValueWithoutNotify(noiseTexture);
            if (globalPaintNoiseStrengthSlider != null)
                globalPaintNoiseStrengthSlider.SetValueWithoutNotify(noiseStrength);
            if (globalPaintNoiseStrengthField != null)
                globalPaintNoiseStrengthField.SetValueWithoutNotify(noiseStrength);
            if (globalPaintNoiseEdgeSlider != null)
                globalPaintNoiseEdgeSlider.SetValueWithoutNotify(noiseEdge);
            if (globalPaintNoiseEdgeField != null)
                globalPaintNoiseEdgeField.SetValueWithoutNotify(noiseEdge);
            if (globalPaintNoiseSizeSlider != null)
                globalPaintNoiseSizeSlider.SetValueWithoutNotify(noiseSize);
            if (globalPaintNoiseSizeField != null)
                globalPaintNoiseSizeField.SetValueWithoutNotify(noiseSize);
            if (globalPaintNoiseOffsetField != null)
                globalPaintNoiseOffsetField.SetValueWithoutNotify(noiseOffset);
            if (globalPaintNoiseInvertToggle != null)
            {
                globalPaintNoiseInvertToggle.SetValueWithoutNotify(noiseInvert);
                UpdateNoiseInvertToggleVisual(globalPaintNoiseInvertToggle);
            }

            SetNoiseHeaderIconTint(globalPaintNoiseFoldout, noiseTexture != null);
        }

        void UpdateGlobalBrushNoiseUI()
        {
            if (globalBrushNoiseTextureField == null && globalBrushNoiseStrengthSlider == null
                && globalBrushNoiseEdgeSlider == null && globalBrushNoiseOffsetField == null
                && globalBrushNoiseSizeSlider == null && globalBrushNoiseInvertToggle == null)
                return;

            var entry = globalBrushNoise ?? new BrushNoiseSettings();
            var noiseTexture = entry.noiseTexture;
            float noiseStrength = entry.noiseStrength;
            float noiseEdge = entry.noiseEdge;
            float noiseSize = entry.noiseWorldSizeMeters;
            Vector2 noiseOffset = entry.noiseOffset;
            bool noiseInvert = entry.noiseInvert;

            if (globalBrushNoiseFoldout != null)
                globalBrushNoiseFoldout.text = "Noise";

            if (globalBrushNoiseTextureField != null)
                globalBrushNoiseTextureField.SetValueWithoutNotify(noiseTexture);
            if (globalBrushNoiseStrengthSlider != null)
                globalBrushNoiseStrengthSlider.SetValueWithoutNotify(noiseStrength);
            if (globalBrushNoiseStrengthField != null)
                globalBrushNoiseStrengthField.SetValueWithoutNotify(noiseStrength);
            if (globalBrushNoiseEdgeSlider != null)
                globalBrushNoiseEdgeSlider.SetValueWithoutNotify(noiseEdge);
            if (globalBrushNoiseEdgeField != null)
                globalBrushNoiseEdgeField.SetValueWithoutNotify(noiseEdge);
            if (globalBrushNoiseSizeSlider != null)
                globalBrushNoiseSizeSlider.SetValueWithoutNotify(noiseSize);
            if (globalBrushNoiseSizeField != null)
                globalBrushNoiseSizeField.SetValueWithoutNotify(noiseSize);
            if (globalBrushNoiseOffsetField != null)
                globalBrushNoiseOffsetField.SetValueWithoutNotify(noiseOffset);
            if (globalBrushNoiseInvertToggle != null)
            {
                globalBrushNoiseInvertToggle.SetValueWithoutNotify(noiseInvert);
                UpdateNoiseInvertToggleVisual(globalBrushNoiseInvertToggle);
            }

            SetNoiseHeaderIconTint(globalBrushNoiseFoldout, noiseTexture != null);
        }

        DetailNoiseLayerSettings GetDetailNoiseLayer(List<DetailNoiseLayerSettings> list, int layerIndex)
        {
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry != null && entry.detailLayerIndex == layerIndex)
                    return entry;
            }
            return null;
        }

        DetailNoiseLayerSettings GetOrCreateDetailNoiseLayer(List<DetailNoiseLayerSettings> list, int layerIndex)
        {
            if (list == null)
                list = new List<DetailNoiseLayerSettings>();

            var entry = GetDetailNoiseLayer(list, layerIndex);
            if (entry != null) return entry;

            entry = new DetailNoiseLayerSettings { detailLayerIndex = layerIndex };
            list.Add(entry);
            return entry;
        }

        List<int> GetGlobalSelectedDetailLayerIndices()
        {
            if (globalSelectedDetailLayerIndices != null && globalSelectedDetailLayerIndices.Count > 0)
                return globalSelectedDetailLayerIndices.Distinct().ToList();

            return new List<int> { globalSelectedDetailLayerIndex };
        }

        void UpdateGlobalDetailNoiseUI()
        {
            if (globalDetailNoiseTextureField == null && globalDetailNoiseSizeSlider == null
                && globalDetailNoiseOffsetField == null && globalDetailNoiseThresholdSlider == null && globalDetailNoiseInvertToggle == null)
                return;

            var selectedLayerIndices = GetGlobalSelectedDetailLayerIndices();
            if (selectedLayerIndices.Count == 0)
                return;

            int layerIndex = selectedLayerIndices[0];
            var entry = GetDetailNoiseLayer(globalDetailNoiseLayers, layerIndex);

            var noiseTexture = entry != null ? entry.noiseTexture : null;
            float noiseSize = entry != null ? entry.noiseWorldSizeMeters : 10f;
            Vector2 noiseOffset = entry != null ? entry.noiseOffset : Vector2.zero;
            float noiseThreshold = entry != null ? entry.noiseThreshold : 0f;
            bool noiseInvert = entry != null && entry.noiseInvert;

            bool mixedTexture = false;
            bool mixedSize = false;
            bool mixedOffset = false;
            bool mixedThreshold = false;
            bool mixedInvert = false;
            bool hasNoiseTexture = noiseTexture != null;

            for (int i = 1; i < selectedLayerIndices.Count; i++)
            {
                int index = selectedLayerIndices[i];
                var otherEntry = GetDetailNoiseLayer(globalDetailNoiseLayers, index);
                var otherTexture = otherEntry != null ? otherEntry.noiseTexture : null;
                float otherSize = otherEntry != null ? otherEntry.noiseWorldSizeMeters : 10f;
                Vector2 otherOffset = otherEntry != null ? otherEntry.noiseOffset : Vector2.zero;
                float otherThreshold = otherEntry != null ? otherEntry.noiseThreshold : 0f;
                bool otherInvert = otherEntry != null && otherEntry.noiseInvert;

                if (otherTexture != noiseTexture)
                    mixedTexture = true;
                if (!Mathf.Approximately(otherSize, noiseSize))
                    mixedSize = true;
                if (otherOffset != noiseOffset)
                    mixedOffset = true;
                if (!Mathf.Approximately(otherThreshold, noiseThreshold))
                    mixedThreshold = true;
                if (otherInvert != noiseInvert)
                    mixedInvert = true;
                if (otherTexture != null)
                    hasNoiseTexture = true;
            }

            if (globalDetailNoiseFoldout != null)
                globalDetailNoiseFoldout.text = selectedLayerIndices.Count > 1
                    ? "Noise Multiple Layers"
                    : $"Noise {GetDetailLayerDisplayName(layerIndex)}";

            if (globalDetailNoiseTextureField != null)
            {
                globalDetailNoiseTextureField.SetValueWithoutNotify(noiseTexture);
                globalDetailNoiseTextureField.showMixedValue = mixedTexture;
            }
            if (globalDetailNoiseSizeSlider != null)
            {
                globalDetailNoiseSizeSlider.SetValueWithoutNotify(noiseSize);
                globalDetailNoiseSizeSlider.showMixedValue = mixedSize;
            }
            if (globalDetailNoiseSizeField != null)
            {
                globalDetailNoiseSizeField.SetValueWithoutNotify(noiseSize);
                globalDetailNoiseSizeField.showMixedValue = mixedSize;
            }
            if (globalDetailNoiseOffsetField != null)
            {
                globalDetailNoiseOffsetField.SetValueWithoutNotify(noiseOffset);
                globalDetailNoiseOffsetField.showMixedValue = mixedOffset;
            }
            if (globalDetailNoiseThresholdSlider != null)
            {
                globalDetailNoiseThresholdSlider.SetValueWithoutNotify(noiseThreshold);
                globalDetailNoiseThresholdSlider.showMixedValue = mixedThreshold;
            }
            if (globalDetailNoiseThresholdField != null)
            {
                globalDetailNoiseThresholdField.SetValueWithoutNotify(noiseThreshold);
                globalDetailNoiseThresholdField.showMixedValue = mixedThreshold;
            }
            if (globalDetailNoiseInvertToggle != null)
            {
                globalDetailNoiseInvertToggle.SetValueWithoutNotify(noiseInvert);
                globalDetailNoiseInvertToggle.showMixedValue = mixedInvert;
                UpdateNoiseInvertToggleVisual(globalDetailNoiseInvertToggle);
            }

            SetNoiseHeaderIconTint(globalDetailNoiseFoldout, hasNoiseTexture);
        }

        void InsertNoiseFoldoutIcon(Foldout foldout)
        {
            if (foldout == null) return;

            var toggle = foldout.Q<Toggle>();
            if (toggle == null) return;

            var existing = toggle.Q<Image>(className: "noise-header-icon");
            if (existing != null) return;

            var icon = new Image
            {
                image = LoadSplineIcon("noise"),
                scaleMode = ScaleMode.ScaleToFit
            };
            icon.AddToClassList("noise-header-icon");

            var input = toggle.Q<VisualElement>(className: "unity-toggle__input");
            if (input != null)
            {
                var inputIndex = toggle.IndexOf(input);
                toggle.Insert(Mathf.Clamp(inputIndex + 1, 0, toggle.childCount), icon);
            }
            else
            {
                toggle.Insert(0, icon);
            }
        }

        string GetDetailLayerDisplayName(int layerIndex)
        {
            var terrains = GetAllTerrains();
            if (terrains.Count == 0) return "Layer";

            var firstTerrain = terrains.FirstOrDefault(t => t != null && t.terrainData != null && t.terrainData.detailPrototypes != null && t.terrainData.detailPrototypes.Length > 0);
            if (firstTerrain == null || firstTerrain.terrainData == null || firstTerrain.terrainData.detailPrototypes == null)
                return $"Layer {layerIndex}";

            var detailPrototypes = firstTerrain.terrainData.detailPrototypes;
            if (layerIndex < 0 || layerIndex >= detailPrototypes.Length)
                return $"Layer {layerIndex}";

            var prototype = detailPrototypes[layerIndex];
            return prototype != null && prototype.prototype != null ? prototype.prototype.name : $"Layer {layerIndex}";
        }

        string GetPaintLayerDisplayName(int layerIndex)
        {
            var terrains = GetAllTerrains();
            if (terrains.Count == 0) return "Layer";

            var firstTerrain = terrains.FirstOrDefault(t => t != null && t.terrainData != null && t.terrainData.terrainLayers != null && t.terrainData.terrainLayers.Length > 0);
            if (firstTerrain == null || firstTerrain.terrainData == null || firstTerrain.terrainData.terrainLayers == null)
                return $"Layer {layerIndex}";

            var terrainLayers = firstTerrain.terrainData.terrainLayers;
            if (layerIndex < 0 || layerIndex >= terrainLayers.Length)
                return $"Layer {layerIndex}";

            var layer = terrainLayers[layerIndex];
            return layer != null ? layer.name : $"Layer {layerIndex}";
        }

        void SetupSplinesListView()
        {
            if (splinesListView == null) return;

            splinesListView.makeItem = () =>
            {
                // Create a placeholder element that will be replaced in bindItem
                var placeholder = new VisualElement();
                placeholder.AddToClassList("spline-item-container");
                return placeholder;
            };

            splinesListView.bindItem = (element, index) =>
            {
                // Add comprehensive safety checks
                if (element == null)
                {
                    Debug.LogWarning("ListView bindItem called with null element");
                    return;
                }

                if (index < 0 || index >= splineItems.Count)
                {
                    Debug.LogWarning($"ListView bindItem called with out-of-bounds index: {index} (count: {splineItems.Count})");
                    return;
                }

                var data = splineItems[index];
                if (data == null)
                {
                    Debug.LogWarning($"ListView bindItem encountered null data at index {index}");
                    return;
                }

                try
                {
                    // Clear the element and rebuild based on data type
                    element.Clear();

                    if (data is GroupItemData groupData)
                    {
                        // Load the group template first
                        var template = Resources.Load<VisualTreeAsset>("UI/GroupListItem");
                        if (template == null)
                        {
                            Debug.LogError("Failed to load GroupListItem template from Resources/UI/GroupListItem");
                            var fallback = new Label("Failed to load group item template");
                            fallback.style.color = Color.red;
                            element.Add(fallback);
                            return;
                        }

                        // Clone the template and add to element
                        var groupElement = template.CloneTree();
                        element.Add(groupElement);

                        // Now bind the data
                        BindGroupItem(element, groupData);
                    }
                    else if (data is SplineItemData splineData)
                    {
                        // Load the spline template first
                        var template = Resources.Load<VisualTreeAsset>("UI/SplineListItem");
                        if (template == null)
                        {
                            Debug.LogError("Failed to load SplineListItem template from Resources/UI/SplineListItem");
                            var fallback = new Label("Failed to load spline item template");
                            fallback.style.color = Color.red;
                            element.Add(fallback);
                            return;
                        }

                        // Clone the template and add to element
                        var splineElement = template.CloneTree();
                        element.Add(splineElement);

                        // Now bind the data
                        BindSplineItem(element, splineData);
                    }
                    else
                    {
                        Debug.LogError($"Unknown data type at index {index}: {data.GetType()}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error binding item at index {index}: {e.Message}");
                }
            };

            splinesListView.itemsSource = splineItems;
            splinesListView.selectionType = SelectionType.None;
            splinesListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            // Add height stabilization and scroll management
            splinesListView.style.height = StyleKeyword.Auto;

            // Add callback for item height changes to trigger refresh when needed
            splinesListView.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (evt.oldRect.height != evt.newRect.height)
                {
                    // Height changed, refresh items to ensure proper layout
                    splinesListView.schedule.Execute(() => { splinesListView.RefreshItems(); }).ExecuteLater(0);
                }
            });
        }

        void UpdateUIState()
        {
            // Update button states
            if (undoButton != null)
            {
                undoButton.SetEnabled(CanUndo);
            }

            if (redoButton != null)
            {
                redoButton.SetEnabled(CanRedo);
            }

            if (pauseResumeButton != null)
                pauseResumeButton.text = updatesPaused ? "Resume Updates" : "Pause Updates";
            if (updatePreviewOnceButton != null)
                updatePreviewOnceButton.SetEnabled(updatesPaused);

            // Update cache info
            if (cacheInfoLabel != null)
            {
                int splineCount = TerraSplinesTool.GetSplineCacheCount();
                float splineMemory = TerraSplinesTool.GetSplineCacheMemoryMB();

                cacheInfoLabel.text = $"Splines: {splineCount} ({splineMemory:F1} MB)";
            }

            // Update backend status
            if (backendStatusLabel != null)
            {
                string currentBackend = TerraSplinesTool.GetCurrentBackend();
                backendStatusLabel.text = $"Active Backend: {currentBackend}";
                backendStatusLabel.RemoveFromClassList("gpu");
                backendStatusLabel.RemoveFromClassList("cpu");
                if (currentBackend.Contains("GPU"))
                    backendStatusLabel.AddToClassList("gpu");
                else
                    backendStatusLabel.AddToClassList("cpu");
            }

            // Update preview textures
            if (baselineTexture != null && baselineTex != null)
            {
                baselineTexture.image = baselineTex;
                baselineTexture.MarkDirtyRepaint();
            }

            if (previewTexture != null && previewTex != null)
            {
                previewTexture.image = previewTex;
                previewTexture.MarkDirtyRepaint();
            }

            // Update performance timing display
            if (performanceTimingLabel != null)
            {
                performanceTimingLabel.text = $"Terrain: {terrainOperationTimeMs:F2}ms | Preview: {previewGenerationTimeMs:F2}ms | Baseline: {updateBaselineTimeMs:F2}ms | Apply: {applyPreviewTimeMs:F2}ms | Spline Previews: {updatePreviewTexturesTimeMs:F2}ms | Height Range: {updateHeightRangeTimeMs:F2}ms";
            }

            UpdateHeightRangeDisplay();

            // Update targets warning
            UpdateTargetsWarning();
            UpdateDebugStatusIcon();
        }

        /// <summary>
        /// Updates the warning indicator on the Targets foldout based on missing required fields
        /// </summary>
        void UpdateTargetsWarning()
        {
            if (targetsFoldout == null) return;

            // Check if any required field is missing
            // Check for terrain group and ensure it contains at least one terrain
            var terrains = GetAllTerrains();
            bool hasTerrain = targetTerrainGroup != null && terrains.Count > 0;
            bool hasSplineRefs = splineGroup != null;

            if (!hasTerrain || !hasSplineRefs)
            {
                // Show warning by adding a yellow warning icon to the foldout header
                if (targetsWarningIcon == null)
                {
                    // Get the foldout's toggle element - it's the first child of the foldout
                    VisualElement foldoutToggle = targetsFoldout.hierarchy.Children().FirstOrDefault();
                    if (foldoutToggle != null)
                    {
                        // Create yellow warning icon
                        targetsWarningIcon = new Label("⚠");
                        targetsWarningIcon.style.color = new Color(1f, 0.843f, 0f); // Yellow color
                        targetsWarningIcon.style.marginLeft = 6f;
                        targetsWarningIcon.style.fontSize = 14f;
                        targetsWarningIcon.style.alignSelf = Align.Center;
                        targetsWarningIcon.tooltip = "Warning: Missing terrain group or spline group reference. Assign both references for the tool to work properly.";

                        // Insert the warning icon into the toggle element
                        foldoutToggle.Add(targetsWarningIcon);
                    }
                }
                else
                {
                    // Ensure the warning icon is visible
                    targetsWarningIcon.style.display = DisplayStyle.Flex;
                }
            }
            else
            {
                // Hide warning by removing the warning icon
                if (targetsWarningIcon != null)
                {
                    targetsWarningIcon.style.display = DisplayStyle.None;
                }
            }
        }

        /// <summary>
        /// Updates only the preview texture Image elements without triggering full UI state updates.
        /// This prevents foldout click loss while ensuring preview textures are displayed.
        /// </summary>
        void UpdatePreviewTexturesUI()
        {
            // Update baseline texture
            if (baselineTexture != null && baselineTex != null)
            {
                baselineTexture.image = baselineTex;
                baselineTexture.MarkDirtyRepaint();
            }

            // Update preview texture
            if (previewTexture != null && previewTex != null)
            {
                previewTexture.image = previewTex;
                previewTexture.MarkDirtyRepaint();
            }

            // Update performance timing display
            if (performanceTimingLabel != null)
            {
                performanceTimingLabel.text = $"Terrain: {terrainOperationTimeMs:F2}ms | Preview: {previewGenerationTimeMs:F2}ms | Baseline: {updateBaselineTimeMs:F2}ms | Apply: {applyPreviewTimeMs:F2}ms | Spline Previews: {updatePreviewTexturesTimeMs:F2}ms | Height Range: {updateHeightRangeTimeMs:F2}ms";
            }

            // Update visibility based on toggle state
            UpdatePreviewTextureVisibility();
        }

        void UpdatePreviewTextureVisibility()
        {
            // Hide/show preview textures and timing label based on toggle state
            bool isVisible = heightmapPreviewEnabled;
            
            if (baselineTexture != null)
            {
                baselineTexture.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            if (previewTexture != null)
            {
                previewTexture.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // Also hide/show the preview textures row container
            var previewTexturesRow = root?.Q<VisualElement>("preview-textures-row");
            if (previewTexturesRow != null)
            {
                previewTexturesRow.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // Hide/show performance timing label
            if (performanceTimingLabel != null)
            {
                performanceTimingLabel.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        void UpdateUIStateWithoutValidation()
        {
            // Same as UpdateUIState but without calling ValidateAndResetReferences
            // This prevents clearing just-assigned references during UI refresh

            // Update button states
            if (undoButton != null)
            {
                undoButton.SetEnabled(CanUndo);
            }

            if (redoButton != null)
            {
                redoButton.SetEnabled(CanRedo);
            }

            if (pauseResumeButton != null)
                pauseResumeButton.text = updatesPaused ? "Resume Updates" : "Pause Updates";
            if (updatePreviewOnceButton != null)
                updatePreviewOnceButton.SetEnabled(updatesPaused);

            // Update cache info
            if (cacheInfoLabel != null)
            {
                int splineCount = TerraSplinesTool.GetSplineCacheCount();
                float splineMemory = TerraSplinesTool.GetSplineCacheMemoryMB();

                cacheInfoLabel.text = $"Splines: {splineCount} ({splineMemory:F1} MB)";
            }

            // Update backend status
            if (backendStatusLabel != null)
            {
                string currentBackend = TerraSplinesTool.GetCurrentBackend();
                backendStatusLabel.text = $"Active Backend: {currentBackend}";
                backendStatusLabel.RemoveFromClassList("gpu");
                backendStatusLabel.RemoveFromClassList("cpu");
                if (currentBackend.Contains("GPU"))
                    backendStatusLabel.AddToClassList("gpu");
                else
                    backendStatusLabel.AddToClassList("cpu");
            }

            // Update preview textures
            if (baselineTexture != null && baselineTex != null)
            {
                baselineTexture.image = baselineTex;
                baselineTexture.MarkDirtyRepaint();
            }

            if (previewTexture != null && previewTex != null)
            {
                previewTexture.image = previewTex;
                previewTexture.MarkDirtyRepaint();
            }

            UpdateHeightRangeDisplay();

            // Update targets warning
            UpdateTargetsWarning();

            // Update global operation warning
            UpdateGlobalOperationWarning();
            UpdateDebugStatusIcon();
        }

        void UpdateSplineCountLabel()
        {
            if (splineCountLabel == null) return;

            int groupCount = 0;
            int splineCount = 0;

            foreach (var item in splineItems)
            {
                if (item is GroupItemData groupItem)
                {
                    groupCount++;
                    splineCount += groupItem.splines.Count;
                }
                else if (item is SplineItemData)
                {
                    splineCount++;
                }
            }

            splineCountLabel.text = $"Groups: {groupCount} | Splines: {splineCount}";
        }

        void UpdateHeightRangeDisplay(bool forceRecalculate = false)
        {
            if (heightRangeLabel == null)
            {
                return;
            }

            var terrains = GetAllTerrains();
            if (terrains.Count == 0)
            {
                heightRangeLabel.text = "Baseline: 0.0-0.0m | Preview: 0.0-0.0m";
                return;
            }

            // Calculate combined height range from all terrains
            if (forceRecalculate || heightRangeNeedsUpdate)
            {
                // Calculate combined baseline range
                float baselineMin = float.MaxValue;
                float baselineMax = float.MinValue;
                float previewMin = float.MaxValue;
                float previewMax = float.MinValue;

                foreach (var terrain in terrains)
                {
                    var state = terrainStateManager?.TerrainStates?.GetValueOrDefault(terrain);
                    if (state == null) continue;

                    // Baseline range
                    if (state.BaselineHeights != null)
                    {
                        var range = GetHeightmapRange(state.BaselineHeights, terrain);
                        if (range.min < baselineMin) baselineMin = range.min;
                        if (range.max > baselineMax) baselineMax = range.max;
                    }

                    // Preview/working range
                    var previewHeights = state.WorkingHeights ?? state.BaselineHeights;
                    if (previewHeights != null)
                    {
                        var range = GetHeightmapRange(previewHeights, terrain);
                        if (range.min < previewMin) previewMin = range.min;
                        if (range.max > previewMax) previewMax = range.max;
                    }
                }

                cachedBaselineRange = (baselineMin == float.MaxValue) ? (0f, 0f) : (baselineMin, baselineMax);
                cachedPreviewRange = (previewMin == float.MaxValue) ? (0f, 0f) : (previewMin, previewMax);

                heightRangeNeedsUpdate = false;
            }

            heightRangeLabel.text = $"Baseline: {cachedBaselineRange.min:F1}-{cachedBaselineRange.max:F1}m | Preview: {cachedPreviewRange.min:F1}-{cachedPreviewRange.max:F1}m";
        }

        void SetModeButtonSelected(Button button, bool selected)
        {
            if (button == null) return;

            if (selected)
            {
                button.AddToClassList("mode-button-selected");
            }
            else
            {
                button.RemoveFromClassList("mode-button-selected");
            }
        }

        void EnsureModeButtonContent(Button button, string iconName, string labelText)
        {
            if (button == null)
                return;

            if (button.Q<VisualElement>("mode-button-content") != null)
                return;

            button.text = string.Empty;

            var content = new VisualElement { name = "mode-button-content" };
            content.style.flexDirection = FlexDirection.Row;
            content.style.alignItems = Align.Center;
            content.style.justifyContent = Justify.Center;
            content.style.height = Length.Percent(100);
            content.style.width = Length.Percent(100);

            var icon = new Image();
            icon.style.width = 14;
            icon.style.height = 14;
            icon.style.minWidth = 14;
            icon.style.minHeight = 14;
            icon.style.marginRight = 6;
            icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            icon.image = LoadSplineIcon(iconName);
            content.Add(icon);

            button.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (icon.image == null)
                    icon.image = LoadSplineIcon(iconName);
            });

            var label = new Label(labelText);
            label.style.fontSize = 11;
            label.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            content.Add(label);

            button.Add(content);
        }

        void SetTabButtonActive(Button button, bool active)
        {
            if (button == null) return;

            if (active)
            {
                button.AddToClassList("spline-tab-button-active");
            }
            else
            {
                button.RemoveFromClassList("spline-tab-button-active");
            }
        }

        void UpdateTabText(Button tabButton, string baseText, bool isOverrideActive)
        {
            if (tabButton == null) return;

            tabButton.text = isOverrideActive ? $"{baseText} ◦" : baseText;
        }

        void UpdateSplineNameIndicator(VisualElement splineItemElement, SplineItemData data)
        {
            if (splineItemElement == null || data == null || data.container == null) return;

            var nameLabel = splineItemElement.Q<Label>("spline-name-label");
            if (nameLabel == null) return;

            string hierarchyPath = GetHierarchyPath(data.container.transform, splineGroup);
            string gameObjectName = data.container.name;

            // Check if any overrides are active
            bool hasOverrides = data.settings.overrideMode || data.settings.overrideBrush || data.settings.overridePaint || data.settings.overrideDetail;

            string indicator = hasOverrides ? " ◦" : string.Empty;

            // Get the global index of this spline
            int globalIndex = GetGlobalSplineIndex(data);

            if (hierarchyPath == gameObjectName)
            {
                nameLabel.text = $"[{globalIndex}] <b>{gameObjectName}</b>{indicator}";
            }
            else
            {
                string pathWithoutName = hierarchyPath.Substring(0, hierarchyPath.LastIndexOf('/'));
                nameLabel.text = $"[{globalIndex}] {pathWithoutName}/<b>{gameObjectName}</b>{indicator}";
            }

            // Update icons
            UpdateSplineIcons(splineItemElement, data);
        }

        void UpdateSplineIcons(VisualElement splineItemElement, SplineItemData data)
        {
            if (splineItemElement == null || data == null) return;

            // Get icon elements
            var modeIcon = splineItemElement.Q<Image>("spline-mode-icon");
            var operationsContainer = splineItemElement.Q<VisualElement>("spline-operation-icons");

            if (modeIcon == null || operationsContainer == null) return;

            // If using global settings (no override), show a single global icon and hide others
            if (!data.settings.overrideMode)
            {
                modeIcon.image = LoadSplineIcon("global");
                modeIcon.tooltip = "Using global mode and operation settings (no override)";
                modeIcon.style.display = DisplayStyle.Flex;
                SetIconTint(modeIcon, false);
                operationsContainer.style.display = DisplayStyle.None;
                return;
            }

            // Get effective mode and operations
            SplineApplyMode mode = data.settings.overrideMode ? data.settings.mode : globalMode;
            bool opHeight = data.settings.overrideMode ? data.settings.operationHeight : globalOpHeight;
            bool opPaint = data.settings.overrideMode ? data.settings.operationPaint : globalOpPaint;
            bool opHole = data.settings.overrideMode ? data.settings.operationHole : globalOpHole;
            bool opFill = data.settings.overrideMode ? data.settings.operationFill : globalOpFill;
            bool opAddDetail = data.settings.overrideMode ? data.settings.operationAddDetail : globalOpAddDetail;
            bool opRemoveDetail = data.settings.overrideMode ? data.settings.operationRemoveDetail : globalOpRemoveDetail;

            opFill = opFill && !opHole;
            opRemoveDetail = opRemoveDetail && !opAddDetail;

            // Check if shape mode is selected but no splines are closed
            bool isModeDisabled = false;
            if (mode == SplineApplyMode.Shape && data.container != null)
            {
                bool hasClosedSpline = false;
                foreach (var spline in data.container.Splines)
                {
                    if (spline.Closed)
                    {
                        hasClosedSpline = true;
                        break;
                    }
                }

                // Shape mode is disabled if no closed splines exist
                isModeDisabled = !hasClosedSpline;
            }

            // Update mode icon
            string modeIconName = mode == SplineApplyMode.Path ? "path" : "shape";
            modeIcon.image = LoadSplineIcon(modeIconName);
            modeIcon.tooltip = mode == SplineApplyMode.Path 
                ? "Spline mode: Path (creates terrain paths along spline curves)" 
                : "Spline mode: Shape (fills interior of closed splines)" + (isModeDisabled ? " - No closed splines" : "");
            SetIconTint(modeIcon, isModeDisabled);

            var operations = new List<(string icon, string tooltip, bool disabled)>();

            if (opHeight)
                operations.Add(("height", "Operation: Height (modify terrain heights)", false));

            if (opPaint)
                operations.Add(("paint", "Operation: Paint (paint textures)", !featurePaintEnabled));

            if (opHole)
                operations.Add(("hole", "Operation: Hole (cut holes in terrain)", !featureHoleEnabled));
            else if (opFill)
                operations.Add(("fill", "Operation: Fill (fill terrain holes)", !featureHoleEnabled));

            if (opAddDetail)
                operations.Add(("grass", "Operation: Add detail", !FeatureDetailEnabled));
            else if (opRemoveDetail)
                operations.Add(("cut", "Operation: Remove detail", !FeatureDetailEnabled));

            UpdateOperationIcons(operationsContainer, operations);
        }

        readonly Color disabledColor = new Color(1f, 0.32f, 0.29f, 1f);
        readonly Color enabledColor = new Color(0.44f, 0.44f, 0.44f, 1f);
        readonly Color noiseEmptyColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        readonly Color noiseActiveColor = Color.white;

        void SetIconTint(Image icon, bool isDisabled)
        {
            if (icon == null) return;

            icon.tintColor = isDisabled ? disabledColor : enabledColor;
        }

        void SetNoiseHeaderIconTint(Foldout foldout, bool hasTexture)
        {
            if (foldout == null) return;

            var toggle = foldout.Q<Toggle>();
            if (toggle == null) return;

            var icon = toggle.Q<Image>(className: "noise-header-icon");
            if (icon == null) return;

            icon.tintColor = hasTexture ? noiseActiveColor : noiseEmptyColor;
        }

        Texture2D LoadSplineIcon(string iconName)
        {
            // Check cache first
            if (iconCache.TryGetValue(iconName, out Texture2D cachedIcon) && cachedIcon != null)
            {
                return cachedIcon;
            }

            // Load icon from Resources
            Texture2D icon = Resources.Load<Texture2D>($"Icons/{iconName}");
            if (icon != null)
            {
                iconCache[iconName] = icon;
            }

            return icon;
        }

        void NotifyOverlaySettingsChanged(SplineContainer container)
        {
            if (container == null) return;
            var settings = container.GetComponent<TerrainSplineSettings>();
            TerrainSplineSettingsOverlay.NotifySettingsChanged(settings);
        }

        void UpdateOperationIcons(VisualElement container, List<(string icon, string tooltip, bool disabled)> operations)
        {
            if (container == null) return;

            // Ensure we have enough icon elements for the operations we want to display
            while (container.childCount < operations.Count)
            {
                var iconElement = new Image();
                iconElement.AddToClassList("spline-icon");
                iconElement.AddToClassList("spline-operation-icon");
                container.Add(iconElement);
            }

            for (int i = 0; i < container.childCount; i++)
            {
                var iconElement = container[i] as Image;
                if (iconElement == null)
                    continue;

                if (i < operations.Count)
                {
                    var op = operations[i];
                    iconElement.image = LoadSplineIcon(op.icon);
                    iconElement.tooltip = op.tooltip + (op.disabled ? " - Feature disabled" : string.Empty);
                    iconElement.style.display = DisplayStyle.Flex;
                    SetIconTint(iconElement, op.disabled);
                }
                else
                {
                    iconElement.style.display = DisplayStyle.None;
                }
            }

            container.style.display = operations.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void CreateDebugDocsButton()
        {
            if (root == null) return;

            var debugFoldout = root.Q<Foldout>("debug-foldout");
            if (debugFoldout == null) return;

            VisualElement foldoutToggle = debugFoldout.hierarchy.Children().FirstOrDefault();
            if (foldoutToggle == null) return;

            if (debugDocsButton != null && debugDocsButton.parent == foldoutToggle)
            {
                if (debugDocsIcon != null && debugDocsIcon.image == null)
                {
                    debugDocsIcon.image = LoadSplineIcon("doc");
                }
                if (debugStatusIcon != null && debugStatusIcon.parent != foldoutToggle)
                {
                    foldoutToggle.Add(debugStatusIcon);
                }
                return;
            }

            var existingButton = foldoutToggle.Q<Button>("debug-docs-button");
            if (existingButton != null)
            {
                debugDocsButton = existingButton;
                debugDocsIcon = existingButton.Q<Image>("debug-docs-icon");
                if (debugDocsIcon != null && debugDocsIcon.image == null)
                {
                    debugDocsIcon.image = LoadSplineIcon("doc");
                }
                var existingStatusIcon = foldoutToggle.Q<Image>("debug-status-icon");
                if (existingStatusIcon != null)
                {
                    debugStatusIcon = existingStatusIcon;
                }
                return;
            }

            var spacer = foldoutToggle.Q<VisualElement>("debug-docs-spacer");
            if (spacer == null)
            {
                spacer = new VisualElement { name = "debug-docs-spacer" };
                spacer.style.flexGrow = 1f;
                spacer.style.flexBasis = 0f;
                foldoutToggle.Add(spacer);
            }

            debugDocsButton = new Button
            {
                name = "debug-docs-button",
                tooltip = "Open Documentation"
            };
            debugDocsButton.AddToClassList("section-header-icon-button");
            debugDocsButton.style.alignSelf = Align.Center;
            debugDocsButton.style.width = 16;
            debugDocsButton.style.height = 16;
            debugDocsButton.style.minWidth = 16;
            debugDocsButton.style.minHeight = 16;
            debugDocsButton.style.marginTop = 0;
            debugDocsButton.style.marginRight = 16;
            debugDocsButton.style.marginBottom = 0;

            debugDocsIcon = new Image { name = "debug-docs-icon" };
            debugDocsIcon.AddToClassList("section-header-icon");
            debugDocsIcon.image = LoadSplineIcon("doc");
            debugDocsIcon.style.width = 14;
            debugDocsIcon.style.height = 14;
            debugDocsIcon.style.marginTop = 0;
            debugDocsIcon.style.marginBottom = 0;

            debugDocsButton.Add(debugDocsIcon);
            debugStatusIcon = new Image
            {
                name = "debug-status-icon",
                tooltip = "Realtime updates active"
            };
            debugStatusIcon.AddToClassList("section-header-icon");
            debugStatusIcon.AddToClassList("section-header-status-icon");
            debugStatusIcon.style.width = 14;
            debugStatusIcon.style.height = 14;
            debugStatusIcon.style.marginTop = 0;
            debugStatusIcon.style.marginBottom = 0;

            foldoutToggle.Add(debugStatusIcon);
            foldoutToggle.Add(debugDocsButton);
        }

        void UpdateDebugStatusIcon()
        {
            if (debugStatusIcon == null)
                return;

            string iconName = updatesPaused ? PauseIconName : ResumeIconName;
            debugStatusIcon.image = LoadSplineIcon(iconName);
            debugStatusIcon.tooltip = updatesPaused ? "Realtime updates paused" : "Realtime updates active";
        }

        void CreateGlobalIcons()
        {
            // Find the global foldout
            var globalFoldout = root?.Q<Foldout>("global-foldout");
            if (globalFoldout == null) return;

            // Get the foldout's toggle element - it's the first child of the foldout
            VisualElement foldoutToggle = globalFoldout.hierarchy.Children().FirstOrDefault();
            if (foldoutToggle == null) return;

            // Create icon container
            globalIconsContainer = new VisualElement();
            globalIconsContainer.AddToClassList("spline-icons-container");
            globalIconsContainer.style.alignItems = Align.FlexEnd;
            globalIconsContainer.style.marginLeft = 8f;

            // Create icon elements
            globalModeIcon = new Image();
            globalModeIcon.AddToClassList("spline-icon");
            globalModeIcon.name = "global-mode-icon";
            globalModeIcon.tooltip = "Global spline mode: Path (linear) or Shape (filled)";

            globalOperationIconsContainer = new VisualElement();
            globalOperationIconsContainer.name = "global-operation-icons";
            globalOperationIconsContainer.AddToClassList("spline-operation-icons");

            // Add icons to container
            globalIconsContainer.Add(globalModeIcon);
            globalIconsContainer.Add(globalOperationIconsContainer);

            // Insert the icon container into the foldout toggle
            foldoutToggle.Add(globalIconsContainer);
        }

        void UpdateGlobalIcons()
        {
            if (globalModeIcon == null || globalOperationIconsContainer == null) return;

            // Update mode icon
            string modeIconName = globalMode == SplineApplyMode.Path ? "path" : "shape";
            globalModeIcon.image = LoadSplineIcon(modeIconName);
            globalModeIcon.tooltip = globalMode == SplineApplyMode.Path 
                ? "Global mode: Path (creates terrain paths along spline curves)" 
                : "Global mode: Shape (fills interior of closed splines)";
            SetIconTint(globalModeIcon, false);
            globalModeIcon.style.display = DisplayStyle.Flex;

            var operations = new List<(string icon, string tooltip, bool disabled)>();

            if (globalOpHeight)
                operations.Add(("height", "Global operation: Height (modify terrain heights)", false));

            if (globalOpPaint)
                operations.Add(("paint", "Global operation: Paint (paint terrain textures)", !featurePaintEnabled));

            if (globalOpHole)
                operations.Add(("hole", "Global operation: Hole (cut holes in terrain)", !featureHoleEnabled));
            else if (globalOpFill)
                operations.Add(("fill", "Global operation: Fill (fill holes in terrain)", !featureHoleEnabled));

            if (globalOpAddDetail)
                operations.Add(("grass", "Global operation: Add detail", !FeatureDetailEnabled));
            else if (globalOpRemoveDetail)
                operations.Add(("cut", "Global operation: Remove detail", !FeatureDetailEnabled));

            UpdateOperationIcons(globalOperationIconsContainer, operations);
        }

        void UpdateGlobalOperationWarning()
        {
            if (globalOperationWarningLabel == null)
                return;

            bool requiresPaint = globalOpPaint;
            bool requiresHole = globalOpHole;
            bool requiresFill = globalOpFill;
            bool requiresDetail = globalOpAddDetail || globalOpRemoveDetail;

            bool shouldShow = false;

            if (requiresPaint && !featurePaintEnabled)
                shouldShow = true;
            else if ((requiresHole || requiresFill) && !featureHoleEnabled)
                shouldShow = true;
            else if (requiresDetail && !FeatureDetailEnabled)
                shouldShow = true;

            globalOperationWarningLabel.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
