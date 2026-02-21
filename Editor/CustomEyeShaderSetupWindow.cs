using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    public class CustomEyeShaderSetupWindow : EditorWindow
    {
        private GameObject _targetAvatar;
        private CustomEyeShaderPreset _avatarPreset;
        private CustomEyeShaderDefinition _shaderPreset;

        private CustomEyeShaderPreset[] _availablePresets;
        private string[] _presetNames;
        private int _selectedPresetIndex = 0;

        private CustomEyeShaderDefinition[] _availableShaders;
        private string[] _shaderNames;
        private int _selectedShaderIndex = 0;

        [MenuItem("GameObject/ちゃとらとりー/Setup Eye Shader", false, 0)]
        private static void ShowWindow(MenuCommand command)
        {
            var window = GetWindow<CustomEyeShaderSetupWindow>("Setup Eye Shader");
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
            string[] guids = AssetDatabase.FindAssets("t:CustomEyeShaderPreset");
            _availablePresets = new CustomEyeShaderPreset[guids.Length];
            _presetNames = new string[guids.Length + 1];
            _presetNames[0] = "--- アバタープリセットを選択 ---";

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availablePresets[i] = AssetDatabase.LoadAssetAtPath<CustomEyeShaderPreset>(path);
                _presetNames[i + 1] = _availablePresets[i].avatarName;
            }
        }

        private void LoadShaders()
        {
            string[] guids = AssetDatabase.FindAssets("t:CustomEyeShaderDefinition");
            _availableShaders = new CustomEyeShaderDefinition[guids.Length];
            _shaderNames = new string[guids.Length + 1];
            _shaderNames[0] = "--- シェーダープリセットを選択 ---";

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availableShaders[i] = AssetDatabase.LoadAssetAtPath<CustomEyeShaderDefinition>(path);
                _shaderNames[i + 1] = _availableShaders[i].shaderName;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Eye Shader Setup", EditorStyles.boldLabel);
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

            var eyeObj = new GameObject("CustomEyeShader");
            eyeObj.transform.SetParent(_targetAvatar.transform, false);

            var comp = eyeObj.AddComponent<CustomEyeShaderCore>();

            comp.appliedAvatarPreset = _avatarPreset;
            comp.appliedShaderDef = _shaderPreset;
            comp.eyeRegions.Clear();

            var renderers = _targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (var pr in _avatarPreset.regions)
            {
                var region = new CustomEyeShaderCore.EyeRegion
                {
                    eyeType = pr.eyeType,
                    materialIndex = pr.materialIndex,
                    eyePolygonRegions = new CustomEyeShaderCore.UVPolygonRegion[pr.eyePolygonRegions.Length]
                };

                if (pr.eyePolygonRegions != null)
                {
                    for (int i = 0; i < pr.eyePolygonRegions.Length; i++)
                    {
                        if (pr.eyePolygonRegions[i].uvPoints != null)
                        {
                            region.eyePolygonRegions[i] = new CustomEyeShaderCore.UVPolygonRegion
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

                if (region.eyeType == CustomEyeShaderCore.EyeType.Left)
                {
                    region.customMaterial = _shaderPreset.leftEyeMaterial;
                }
                else if (region.eyeType == CustomEyeShaderCore.EyeType.Right)
                {
                    region.customMaterial = _shaderPreset.rightEyeMaterial;
                }
                else if (region.eyeType == CustomEyeShaderCore.EyeType.Both)
                {
                    region.customMaterial = _shaderPreset.bothEyeMaterial;
                }

                comp.eyeRegions.Add(region);
            }

            Undo.RegisterCreatedObjectUndo(eyeObj, "Setup Eye Shader");
            Selection.activeGameObject = eyeObj;
            Debug.Log($"[CustomEyeShaderCore] Setup complete for {_targetAvatar.name}");

            this.Close();
        }
    }
}
