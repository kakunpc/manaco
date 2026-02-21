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
            _presetNames[0] = "--- プリセットを選択して適用 ---";

            var comp = target as Manaco;
            _selectedPresetIndex = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availablePresets[i] = AssetDatabase.LoadAssetAtPath<ManacoPreset>(path);
                _presetNames[i + 1] = _availablePresets[i].avatarName;

                if (comp != null && comp.appliedAvatarPreset == _availablePresets[i])
                {
                    _selectedPresetIndex = i + 1;
                }
            }
        }

        private ManacoShaderDefinition[] _availableShaders;
        private string[] _shaderNames;
        private int _selectedShaderIndex = 0;

        private void LoadShaders()
        {
            string[] guids = AssetDatabase.FindAssets("t:ManacoShaderDefinition");
            _availableShaders = new ManacoShaderDefinition[guids.Length];
            _shaderNames = new string[guids.Length + 1];
            _shaderNames[0] = "--- マテリアルを選択して適用 ---";

            var comp = target as Manaco;
            _selectedShaderIndex = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availableShaders[i] = AssetDatabase.LoadAssetAtPath<ManacoShaderDefinition>(path);
                _shaderNames[i + 1] = _availableShaders[i].shaderName;

                if (comp != null && comp.appliedShaderDef == _availableShaders[i])
                {
                    _selectedShaderIndex = i + 1;
                }
            }
        }

        private ManacoPreset _selectedPreset;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("アバタープリセット", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup("Apply Preset", _selectedPresetIndex, _presetNames);
            if (newIndex != _selectedPresetIndex)
            {
                _selectedPresetIndex = newIndex;
                if (_selectedPresetIndex > 0)
                {
                    ApplyPreset((Manaco)target, _availablePresets[_selectedPresetIndex - 1]);
                }
            }
            if (GUILayout.Button("更新", GUILayout.Width(40)))
            {
                LoadPresets();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("カスタムマテリアル", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            int newShaderIndex = EditorGUILayout.Popup("Apply Material", _selectedShaderIndex, _shaderNames);
            if (newShaderIndex != _selectedShaderIndex)
            {
                _selectedShaderIndex = newShaderIndex;
                if (_selectedShaderIndex > 0)
                {
                    ApplyShader((Manaco)target, _availableShaders[_selectedShaderIndex - 1]);
                }
            }
            if (GUILayout.Button("更新", GUILayout.Width(40)))
            {
                LoadShaders();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            EditorGUILayout.PropertyField(_useNdmfPreviewProp, new GUIContent("NDMF Preview を有効にする"));

            EditorGUILayout.Space(8);

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "上級設定", true, EditorStyles.foldoutHeader);
            if (_showAdvanced)
            {
                var comp = (Manaco)target;

                for (int i = 0; i < _eyeRegionsProp.arraySize; i++)
                {
                    if (DrawEyeRegionSummary(comp, _eyeRegionsProp.GetArrayElementAtIndex(i), i))
                        break;
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("+ 追加"))
                    _eyeRegionsProp.arraySize++;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ApplyPreset(Manaco comp, ManacoPreset preset)
        {
            Undo.RecordObject(comp, "Apply Manaco Preset");

            comp.appliedAvatarPreset = preset;
            comp.eyeRegions.Clear();

            // アバターのルートからSkinnedMeshRendererを検索する
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

                // オブジェクト名に一致するSkinnedMeshRendererを探す
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

            // シェーダーがすでに適用されているなら再度割り当て直す
            if (comp.appliedShaderDef != null)
            {
                ApplyShader(comp, comp.appliedShaderDef);
            }

            serializedObject.Update();
            EditorUtility.SetDirty(comp);

            // Debug.Log($"[Manaco] Applied preset: {preset.avatarName}");
        }

        private void ApplyShader(Manaco comp, ManacoShaderDefinition shaderDef)
        {
            Undo.RecordObject(comp, "Apply Manaco Shader Definition");

            comp.appliedShaderDef = shaderDef;

            foreach (var region in comp.eyeRegions)
            {
                if (region.eyeType == Manaco.EyeType.Left)
                {
                    region.customMaterial = shaderDef.leftEyeMaterial;
                }
                else if (region.eyeType == Manaco.EyeType.Right)
                {
                    region.customMaterial = shaderDef.rightEyeMaterial;
                }
                else if (region.eyeType == Manaco.EyeType.Both)
                {
                    region.customMaterial = shaderDef.bothEyeMaterial;
                }
            }

            serializedObject.Update();
            EditorUtility.SetDirty(comp);

            // Debug.Log($"[Manaco] Applied shader: {shaderDef.shaderName}");
        }

        private bool DrawEyeRegionSummary(Manaco comp, SerializedProperty element, int index)
        {
            var eyeTypeProp             = element.FindPropertyRelative("eyeType");
            var rendererProp            = element.FindPropertyRelative("targetRenderer");
            var matIndexProp            = element.FindPropertyRelative("materialIndex");
            var uvRectsProp             = element.FindPropertyRelative("eyePolygonRegions");
            var customMaterialProp      = element.FindPropertyRelative("customMaterial");
            var bakeFallbackProp        = element.FindPropertyRelative("bakeFallbackTexture");
            var fallbackResolutionProp  = element.FindPropertyRelative("fallbackTextureResolution");

            var smr = rendererProp.objectReferenceValue as SkinnedMeshRenderer;
            var eyeTypeEnum = (Manaco.EyeType)eyeTypeProp.enumValueIndex;
            string eyeTypeStr = eyeTypeEnum == Manaco.EyeType.Both ? "両目" : (eyeTypeEnum == Manaco.EyeType.Left ? "左目" : "右目");
            string label = smr != null ? $"[{index}] ({eyeTypeStr})  ({smr.name})" : $"[{index}]({eyeTypeStr})";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ヘッダー行（ラベル + 削除ボタン）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (GUILayout.Button("削除", GUILayout.Width(50)))
            {
                _eyeRegionsProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(eyeTypeProp, new GUIContent("目のタイプ"));
            EditorGUILayout.PropertyField(rendererProp, new GUIContent("レンダラー"));
            EditorGUILayout.PropertyField(matIndexProp, new GUIContent("マテリアルスロット"));
            EditorGUILayout.PropertyField(customMaterialProp, new GUIContent("マテリアル"));

            if (customMaterialProp.objectReferenceValue == null)
                EditorGUILayout.HelpBox("カスタムマテリアルが未設定です。", MessageType.Warning);

            EditorGUILayout.PropertyField(bakeFallbackProp, new GUIContent("フォールバックテクスチャを自動生成", "ビルド時にシェーダーをレンダリングして _MainTex に設定します。VRChatセーフティー設定対策。"));
            if (bakeFallbackProp.boolValue)
            {
                EditorGUI.indentLevel++;
                fallbackResolutionProp.intValue = EditorGUILayout.IntPopup(
                    "解像度",
                    fallbackResolutionProp.intValue,
                    new[] { "64", "128", "256", "512", "1024", "2048" },
                    new[] { 64, 128, 256, 512, 1024, 2048 });
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"選択済みUV Island: {uvRectsProp.arraySize} 個", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(2);

            // このリージョン専用のUVエディタを開くボタン
            if (GUILayout.Button("UV エディタを開く"))
                ManacoWindow.OpenWith(comp, index);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            return false;
        }

        private bool _showAdvanced = false;
    }
}
