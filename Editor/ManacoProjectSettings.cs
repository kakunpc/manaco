using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    [FilePath("ProjectSettings/ManacoSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ManacoProjectSettingsData : ScriptableSingleton<ManacoProjectSettingsData>
    {
        [SerializeField] private bool useFastPreview;

        public bool UseFastPreview
        {
            get => useFastPreview;
            set => useFastPreview = value;
        }

        public void SaveSettings()
        {
            Save(true);
        }

        private void OnDisable()
        {
            SaveSettings();
        }
    }

    public static class ManacoProjectSettings
    {
        public static bool UseFastPreview
        {
            get => ManacoProjectSettingsData.instance.UseFastPreview;
            set
            {
                if (ManacoProjectSettingsData.instance.UseFastPreview == value) return;
                ManacoProjectSettingsData.instance.UseFastPreview = value;
                ManacoProjectSettingsData.instance.SaveSettings();
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/Manaco", SettingsScope.Project)
            {
                label = "Manaco",
                guiHandler = DrawGUI,
                keywords = new HashSet<string> { "Manaco", "Preview", "Fast Preview", "高速" },
            };
        }

        private static void DrawGUI(string searchContext)
        {
            EditorGUI.BeginChangeCheck();
            bool useFastPreview = EditorGUILayout.Toggle(
                ManacoLocale.T("Toggle.FastPreview"),
                UseFastPreview);
            if (EditorGUI.EndChangeCheck())
                UseFastPreview = useFastPreview;

            if (UseFastPreview)
            {
                EditorGUILayout.HelpBox(
                    ManacoLocale.T("Message.FastPreviewWarning"),
                    MessageType.Warning);
            }
        }
    }
}
