using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    public static class ManacoPresetUtility
    {
        [MenuItem("CONTEXT/Manaco/Create Avatar Preset")]
        public static void CreatePreset(MenuCommand command)
        {
            var comp = command.context as Manaco;
            if (comp == null) return;

            var preset = ScriptableObject.CreateInstance<ManacoPreset>();
            preset.avatarName = comp.gameObject.name;

            foreach (var region in comp.eyeRegions)
            {
                if (region.targetRenderer == null) continue;

                var presetRegion = new ManacoPreset.PresetRegion
                {
                    eyeType = region.eyeType,
                    targetRendererName = region.targetRenderer.name,
                    materialIndex = region.materialIndex,
                    eyePolygonRegions = new Manaco.UVPolygonRegion[region.eyePolygonRegions.Length]
                };
                for (int i = 0; i < region.eyePolygonRegions.Length; i++)
                {
                    presetRegion.eyePolygonRegions[i] = new Manaco.UVPolygonRegion
                    {
                        uvPoints = region.eyePolygonRegions[i].uvPoints.Clone() as Vector2[]
                    };
                }
                preset.regions.Add(presetRegion);
            }

            string defaultName = $"{preset.avatarName}_EyePreset.asset";
            string path = EditorUtility.SaveFilePanelInProject(
                ManacoLocale.T("Preset.SaveTitle"), defaultName, "asset", "");

            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(preset);
        }
    }
}
