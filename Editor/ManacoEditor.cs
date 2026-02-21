using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    [CustomEditor(typeof(Manaco))]
    public class ManacoEditor : UnityEditor.Editor
    {
        private SerializedProperty _eyeRegionsProp;
        private SerializedProperty _useNdmfPreviewProp;

        private void OnEnable()
        {
            _eyeRegionsProp = serializedObject.FindProperty("eyeRegions");
            _useNdmfPreviewProp = serializedObject.FindProperty("useNdmfPreview");
            LoadPresets();
            LoadShaders();
        }

        private ManacoPreset[] _availablePresets;
        private string[] _presetNames;
        private int _selectedPresetIndex = 0;

        private void LoadPresets()
        {
            string[] guids = AssetDatabase.FindAssets("t:ManacoPreset");
            _availablePresets = new ManacoPreset[guids.Length];
            _presetNames = new string[guids.Length + 1];
            _presetNames[0] = ManacoLocale.T("Prompt.SelectPreset");

            var comp = target as Manaco;
            _selectedPresetIndex = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availablePresets[i] = AssetDatabase.LoadAssetAtPath<ManacoPreset>(path);
                _presetNames[i + 1] = _availablePresets[i].avatarName;

                if (comp != null && comp.appliedAvatarPreset == _availablePresets[i])
                    _selectedPresetIndex = i + 1;
            }
        }

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

        private ManacoPreset _selectedPreset;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField(ManacoLocale.T("Label.AvatarPreset"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyPreset"), _selectedPresetIndex, _presetNames);
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
            int newShaderIndex = EditorGUILayout.Popup(ManacoLocale.T("Popup.ApplyMaterial"), _selectedShaderIndex, _shaderNames);
            if (newShaderIndex != _selectedShaderIndex)
            {
                _selectedShaderIndex = newShaderIndex;
                if (_selectedShaderIndex > 0)
                    ApplyShader((Manaco)target, _availableShaders[_selectedShaderIndex - 1]);
            }
            if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(50)))
                LoadShaders();
            EditorGUILayout.EndHorizontal();

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
                    if (DrawEyeRegionSummary(comp, _eyeRegionsProp.GetArrayElementAtIndex(i), i))
                        break;
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button(ManacoLocale.T("Button.Add")))
                    _eyeRegionsProp.arraySize++;
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)), new Color(0.3f, 0.3f, 0.3f));
            EditorGUILayout.Space(4);

            var (codes, names) = ManacoLocale.GetAvailableLanguages();
            int langIdx = System.Array.IndexOf(codes, ManacoLocale.CurrentLanguageCode);
            if (langIdx < 0) langIdx = 0;
            EditorGUI.BeginChangeCheck();
            int newLangIdx = EditorGUILayout.Popup(ManacoLocale.T("Prefs.Language"), langIdx, names);
            if (EditorGUI.EndChangeCheck() && newLangIdx >= 0 && newLangIdx < codes.Length)
                ManacoLocale.SetLanguage(codes[newLangIdx]);
        }

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
                    eyeType = pr.eyeType,
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
                if (region.eyeType == Manaco.EyeType.Left)
                    region.customMaterial = shaderDef.leftEyeMaterial;
                else if (region.eyeType == Manaco.EyeType.Right)
                    region.customMaterial = shaderDef.rightEyeMaterial;
                else if (region.eyeType == Manaco.EyeType.Both)
                    region.customMaterial = shaderDef.bothEyeMaterial;
            }

            serializedObject.Update();
            EditorUtility.SetDirty(comp);
        }

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
            string eyeTypeStr = eyeTypeEnum == Manaco.EyeType.Both
                ? ManacoLocale.T("EyeType.Both")
                : (eyeTypeEnum == Manaco.EyeType.Left
                    ? ManacoLocale.T("EyeType.Left")
                    : ManacoLocale.T("EyeType.Right"));
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
