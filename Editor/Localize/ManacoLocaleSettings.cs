using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    /// <summary>
    /// Edit > Preferences > Manaco に言語設定を追加する。
    /// </summary>
    public static class ManacoLocaleSettings
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Preferences/Manaco", SettingsScope.User)
            {
                label = "Manaco",
                guiHandler = DrawGUI,
                keywords = new HashSet<string> { "Manaco", "Language", "言語", "ローカライズ" },
            };
        }

        private static void DrawGUI(string searchContext)
        {
            var (codes, names) = ManacoLocale.GetAvailableLanguages();
            int idx = System.Array.IndexOf(codes, ManacoLocale.CurrentLanguageCode);
            if (idx < 0) idx = 0;

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                int newIdx = EditorGUILayout.Popup(
                    ManacoLocale.T("Prefs.Language"),
                    idx,
                    names,
                    GUILayout.MaxWidth(400f));

                if (GUILayout.Button(ManacoLocale.T("Button.Refresh"), GUILayout.Width(60f)))
                {
                    ManacoLocale.Reload();
                    GUIUtility.ExitGUI();
                    return;
                }

                if (EditorGUI.EndChangeCheck() && newIdx >= 0 && newIdx < codes.Length)
                    ManacoLocale.SetLanguage(codes[newIdx]);
            }

            EditorGUILayout.HelpBox(
                codes.Length == 0
                    ? "No locale assets found."
                    : $"Current: {ManacoLocale.CurrentLanguageCode}  |  {codes.Length} language(s) available",
                codes.Length == 0 ? MessageType.Warning : MessageType.None);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("ロケールアセットを再生成", GUILayout.MaxWidth(200f)))
            {
                ManacoLocaleSetup.RecreateLocaleAssets();
                GUIUtility.ExitGUI();
            }
        }
    }
}
