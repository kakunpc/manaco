using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    [CustomEditor(typeof(Manaco))]
    public class ManacoEditor : UnityEditor.Editor
    {
        private SerializedProperty _eyeRegionsProp;
        private SerializedProperty _useNdmfPreviewProp;
        private SerializedProperty _modeProp;
        private SerializedProperty _sourceAvatarPrefabProp;

        private void OnEnable()
        {
            _eyeRegionsProp        = serializedObject.FindProperty("eyeRegions");
            _useNdmfPreviewProp    = serializedObject.FindProperty("useNdmfPreview");
            _modeProp              = serializedObject.FindProperty("mode");
            _sourceAvatarPrefabProp = serializedObject.FindProperty("sourceAvatarPrefab");
            LoadPresets();
            LoadShaders();
        }

        // ---- プリセット（コピー先 / コピー元で同じリストを共用） ----
        private ManacoPreset[] _availablePresets;
        private string[] _presetNames;
        private int _selectedPresetIndex       = 0;
        private int _selectedSourcePresetIndex = 0;

        private void LoadPresets()
        {
            string[] guids = AssetDatabase.FindAssets("t:ManacoPreset");
            _availablePresets = new ManacoPreset[guids.Length];
            _presetNames = new string[guids.Length + 1];
            _presetNames[0] = ManacoLocale.T("Prompt.SelectPreset");

            var comp = target as Manaco;
            _selectedPresetIndex       = 0;
            _selectedSourcePresetIndex = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availablePresets[i] = AssetDatabase.LoadAssetAtPath<ManacoPreset>(path);
                _presetNames[i + 1] = _availablePresets[i].avatarName;

                if (comp != null && comp.appliedAvatarPreset == _availablePresets[i])
                    _selectedPresetIndex = i + 1;
                if (comp != null && comp.appliedSourceAvatarPreset == _availablePresets[i])
                    _selectedSourcePresetIndex = i + 1;
            }
        }

        // ---- シェーダー定義 ----
        private ManacoMaterialDefinition[] _availableShaders;
        private string[] _shaderNames;
        private int _selectedShaderIndex = 0;

        private void LoadShaders()
        {
            string[] guids = AssetDatabase.FindAssets("t:ManacoMaterialDefinition");
            _availableShaders = new ManacoMaterialDefinition[guids.Length];
            _shaderNames = new string[guids.Length + 1];
            _shaderNames[0] = ManacoLocale.T("Prompt.SelectMaterial");

            var comp = target as Manaco;
            _selectedShaderIndex = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availableShaders[i] = AssetDatabase.LoadAssetAtPath<ManacoMaterialDefinition>(path);
                _shaderNames[i + 1] = _availableShaders[i].name;

                if (comp != null && comp.appliedShaderDef == _availableShaders[i])
                    _selectedShaderIndex = i + 1;
            }
        }

        // ================================================================
        //  OnInspectorGUI
        // ================================================================

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // モード選択（ローカライズ済みポップアップ）
            var modeOptions = new[]
            {
                ManacoLocale.T("ManacoMode.EyeMaterialAssignment"),
                ManacoLocale.T("ManacoMode.CopyEyeFromAvatar"),
            };
            EditorGUI.BeginChangeCheck();
            int newModeIdx = EditorGUILayout.Popup(
                ManacoLocale.T("Label.Mode"), _modeProp.enumValueIndex, modeOptions);
            if (EditorGUI.EndChangeCheck())
                _modeProp.enumValueIndex = newModeIdx;
            var currentMode = (Manaco.ManacoMode)_modeProp.enumValueIndex;

            EditorGUILayout.Space(8);

            if (currentMode == Manaco.ManacoMode.EyeMaterialAssignment)
                DrawEyeMaterialAssignmentTop();
            else
                DrawCopyEyeFromAvatarTop();

            EditorGUILayout.Space(8);

            EditorGUILayout.PropertyField(_useNdmfPreviewProp,
                new GUIContent(ManacoLocale.T("Toggle.NdmfPreview")));

            EditorGUILayout.Space(8);

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced,
                ManacoLocale.T("Label.AdvancedSettings"), true, EditorStyles.foldoutHeader);
            if (_showAdvanced)
            {
                var comp = (Manaco)target;

                for (int i = 0; i < _eyeRegionsProp.arraySize; i++)
                {
                    bool deleted;
                    if (currentMode == Manaco.ManacoMode.CopyEyeFromAvatar)
                        deleted = ManacoEyeCopyDrawer.DrawCopyEyeRegionSummary(
                            comp, _eyeRegionsProp, _eyeRegionsProp.GetArrayElementAtIndex(i), i);
                    else
                        deleted = DrawEyeRegionSummary(comp, _eyeRegionsProp.GetArrayElementAtIndex(i), i);

                    if (deleted) break;
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button(ManacoLocale.T("Button.Add")))
                    _eyeRegionsProp.arraySize++;
            }

            serializedObject.ApplyModifiedProperties();

            // 言語セレクター
            EditorGUILayout.Space(8);
            EditorGUI.DrawRect(
                GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)),
                new Color(0.3f, 0.3f, 0.3f));
            EditorGUILayout.Space(4);

            var (codes, names) = ManacoLocale.GetAvailableLanguages();
            int langIdx = System.Array.IndexOf(codes, ManacoLocale.CurrentLanguageCode);
            if (langIdx < 0) langIdx = 0;
            EditorGUI.BeginChangeCheck();
            int newLangIdx = EditorGUILayout.Popup(ManacoLocale.T("Prefs.Language"), langIdx, names);
            if (EditorGUI.EndChangeCheck() && newLangIdx >= 0 && newLangIdx < codes.Length)
                ManacoLocale.SetLanguage(codes[newLangIdx]);
        }

        // ----------------------------------------------------------------
        //  EyeMaterialAssignment モードのトップセクション
        // ----------------------------------------------------------------

        private void DrawEyeMaterialAssignmentTop()
        {
            EditorGUILayout.LabelField(ManacoLocale.T("Label.AvatarPreset"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyPreset"),
                _selectedPresetIndex, _presetNames);
            if (newIndex != _selectedPresetIndex)
            {
                _selectedPresetIndex = newIndex;
                if (_selectedPresetIndex > 0)
                    ApplyPreset((Manaco)target, _availablePresets[_selectedPresetIndex - 1]);
            }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50)))
                LoadPresets();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(ManacoLocale.T("Label.CustomMaterial"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            int newShaderIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyMaterial"),
                _selectedShaderIndex, _shaderNames);
            if (newShaderIndex != _selectedShaderIndex)
            {
                _selectedShaderIndex = newShaderIndex;
                if (_selectedShaderIndex > 0)
                    ApplyShader((Manaco)target, _availableShaders[_selectedShaderIndex - 1]);
            }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50)))
                LoadShaders();
            EditorGUILayout.EndHorizontal();
        }

        // ----------------------------------------------------------------
        //  CopyEyeFromAvatar モードのトップセクション
        // ----------------------------------------------------------------

        private void DrawCopyEyeFromAvatarTop()
        {
            // アバタープリセット（コピー先）
            EditorGUILayout.LabelField(ManacoLocale.T("Label.DestinationPreset"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyPreset"),
                _selectedPresetIndex, _presetNames);
            if (newIndex != _selectedPresetIndex)
            {
                _selectedPresetIndex = newIndex;
                if (_selectedPresetIndex > 0)
                    ApplyPreset((Manaco)target, _availablePresets[_selectedPresetIndex - 1]);
            }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50)))
                LoadPresets();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // コピー元のアバター
            EditorGUILayout.LabelField(ManacoLocale.T("Label.SourceAvatar"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_sourceAvatarPrefabProp, new GUIContent("Avatar"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                var comp = (Manaco)target;
                if (comp.appliedSourceAvatarPreset != null && comp.sourceAvatarPrefab != null)
                    RefreshSourceRenderers(comp);
                serializedObject.Update();
            }

            EditorGUILayout.Space(8);

            // コピー元のアバタープリセット
            EditorGUILayout.LabelField(ManacoLocale.T("Label.SourcePreset"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            int newSourceIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyPreset"),
                _selectedSourcePresetIndex, _presetNames);
            if (newSourceIndex != _selectedSourcePresetIndex)
            {
                _selectedSourcePresetIndex = newSourceIndex;
                if (_selectedSourcePresetIndex > 0)
                    ApplySourcePreset((Manaco)target, _availablePresets[_selectedSourcePresetIndex - 1]);
            }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50)))
                LoadPresets();
            EditorGUILayout.EndHorizontal();
        }

        // ================================================================
        //  プリセット適用
        // ================================================================

        private void ApplyPreset(Manaco comp, ManacoPreset preset)
        {
            Undo.RecordObject(comp, "Apply Manaco Preset");

            comp.appliedAvatarPreset = preset;
            comp.eyeRegions.Clear();

            Transform searchRoot = comp.transform;
            var descriptor = comp.GetComponentInParent<VRC.SDKBase.VRC_AvatarDescriptor>();
            if (descriptor != null) searchRoot = descriptor.transform;
            else
            {
                var animator = comp.GetComponentInParent<Animator>();
                if (animator != null) searchRoot = animator.transform;
                else searchRoot = comp.transform.root;
            }

            var renderers = searchRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (var pr in preset.regions)
            {
                var region = new Manaco.EyeRegion
                {
                    eyeType      = pr.eyeType,
                    materialIndex = pr.materialIndex,
                    eyePolygonRegions = new Manaco.UVPolygonRegion[pr.eyePolygonRegions.Length]
                };
                for (int i = 0; i < pr.eyePolygonRegions.Length; i++)
                {
                    region.eyePolygonRegions[i] = new Manaco.UVPolygonRegion
                    {
                        uvPoints = pr.eyePolygonRegions[i].uvPoints.Clone() as Vector2[]
                    };
                }

                foreach (var smr in renderers)
                {
                    if (smr.name == pr.targetRendererName)
                    {
                        region.targetRenderer = smr;
                        break;
                    }
                }

                comp.eyeRegions.Add(region);
            }

            if (comp.appliedShaderDef != null)
                ApplyShader(comp, comp.appliedShaderDef);

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
                    Manaco.EyeType.Left       => shaderDef.leftEyeMaterial,
                    Manaco.EyeType.Right      => shaderDef.rightEyeMaterial,
                    Manaco.EyeType.Both       => shaderDef.bothEyeMaterial,
                    Manaco.EyeType.LeftPupil  => shaderDef.leftPupilMaterial,
                    Manaco.EyeType.RightPupil => shaderDef.rightPupilMaterial,
                    Manaco.EyeType.BothPupil  => shaderDef.bothPupilMaterial,
                    _                         => null,
                };
            }

            serializedObject.Update();
            EditorUtility.SetDirty(comp);
        }

        /// <summary>
        /// ソースプリセットを適用する。
        /// 各 EyeRegion にソース UV Island とマテリアルスロットをコピーし、
        /// sourceAvatarPrefab が設定済みなら sourceRenderer も設定する。
        /// </summary>
        private void ApplySourcePreset(Manaco comp, ManacoPreset sourcePreset)
        {
            Undo.RecordObject(comp, "Apply Source Preset");

            comp.appliedSourceAvatarPreset = sourcePreset;

            int count = Mathf.Min(comp.eyeRegions.Count, sourcePreset.regions.Count);
            for (int i = 0; i < count; i++)
            {
                var region     = comp.eyeRegions[i];
                var presetRegion = sourcePreset.regions[i];

                region.sourceMaterialIndex = presetRegion.materialIndex;
                region.sourceEyePolygonRegions =
                    new Manaco.UVPolygonRegion[presetRegion.eyePolygonRegions.Length];
                for (int j = 0; j < presetRegion.eyePolygonRegions.Length; j++)
                {
                    region.sourceEyePolygonRegions[j] = new Manaco.UVPolygonRegion
                    {
                        uvPoints = presetRegion.eyePolygonRegions[j].uvPoints.Clone() as Vector2[]
                    };
                }

                if (comp.sourceAvatarPrefab != null)
                {
                    var renderers = comp.sourceAvatarPrefab
                        .GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    foreach (var smr in renderers)
                    {
                        if (smr.name == presetRegion.targetRendererName)
                        {
                            region.sourceRenderer = smr;
                            break;
                        }
                    }
                }
            }

            serializedObject.Update();
            EditorUtility.SetDirty(comp);
        }

        /// <summary>
        /// sourceAvatarPrefab が変わった時に sourceRenderer を再設定する。
        /// </summary>
        private void RefreshSourceRenderers(Manaco comp)
        {
            var preset = comp.appliedSourceAvatarPreset;
            if (preset == null || comp.sourceAvatarPrefab == null) return;

            var renderers = comp.sourceAvatarPrefab
                .GetComponentsInChildren<SkinnedMeshRenderer>(true);

            int count = Mathf.Min(comp.eyeRegions.Count, preset.regions.Count);
            for (int i = 0; i < count; i++)
            {
                var region     = comp.eyeRegions[i];
                var presetRegion = preset.regions[i];
                foreach (var smr in renderers)
                {
                    if (smr.name == presetRegion.targetRendererName)
                    {
                        region.sourceRenderer = smr;
                        break;
                    }
                }
            }

            EditorUtility.SetDirty(comp);
        }

        // ================================================================
        //  EyeMaterialAssignment モード用の EyeRegion 描画
        // ================================================================

        private bool DrawEyeRegionSummary(Manaco comp, SerializedProperty element, int index)
        {
            var eyeTypeProp            = element.FindPropertyRelative("eyeType");
            var rendererProp           = element.FindPropertyRelative("targetRenderer");
            var matIndexProp           = element.FindPropertyRelative("materialIndex");
            var uvRectsProp            = element.FindPropertyRelative("eyePolygonRegions");
            var customMaterialProp     = element.FindPropertyRelative("customMaterial");
            var bakeFallbackProp       = element.FindPropertyRelative("bakeFallbackTexture");
            var fallbackResolutionProp = element.FindPropertyRelative("fallbackTextureResolution");

            var smr = rendererProp.objectReferenceValue as SkinnedMeshRenderer;
            var eyeTypeEnum = (Manaco.EyeType)eyeTypeProp.enumValueIndex;
            string eyeTypeStr = eyeTypeEnum switch
            {
                Manaco.EyeType.Both       => ManacoLocale.T("EyeType.Both"),
                Manaco.EyeType.Left       => ManacoLocale.T("EyeType.Left"),
                Manaco.EyeType.Right      => ManacoLocale.T("EyeType.Right"),
                Manaco.EyeType.BothPupil  => ManacoLocale.T("EyeType.BothPupil"),
                Manaco.EyeType.LeftPupil  => ManacoLocale.T("EyeType.LeftPupil"),
                Manaco.EyeType.RightPupil => ManacoLocale.T("EyeType.RightPupil"),
                _                         => eyeTypeEnum.ToString(),
            };
            string label = smr != null
                ? $"[{index}] ({eyeTypeStr})  ({smr.name})"
                : $"[{index}]({eyeTypeStr})";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (GUILayout.Button(ManacoLocale.T("Button.Delete"), GUILayout.Width(50)))
            {
                _eyeRegionsProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(eyeTypeProp,        new GUIContent(ManacoLocale.T("Label.EyeType")));
            EditorGUILayout.PropertyField(rendererProp,       new GUIContent(ManacoLocale.T("Label.Renderer")));
            EditorGUILayout.PropertyField(matIndexProp,       new GUIContent(ManacoLocale.T("Label.MaterialSlot")));
            EditorGUILayout.PropertyField(customMaterialProp, new GUIContent(ManacoLocale.T("Label.Material")));

            if (customMaterialProp.objectReferenceValue == null)
                EditorGUILayout.HelpBox(ManacoLocale.T("Message.MaterialNotSet"), MessageType.Warning);

            EditorGUILayout.PropertyField(bakeFallbackProp,
                new GUIContent(
                    ManacoLocale.T("Toggle.FallbackTexture"),
                    ManacoLocale.T("Tooltip.FallbackTexture")));
            if (bakeFallbackProp.boolValue)
            {
                EditorGUI.indentLevel++;
                fallbackResolutionProp.intValue = EditorGUILayout.IntPopup(
                    ManacoLocale.T("Label.Resolution"),
                    fallbackResolutionProp.intValue,
                    new[] { "64", "128", "256", "512", "1024", "2048" },
                    new[] { 64, 128, 256, 512, 1024, 2048 });
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(
                ManacoLocale.T("Message.UVIslandCount", uvRectsProp.arraySize),
                EditorStyles.miniLabel);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(2);

            if (GUILayout.Button(ManacoLocale.T("Button.OpenUVEditor")))
                ManacoWindow.OpenWith(comp, index);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            return false;
        }

        private bool _showAdvanced = false;
    }
}
