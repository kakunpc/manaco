using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    public class ManacoSetupWindow : EditorWindow
    {
        private GameObject _targetAvatar;
        private ManacoPreset _avatarPreset;
        private ManacoShaderDefinition _shaderPreset;

        private ManacoPreset[] _availablePresets;
        private string[] _presetNames;
        private int _selectedPresetIndex = 0;

        private ManacoShaderDefinition[] _availableShaders;
        private string[] _shaderNames;
        private int _selectedShaderIndex = 0;

        [MenuItem("GameObject/ちゃとらとりー/Manaco(まなこ)", false, 0)]
        private static void ShowWindow(MenuCommand command)
        {
            var window = GetWindow<ManacoSetupWindow>("Manaco(まなこ)");
            window._targetAvatar = command.context as GameObject;
            if (window._targetAvatar == null)
            {
                window._targetAvatar = Selection.activeGameObject;
            }
            window.Show();
        }

        private void OnEnable()
        {
            LoadPresets();
            LoadShaders();
        }

        private void LoadPresets()
        {
            string[] guids = AssetDatabase.FindAssets("t:ManacoPreset");
            _availablePresets = new ManacoPreset[guids.Length];
            _presetNames = new string[guids.Length + 1];
            _presetNames[0] = "--- アバタープリセットを選択 ---";

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availablePresets[i] = AssetDatabase.LoadAssetAtPath<ManacoPreset>(path);
                _presetNames[i + 1] = _availablePresets[i].avatarName;
            }
        }

        private void LoadShaders()
        {
            string[] guids = AssetDatabase.FindAssets("t:ManacoShaderDefinition");
            _availableShaders = new ManacoShaderDefinition[guids.Length];
            _shaderNames = new string[guids.Length + 1];
            _shaderNames[0] = "--- マテリアルプリセットを選択 ---";

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availableShaders[i] = AssetDatabase.LoadAssetAtPath<ManacoShaderDefinition>(path);
                _shaderNames[i + 1] = _availableShaders[i].shaderName;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Material Assign Non-destructive Assistant for Customization Operations（まなこ）", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _targetAvatar = (GameObject)EditorGUILayout.ObjectField("Target Avatar", _targetAvatar, typeof(GameObject), true);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            _selectedPresetIndex = EditorGUILayout.Popup("Avatar Preset", _selectedPresetIndex, _presetNames);
            if (GUILayout.Button("更新", GUILayout.Width(40))) LoadPresets();
            EditorGUILayout.EndHorizontal();
            _avatarPreset = (_selectedPresetIndex > 0 && _selectedPresetIndex <= _availablePresets.Length) ? _availablePresets[_selectedPresetIndex - 1] : null;

            EditorGUILayout.BeginHorizontal();
            _selectedShaderIndex = EditorGUILayout.Popup("Shader Preset", _selectedShaderIndex, _shaderNames);
            if (GUILayout.Button("更新", GUILayout.Width(40))) LoadShaders();
            EditorGUILayout.EndHorizontal();
            _shaderPreset = (_selectedShaderIndex > 0 && _selectedShaderIndex <= _availableShaders.Length) ? _availableShaders[_selectedShaderIndex - 1] : null;

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(_targetAvatar == null || _avatarPreset == null || _shaderPreset == null);
            if (GUILayout.Button("Apply", GUILayout.Height(30)))
            {
                ApplySetup();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void ApplySetup()
        {
            if (_targetAvatar == null || _avatarPreset == null || _shaderPreset == null) return;

            var eyeObj = new GameObject("Manaco");
            eyeObj.transform.SetParent(_targetAvatar.transform, false);

            var comp = eyeObj.AddComponent<Manaco>();

            comp.appliedAvatarPreset = _avatarPreset;
            comp.appliedShaderDef = _shaderPreset;
            comp.eyeRegions.Clear();

            var renderers = _targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (var pr in _avatarPreset.regions)
            {
                var region = new Manaco.EyeRegion
                {
                    eyeType = pr.eyeType,
                    materialIndex = pr.materialIndex,
                    eyePolygonRegions = new Manaco.UVPolygonRegion[pr.eyePolygonRegions.Length]
                };

                if (pr.eyePolygonRegions != null)
                {
                    for (int i = 0; i < pr.eyePolygonRegions.Length; i++)
                    {
                        if (pr.eyePolygonRegions[i].uvPoints != null)
                        {
                            region.eyePolygonRegions[i] = new Manaco.UVPolygonRegion
                            {
                                uvPoints = pr.eyePolygonRegions[i].uvPoints.Clone() as Vector2[]
                            };
                        }
                    }
                }

                foreach (var smr in renderers)
                {
                    if (smr.name == pr.targetRendererName)
                    {
                        region.targetRenderer = smr;
                        break;
                    }
                }

                if (region.eyeType == Manaco.EyeType.Left)
                {
                    region.customMaterial = _shaderPreset.leftEyeMaterial;
                }
                else if (region.eyeType == Manaco.EyeType.Right)
                {
                    region.customMaterial = _shaderPreset.rightEyeMaterial;
                }
                else if (region.eyeType == Manaco.EyeType.Both)
                {
                    region.customMaterial = _shaderPreset.bothEyeMaterial;
                }

                comp.eyeRegions.Add(region);
            }

            Undo.RegisterCreatedObjectUndo(eyeObj, "Setup Eye Shader");
            Selection.activeGameObject = eyeObj;
            Debug.Log($"[Manaco] Setup complete for {_targetAvatar.name}");

            this.Close();
        }
    }
}
