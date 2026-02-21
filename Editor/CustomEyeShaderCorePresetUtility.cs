using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chatoratori.CustomEyeShaderCore.Editor
{
    public static class CustomEyeShaderCorePresetUtility
    {
        [MenuItem("CONTEXT/CustomEyeShaderCore/Create Avatar Preset")]
        public static void CreatePreset(MenuCommand command)
        {
            var comp = command.context as CustomEyeShaderCore;
            if (comp == null) return;

            var preset = ScriptableObject.CreateInstance<CustomEyeShaderPreset>();
            preset.avatarName = comp.gameObject.name;

            foreach (var region in comp.eyeRegions)
            {
                if (region.targetRenderer == null) continue;

                var presetRegion = new CustomEyeShaderPreset.PresetRegion
                {
                    eyeType = region.eyeType,
                    targetRendererName = region.targetRenderer.name,
                    materialIndex = region.materialIndex,
                    eyePolygonRegions = new CustomEyeShaderCore.UVPolygonRegion[region.eyePolygonRegions.Length]
                };
                for (int i = 0; i < region.eyePolygonRegions.Length; i++)
                {
                    presetRegion.eyePolygonRegions[i] = new CustomEyeShaderCore.UVPolygonRegion
                    {
                        uvPoints = region.eyePolygonRegions[i].uvPoints.Clone() as Vector2[]
                    };
                }
                preset.regions.Add(presetRegion);
            }

            string folderPath = "Assets/ちゃとらとりー/CustomEyeShaderCore/Presets";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/ちゃとらとりー/CustomEyeShaderCore", "Presets");
            }

            string defaultName = $"{preset.avatarName}_EyePreset.asset";
            string path = EditorUtility.SaveFilePanelInProject("Save Avatar Preset", defaultName, "asset", "Save preset to Presets folder", folderPath);

            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CustomEyeShaderCore] Preset saved to {path}");
            EditorGUIUtility.PingObject(preset);
        }
    }
}
