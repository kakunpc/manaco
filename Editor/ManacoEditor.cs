using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    [CustomEditor(typeof(Manaco))]
    public class ManacoEditor : UnityEditor.Editor
    {
        private SerializedProperty _eyeRegionsProp, _useNdmfPreviewProp, _modeProp, _sourceAvatarPrefabProp;
        private SerializedProperty _tutorialPageProp, _tutorialSkippedProp, _tutorialCompletedProp;
        private ManacoPreset[] _availablePresets;
        private string[] _presetNames;
        private int _selectedPresetIndex, _selectedSourcePresetIndex;
        private ManacoMaterialDefinition[] _availableShaders;
        private string[] _shaderNames;
        private int _selectedShaderIndex;
        private bool _showAdvanced;
        private readonly Dictionary<string, ManacoEmbeddedUvSelector> _selectorCache = new Dictionary<string, ManacoEmbeddedUvSelector>();
        private int _expandedRegionIndex = -1;
        private bool _expandedRegionIsSource;
        private string _tutorialWarning;

        private void OnEnable()
        {
            _eyeRegionsProp = serializedObject.FindProperty("eyeRegions");
            _useNdmfPreviewProp = serializedObject.FindProperty("useNdmfPreview");
            _modeProp = serializedObject.FindProperty("mode");
            _sourceAvatarPrefabProp = serializedObject.FindProperty("sourceAvatarPrefab");
            _tutorialPageProp = serializedObject.FindProperty("tutorialPage");
            _tutorialSkippedProp = serializedObject.FindProperty("tutorialSkipped");
            _tutorialCompletedProp = serializedObject.FindProperty("tutorialCompleted");
            LoadPresets();
            LoadShaders();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ManacoEmbeddedUvSelector.ClearActiveSelector();
            var comp = (Manaco)target;
            _tutorialPageProp.intValue = Mathf.Clamp(_tutorialPageProp.intValue, 0, Mathf.Max(0, GetLastTutorialPage(comp)));
            if (!comp.tutorialSkipped && !comp.tutorialCompleted) DrawTutorialInspector(comp);
            else DrawCompletedInspector(comp);
            serializedObject.ApplyModifiedProperties();
            DrawLanguageSelector();
        }

        private void DrawTutorialInspector(Manaco comp)
        {
            int page = _tutorialPageProp.intValue;
            int maxPage = GetLastTutorialPage(comp);
            var step = GetTutorialStep(comp, page);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Manaco Tutorial", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("スキップ", GUILayout.Width(100f)))
            {
                _tutorialSkippedProp.boolValue = true;
                _tutorialCompletedProp.boolValue = false;
                _tutorialWarning = null;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"Step {page + 1} / {maxPage + 1}", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(_tutorialWarning)) EditorGUILayout.HelpBox(_tutorialWarning, MessageType.Warning);
            DrawTutorialStep(comp, step);
            EditorGUILayout.Space(10f);
            DrawTutorialNavigation(comp, step);
            EditorGUILayout.EndVertical();
        }

        private void DrawCompletedInspector(Manaco comp)
        {
            DrawStandardInspector(comp);
        }

        private void DrawStandardInspector(Manaco comp)
        {
            DrawModeField();
            EditorGUILayout.Space(8f);
            if ((Manaco.ManacoMode)_modeProp.enumValueIndex == Manaco.ManacoMode.EyeMaterialAssignment) DrawEyeMaterialAssignmentTop(comp);
            else DrawCopyEyeFromAvatarTop(comp);
            EditorGUILayout.Space(8f);
            EditorGUILayout.PropertyField(_useNdmfPreviewProp, new GUIContent(ManacoLocale.T("Toggle.NdmfPreview")));
            EditorGUI.BeginChangeCheck();
            bool useFastPreview = EditorGUILayout.Toggle(ManacoLocale.T("Toggle.FastPreview"), ManacoProjectSettings.UseFastPreview);
            if (EditorGUI.EndChangeCheck()) ManacoProjectSettings.UseFastPreview = useFastPreview;
            if (ManacoProjectSettings.UseFastPreview) EditorGUILayout.HelpBox(ManacoLocale.T("Message.FastPreviewWarning"), MessageType.Warning);
            EditorGUILayout.Space(8f);
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, ManacoLocale.T("Label.AdvancedSettings"), true, EditorStyles.foldoutHeader);
            if (_showAdvanced) DrawAdvanced(comp);
            DrawExpandedSelector(comp);
        }

        private void DrawAdvanced(Manaco comp)
        {
            if (GUILayout.Button("チュートリアルを最初からやり直す"))
            {
                _tutorialPageProp.intValue = 0;
                _tutorialSkippedProp.boolValue = false;
                _tutorialCompletedProp.boolValue = false;
                _tutorialWarning = null;
                _expandedRegionIndex = -1;
                _selectorCache.Clear();
                Repaint();
            }

            EditorGUILayout.Space(6f);
            for (int i = 0; i < _eyeRegionsProp.arraySize; i++)
            {
                bool deleted = ((Manaco.ManacoMode)_modeProp.enumValueIndex == Manaco.ManacoMode.CopyEyeFromAvatar)
                    ? ManacoEyeCopyDrawer.DrawCopyEyeRegionSummary(comp, _eyeRegionsProp, _eyeRegionsProp.GetArrayElementAtIndex(i), i, OnOpenRegionSelector)
                    : DrawEyeRegionSummary(comp, _eyeRegionsProp.GetArrayElementAtIndex(i), i);
                if (deleted) { _expandedRegionIndex = -1; _selectorCache.Clear(); break; }
            }
            EditorGUILayout.Space(4f);
            if (GUILayout.Button(ManacoLocale.T("Button.Add"))) _eyeRegionsProp.arraySize++;
        }

        private void DrawModeField()
        {
            var modeOptions = new[] { ManacoLocale.T("ManacoMode.EyeMaterialAssignment"), ManacoLocale.T("ManacoMode.CopyEyeFromAvatar"), };
            EditorGUI.BeginChangeCheck();
            int newModeIndex = EditorGUILayout.Popup(ManacoLocale.T("Label.Mode"), _modeProp.enumValueIndex, modeOptions);
            if (EditorGUI.EndChangeCheck()) { _modeProp.enumValueIndex = newModeIndex; _expandedRegionIndex = -1; _selectorCache.Clear(); }
        }

        private void DrawEyeMaterialAssignmentTop(Manaco comp)
        {
            EditorGUILayout.LabelField(ManacoLocale.T("Label.AvatarPreset"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyPreset"), _selectedPresetIndex, _presetNames);
            if (newIndex != _selectedPresetIndex) { _selectedPresetIndex = newIndex; if (_selectedPresetIndex > 0) ApplyPreset(comp, _availablePresets[_selectedPresetIndex - 1]); }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50f))) LoadPresets();
            EditorGUILayout.EndHorizontal();
            DrawIslandTutorials(comp);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(ManacoLocale.T("Label.CustomMaterial"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newShaderIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyMaterial"), _selectedShaderIndex, _shaderNames);
            if (newShaderIndex != _selectedShaderIndex) { _selectedShaderIndex = newShaderIndex; if (_selectedShaderIndex > 0) ApplyShader(comp, _availableShaders[_selectedShaderIndex - 1]); }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50f))) LoadShaders();
            EditorGUILayout.EndHorizontal();
            DrawMaterialList(comp);
        }

        private void DrawCopyEyeFromAvatarTop(Manaco comp)
        {
            EditorGUILayout.LabelField(ManacoLocale.T("Label.DestinationPreset"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyPreset"), _selectedPresetIndex, _presetNames);
            if (newIndex != _selectedPresetIndex) { _selectedPresetIndex = newIndex; if (_selectedPresetIndex > 0) ApplyPreset(comp, _availablePresets[_selectedPresetIndex - 1]); }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50f))) LoadPresets();
            EditorGUILayout.EndHorizontal();
            DrawIslandTutorials(comp, true, false);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(ManacoLocale.T("Label.SourceAvatar"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_sourceAvatarPrefabProp, new GUIContent("Avatar"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (comp.appliedSourceAvatarPreset != null && comp.sourceAvatarPrefab != null) RefreshSourceRenderers(comp);
                serializedObject.Update();
                _selectorCache.Clear();
            }
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(ManacoLocale.T("Label.SourcePreset"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newSourceIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyPreset"), _selectedSourcePresetIndex, _presetNames);
            if (newSourceIndex != _selectedSourcePresetIndex) { _selectedSourcePresetIndex = newSourceIndex; if (_selectedSourcePresetIndex > 0) ApplySourcePreset(comp, _availablePresets[_selectedSourcePresetIndex - 1]); }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50f))) LoadPresets();
            EditorGUILayout.EndHorizontal();
            DrawIslandTutorials(comp, false, true);
        }

        private void DrawMaterialList(Manaco comp)
        {
            bool hasAny = false;
            foreach (var region in comp.eyeRegions) if (region.customMaterial != null) { hasAny = true; break; }
            if (!hasAny) return;
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(ManacoLocale.T("Label.MaterialList"), EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginDisabledGroup(true);
            foreach (var region in comp.eyeRegions) if (region.customMaterial != null) EditorGUILayout.ObjectField(ManacoLocale.GetEyeTypeName(region.eyeType), region.customMaterial, typeof(Material), false);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        private bool DrawEyeRegionSummary(Manaco comp, SerializedProperty element, int index)
        {
            var eyeTypeProp = element.FindPropertyRelative("eyeType");
            var rendererProp = element.FindPropertyRelative("targetRenderer");
            var matIndexProp = element.FindPropertyRelative("materialIndex");
            var uvRectsProp = element.FindPropertyRelative("eyePolygonRegions");
            var customMaterialProp = element.FindPropertyRelative("customMaterial");
            var bakeFallbackProp = element.FindPropertyRelative("bakeFallbackTexture");
            var fallbackResolutionProp = element.FindPropertyRelative("fallbackTextureResolution");
            var smr = rendererProp.objectReferenceValue as SkinnedMeshRenderer;
            string eyeTypeStr = ManacoLocale.GetEyeTypeName((Manaco.EyeType)eyeTypeProp.enumValueIndex);
            string label = smr != null ? $"[{index}] ({eyeTypeStr}) ({smr.name})" : $"[{index}] ({eyeTypeStr})";
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (GUILayout.Button(ManacoLocale.T("Button.Delete"), GUILayout.Width(50f))) { _eyeRegionsProp.DeleteArrayElementAtIndex(index); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); return true; }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(eyeTypeProp, new GUIContent(ManacoLocale.T("Label.EyeType")));
            EditorGUILayout.PropertyField(rendererProp, new GUIContent(ManacoLocale.T("Label.Renderer")));
            EditorGUILayout.PropertyField(matIndexProp, new GUIContent(ManacoLocale.T("Label.MaterialSlot")));
            EditorGUILayout.PropertyField(customMaterialProp, new GUIContent(ManacoLocale.T("Label.Material")));
            if (customMaterialProp.objectReferenceValue == null) EditorGUILayout.HelpBox(ManacoLocale.T("Message.MaterialNotSet"), MessageType.Warning);
            EditorGUILayout.PropertyField(bakeFallbackProp, new GUIContent(ManacoLocale.T("Toggle.FallbackTexture"), ManacoLocale.T("Tooltip.FallbackTexture")));
            if (bakeFallbackProp.boolValue)
            {
                EditorGUI.indentLevel++;
                fallbackResolutionProp.intValue = EditorGUILayout.IntPopup(ManacoLocale.T("Label.Resolution"), fallbackResolutionProp.intValue, new[] { "64", "128", "256", "512", "1024", "2048" }, new[] { 64, 128, 256, 512, 1024, 2048 });
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(ManacoLocale.T("Message.UVIslandCount", uvRectsProp.arraySize), EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
            bool isOpen = _expandedRegionIndex == index && !_expandedRegionIsSource;
            if (GUILayout.Button(isOpen ? "UV 選択を閉じる" : "UV をインスペクター内で選択")) OnOpenRegionSelector(index, false);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
            return false;
        }

        private void DrawExpandedSelector(Manaco comp)
        {
            if (_expandedRegionIndex < 0 || _expandedRegionIndex >= comp.eyeRegions.Count) return;
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            string title = _expandedRegionIsSource ? $"{ManacoLocale.GetEyeTypeName(comp.eyeRegions[_expandedRegionIndex].eyeType)} コピー元 UV" : $"{ManacoLocale.GetEyeTypeName(comp.eyeRegions[_expandedRegionIndex].eyeType)} UV";
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GetSelector(comp, _expandedRegionIndex, _expandedRegionIsSource).DrawLayout();
            EditorGUILayout.EndVertical();
        }

        private void DrawIslandTutorials(Manaco comp, bool showDest = true, bool showSrc = true)
        {
            bool any = false;
            for (int i = 0; i < comp.eyeRegions.Count; i++)
            {
                var region = comp.eyeRegions[i];
                string eyeTypeName = ManacoLocale.GetEyeTypeName(region.eyeType);
                if (showDest && (region.eyePolygonRegions == null || region.eyePolygonRegions.Length == 0))
                {
                    if (!any) { EditorGUILayout.Space(4f); any = true; }
                    EditorGUILayout.HelpBox(ManacoLocale.T("Tutorial.SelectEyeType", eyeTypeName), MessageType.Info);
                    if (GUILayout.Button($"{eyeTypeName} を選択")) OnOpenRegionSelector(i, false);
                }
                if (showSrc && comp.mode == Manaco.ManacoMode.CopyEyeFromAvatar && (region.sourceEyePolygonRegions == null || region.sourceEyePolygonRegions.Length == 0))
                {
                    if (!any) { EditorGUILayout.Space(4f); any = true; }
                    EditorGUILayout.HelpBox(ManacoLocale.T("Tutorial.SelectSourceEyeType", eyeTypeName), MessageType.Info);
                    if (GUILayout.Button($"コピー元の {eyeTypeName} を選択")) OnOpenRegionSelector(i, true);
                }
            }
        }

        private void DrawTutorialStep(Manaco comp, TutorialStep step)
        {
            switch (step.Kind)
            {
                case TutorialStepKind.Mode:
                    EditorGUILayout.HelpBox("まず使いたいモードを選んでください。", MessageType.Info);
                    DrawModeField();
                    break;
                case TutorialStepKind.DestinationPreset:
                    EditorGUILayout.HelpBox("対象アバターのプリセットを選んでください。", MessageType.Info);
                    if ((Manaco.ManacoMode)_modeProp.enumValueIndex == Manaco.ManacoMode.EyeMaterialAssignment) DrawEyeMaterialAssignmentPresetOnly(comp);
                    else DrawCopyDestinationPresetOnly(comp);
                    break;
                case TutorialStepKind.DestinationIsland:
                    DrawTutorialIslandStep(comp, step.RegionIndex, false);
                    break;
                case TutorialStepKind.MaterialSetup:
                    EditorGUILayout.HelpBox("使うカスタムマテリアルを選んでください。", MessageType.Info);
                    DrawMaterialSetupOnly(comp);
                    break;
                case TutorialStepKind.SourceSetup:
                    EditorGUILayout.HelpBox("コピー元アバターとコピー元プリセットを選んでください。", MessageType.Info);
                    DrawSourceSetupOnly(comp);
                    break;
                case TutorialStepKind.SourceIsland:
                    DrawTutorialIslandStep(comp, step.RegionIndex, true);
                    break;
            }
        }

        private void DrawEyeMaterialAssignmentPresetOnly(Manaco comp)
        {
            EditorGUILayout.LabelField(ManacoLocale.T("Label.AvatarPreset"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyPreset"), _selectedPresetIndex, _presetNames);
            if (newIndex != _selectedPresetIndex) { _selectedPresetIndex = newIndex; if (_selectedPresetIndex > 0) ApplyPreset(comp, _availablePresets[_selectedPresetIndex - 1]); }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50f))) LoadPresets();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCopyDestinationPresetOnly(Manaco comp)
        {
            EditorGUILayout.LabelField(ManacoLocale.T("Label.DestinationPreset"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyPreset"), _selectedPresetIndex, _presetNames);
            if (newIndex != _selectedPresetIndex) { _selectedPresetIndex = newIndex; if (_selectedPresetIndex > 0) ApplyPreset(comp, _availablePresets[_selectedPresetIndex - 1]); }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50f))) LoadPresets();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMaterialSetupOnly(Manaco comp)
        {
            EditorGUILayout.LabelField(ManacoLocale.T("Label.CustomMaterial"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newShaderIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyMaterial"), _selectedShaderIndex, _shaderNames);
            if (newShaderIndex != _selectedShaderIndex) { _selectedShaderIndex = newShaderIndex; if (_selectedShaderIndex > 0) ApplyShader(comp, _availableShaders[_selectedShaderIndex - 1]); }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50f))) LoadShaders();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8f);
            EditorGUILayout.PropertyField(_useNdmfPreviewProp, new GUIContent(ManacoLocale.T("Toggle.NdmfPreview")));
            EditorGUI.BeginChangeCheck();
            bool useFastPreview = EditorGUILayout.Toggle(ManacoLocale.T("Toggle.FastPreview"), ManacoProjectSettings.UseFastPreview);
            if (EditorGUI.EndChangeCheck()) ManacoProjectSettings.UseFastPreview = useFastPreview;
            if (ManacoProjectSettings.UseFastPreview) EditorGUILayout.HelpBox(ManacoLocale.T("Message.FastPreviewWarning"), MessageType.Warning);
            DrawMaterialList(comp);
        }

        private void DrawSourceSetupOnly(Manaco comp)
        {
            EditorGUILayout.LabelField(ManacoLocale.T("Label.SourceAvatar"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_sourceAvatarPrefabProp, new GUIContent("Avatar"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (comp.appliedSourceAvatarPreset != null && comp.sourceAvatarPrefab != null) RefreshSourceRenderers(comp);
                serializedObject.Update();
                _selectorCache.Clear();
            }
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(ManacoLocale.T("Label.SourcePreset"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newSourceIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyPreset"), _selectedSourcePresetIndex, _presetNames);
            if (newSourceIndex != _selectedSourcePresetIndex) { _selectedSourcePresetIndex = newSourceIndex; if (_selectedSourcePresetIndex > 0) ApplySourcePreset(comp, _availablePresets[_selectedSourcePresetIndex - 1]); }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50f))) LoadPresets();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTutorialIslandStep(Manaco comp, int regionIndex, bool isSource)
        {
            if (regionIndex < 0 || regionIndex >= comp.eyeRegions.Count) { EditorGUILayout.HelpBox("プリセットを先に選択してください。", MessageType.Warning); return; }
            var region = comp.eyeRegions[regionIndex];
            string eyeTypeName = ManacoLocale.GetEyeTypeName(region.eyeType);
            EditorGUILayout.HelpBox(isSource ? $"コピー元の {eyeTypeName} のアイランドを選択してください。" : $"{eyeTypeName} のアイランドを選択してください。", MessageType.Info);
            GetSelector(comp, regionIndex, isSource).DrawLayout(460f, true);
        }

        private void DrawTutorialNavigation(Manaco comp, TutorialStep step)
        {
            EditorGUILayout.BeginHorizontal();
            if (step.PageIndex > 0)
            {
                if (GUILayout.Button("戻る", GUILayout.Width(100f))) { _tutorialPageProp.intValue = Mathf.Max(0, step.PageIndex - 1); _tutorialWarning = null; }
            }
            else GUILayout.Space(104f);
            GUILayout.FlexibleSpace();
            bool isFinalStep = step.PageIndex >= GetLastTutorialPage(comp);
            string nextLabel = isFinalStep ? "完了" : "次へ";
            if (GUILayout.Button(nextLabel, GUILayout.Width(120f)))
            {
                if (TryAdvanceTutorial(comp, step))
                {
                    _tutorialWarning = null;
                    if (isFinalStep) { _tutorialCompletedProp.boolValue = true; _tutorialSkippedProp.boolValue = false; }
                    else _tutorialPageProp.intValue = Mathf.Min(GetLastTutorialPage(comp), step.PageIndex + 1);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool TryAdvanceTutorial(Manaco comp, TutorialStep step)
        {
            switch (step.Kind)
            {
                case TutorialStepKind.Mode:
                    return true;
                case TutorialStepKind.DestinationPreset:
                    if (comp.appliedAvatarPreset == null || comp.eyeRegions.Count == 0) { _tutorialWarning = "対象のアバタープリセットを選択してください。"; return false; }
                    return true;
                case TutorialStepKind.DestinationIsland:
                    if (!HasSelectedIsland(comp, step.RegionIndex, false))
                    {
                        bool stay = EditorUtility.DisplayDialog(
                            "未選択の確認",
                            "選択されてません、選択しないと正しく反映されない可能性があります",
                            "選択する",
                            "次へ進む");
                        if (stay)
                            return false;
                    }
                    return true;
                case TutorialStepKind.MaterialSetup:
                    if (comp.appliedShaderDef == null) { _tutorialWarning = "カスタムマテリアルを選択してください。"; return false; }
                    return true;
                case TutorialStepKind.SourceSetup:
                    if (comp.sourceAvatarPrefab == null) { _tutorialWarning = "コピー元アバターを選択してください。"; return false; }
                    if (comp.appliedSourceAvatarPreset == null) { _tutorialWarning = "コピー元プリセットを選択してください。"; return false; }
                    return true;
                case TutorialStepKind.SourceIsland:
                    if (!HasSelectedIsland(comp, step.RegionIndex, true))
                    {
                        bool stay = EditorUtility.DisplayDialog(
                            "未選択の確認",
                            "選択されてません、選択しないと正しく反映されない可能性があります",
                            "選択する",
                            "次へ進む");
                        if (stay)
                            return false;
                    }
                    return true;
            }
            return true;
        }

        private bool HasSelectedIsland(Manaco comp, int regionIndex, bool isSource)
        {
            if (regionIndex < 0 || regionIndex >= comp.eyeRegions.Count) return false;
            var regions = isSource ? comp.eyeRegions[regionIndex].sourceEyePolygonRegions : comp.eyeRegions[regionIndex].eyePolygonRegions;
            return regions != null && regions.Length > 0;
        }

        private void OnOpenRegionSelector(int regionIndex, bool isSource)
        {
            if (_expandedRegionIndex == regionIndex && _expandedRegionIsSource == isSource) { _expandedRegionIndex = -1; return; }
            _expandedRegionIndex = regionIndex;
            _expandedRegionIsSource = isSource;
        }

        private ManacoEmbeddedUvSelector GetSelector(Manaco comp, int regionIndex, bool isSource)
        {
            string key = $"{regionIndex}:{isSource}:{comp.GetInstanceID()}";
            if (!_selectorCache.TryGetValue(key, out var selector)) { selector = new ManacoEmbeddedUvSelector(comp, regionIndex, isSource); _selectorCache[key] = selector; }
            return selector;
        }

        private int GetLastTutorialPage(Manaco comp)
        {
            int pageCount = 2 + comp.eyeRegions.Count + 1;
            if ((Manaco.ManacoMode)_modeProp.enumValueIndex == Manaco.ManacoMode.CopyEyeFromAvatar) pageCount += comp.eyeRegions.Count + 1;
            return Mathf.Max(0, pageCount - 1);
        }

        private TutorialStep GetTutorialStep(Manaco comp, int pageIndex)
        {
            int destCount = comp.eyeRegions.Count;
            int currentPage = 0;
            if (pageIndex == currentPage) return new TutorialStep(TutorialStepKind.Mode, pageIndex);
            currentPage++;
            if (pageIndex == currentPage) return new TutorialStep(TutorialStepKind.DestinationPreset, pageIndex);
            currentPage++;
            for (int i = 0; i < destCount; i++, currentPage++) if (pageIndex == currentPage) return new TutorialStep(TutorialStepKind.DestinationIsland, pageIndex, i);
            if ((Manaco.ManacoMode)_modeProp.enumValueIndex == Manaco.ManacoMode.EyeMaterialAssignment)
            {
                if (pageIndex == currentPage) return new TutorialStep(TutorialStepKind.MaterialSetup, pageIndex);
                return new TutorialStep(TutorialStepKind.MaterialSetup, currentPage);
            }
            if (pageIndex == currentPage) return new TutorialStep(TutorialStepKind.SourceSetup, pageIndex);
            currentPage++;
            for (int i = 0; i < destCount; i++, currentPage++) if (pageIndex == currentPage) return new TutorialStep(TutorialStepKind.SourceIsland, pageIndex, i);
            return new TutorialStep(TutorialStepKind.SourceIsland, Mathf.Max(0, currentPage - 1), Mathf.Max(0, destCount - 1));
        }

        private void LoadPresets()
        {
            string[] guids = AssetDatabase.FindAssets("t:ManacoPreset");
            _availablePresets = new ManacoPreset[guids.Length];
            _presetNames = new string[guids.Length + 1];
            _presetNames[0] = ManacoLocale.T("Prompt.SelectPreset");
            var comp = (Manaco)target;
            _selectedPresetIndex = 0;
            _selectedSourcePresetIndex = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availablePresets[i] = AssetDatabase.LoadAssetAtPath<ManacoPreset>(path);
                _presetNames[i + 1] = _availablePresets[i].avatarName;
                if (comp.appliedAvatarPreset == _availablePresets[i]) _selectedPresetIndex = i + 1;
                if (comp.appliedSourceAvatarPreset == _availablePresets[i]) _selectedSourcePresetIndex = i + 1;
            }
        }

        private void LoadShaders()
        {
            string[] guids = AssetDatabase.FindAssets("t:ManacoMaterialDefinition");
            _availableShaders = new ManacoMaterialDefinition[guids.Length];
            _shaderNames = new string[guids.Length + 1];
            _shaderNames[0] = ManacoLocale.T("Prompt.SelectMaterial");
            var comp = (Manaco)target;
            _selectedShaderIndex = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availableShaders[i] = AssetDatabase.LoadAssetAtPath<ManacoMaterialDefinition>(path);
                _shaderNames[i + 1] = _availableShaders[i].name;
                if (comp.appliedShaderDef == _availableShaders[i]) _selectedShaderIndex = i + 1;
            }
        }

        private void ApplyPreset(Manaco comp, ManacoPreset preset)
        {
            Undo.RecordObject(comp, "Apply Manaco Preset");
            comp.appliedAvatarPreset = preset;
            comp.eyeRegions.Clear();
            _selectorCache.Clear();
            _expandedRegionIndex = -1;
            Transform searchRoot = comp.transform;
            var descriptor = comp.GetComponentInParent<VRC.SDKBase.VRC_AvatarDescriptor>();
            if (descriptor != null) searchRoot = descriptor.transform;
            else
            {
                var animator = comp.GetComponentInParent<Animator>();
                searchRoot = animator != null ? animator.transform : comp.transform.root;
            }
            var renderers = searchRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var presetRegion in preset.regions)
            {
                var region = new Manaco.EyeRegion
                {
                    eyeType = presetRegion.eyeType,
                    materialIndex = presetRegion.materialIndex,
                    eyePolygonRegions = ClonePolygonRegions(presetRegion.eyePolygonRegions)
                };
                foreach (var smr in renderers) if (smr.name == presetRegion.targetRendererName) { region.targetRenderer = smr; break; }
                comp.eyeRegions.Add(region);
            }
            if (comp.appliedShaderDef != null) ApplyShader(comp, comp.appliedShaderDef);
            serializedObject.Update();
            EditorUtility.SetDirty(comp);
        }

        private void ApplyShader(Manaco comp, ManacoMaterialDefinition shaderDef)
        {
            Undo.RecordObject(comp, "Apply Manaco Shader Definition");
            comp.appliedShaderDef = shaderDef;
            foreach (var region in comp.eyeRegions)
            {
                region.customMaterial = region.eyeType switch
                {
                    Manaco.EyeType.Left => shaderDef.leftEyeMaterial,
                    Manaco.EyeType.Right => shaderDef.rightEyeMaterial,
                    Manaco.EyeType.LeftPupil => shaderDef.leftPupilMaterial,
                    Manaco.EyeType.RightPupil => shaderDef.rightPupilMaterial,
                    _ => null,
                };
            }
            serializedObject.Update();
            EditorUtility.SetDirty(comp);
        }

        private void ApplySourcePreset(Manaco comp, ManacoPreset sourcePreset)
        {
            Undo.RecordObject(comp, "Apply Source Preset");
            comp.appliedSourceAvatarPreset = sourcePreset;
            _selectorCache.Clear();
            int count = Mathf.Min(comp.eyeRegions.Count, sourcePreset.regions.Count);
            for (int i = 0; i < count; i++)
            {
                var region = comp.eyeRegions[i];
                var presetRegion = sourcePreset.regions[i];
                region.sourceMaterialIndex = presetRegion.materialIndex;
                region.sourceEyePolygonRegions = ClonePolygonRegions(presetRegion.eyePolygonRegions);
                if (comp.sourceAvatarPrefab == null) continue;
                var renderers = comp.sourceAvatarPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in renderers) if (smr.name == presetRegion.targetRendererName) { region.sourceRenderer = smr; break; }
            }
            serializedObject.Update();
            EditorUtility.SetDirty(comp);
        }

        private void RefreshSourceRenderers(Manaco comp)
        {
            var preset = comp.appliedSourceAvatarPreset;
            if (preset == null || comp.sourceAvatarPrefab == null) return;
            var renderers = comp.sourceAvatarPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int count = Mathf.Min(comp.eyeRegions.Count, preset.regions.Count);
            for (int i = 0; i < count; i++)
            {
                var region = comp.eyeRegions[i];
                var presetRegion = preset.regions[i];
                foreach (var smr in renderers) if (smr.name == presetRegion.targetRendererName) { region.sourceRenderer = smr; break; }
            }
            EditorUtility.SetDirty(comp);
        }

        private static Manaco.UVPolygonRegion[] ClonePolygonRegions(Manaco.UVPolygonRegion[] source)
        {
            if (source == null) return System.Array.Empty<Manaco.UVPolygonRegion>();
            var cloned = new Manaco.UVPolygonRegion[source.Length];
            for (int i = 0; i < source.Length; i++)
                cloned[i] = new Manaco.UVPolygonRegion { uvPoints = source[i].uvPoints != null ? (Vector2[])source[i].uvPoints.Clone() : System.Array.Empty<Vector2>() };
            return cloned;
        }

        private void DrawLanguageSelector()
        {
            EditorGUILayout.Space(8f);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true)), new Color(0.3f, 0.3f, 0.3f));
            EditorGUILayout.Space(4f);
            var (codes, names) = ManacoLocale.GetAvailableLanguages();
            int languageIndex = System.Array.IndexOf(codes, ManacoLocale.CurrentLanguageCode);
            if (languageIndex < 0) languageIndex = 0;
            EditorGUI.BeginChangeCheck();
            int newLanguageIndex = EditorGUILayout.Popup(ManacoLocale.T("Prefs.Language"), languageIndex, names);
            if (EditorGUI.EndChangeCheck() && newLanguageIndex >= 0 && newLanguageIndex < codes.Length) ManacoLocale.SetLanguage(codes[newLanguageIndex]);
        }

        private readonly struct TutorialStep
        {
            public readonly TutorialStepKind Kind;
            public readonly int PageIndex;
            public readonly int RegionIndex;
            public TutorialStep(TutorialStepKind kind, int pageIndex, int regionIndex = -1) { Kind = kind; PageIndex = pageIndex; RegionIndex = regionIndex; }
        }

        private enum TutorialStepKind { Mode, DestinationPreset, DestinationIsland, MaterialSetup, SourceSetup, SourceIsland }
    }
}
