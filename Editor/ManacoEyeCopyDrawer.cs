using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    /// <summary>
    /// CopyEyeFromAvatar モード時の EyeRegion Inspector 描画を担当する静的クラス。
    /// コピー先（自分のアバター）とコピー元（コピーするアバター）を明確に分けて表示する。
    /// </summary>
    public static class ManacoEyeCopyDrawer
    {
        private static readonly Color SectionLineColor = new Color(0.3f, 0.3f, 0.3f);

        /// <summary>
        /// CopyEyeFromAvatar モード時の EyeRegion を描画する。
        /// 削除が行われた場合は true を返す。
        /// </summary>
        public static bool DrawCopyEyeRegionSummary(
            Manaco comp,
            SerializedProperty eyeRegionsProp,
            SerializedProperty element,
            int index)
        {
            var eyeTypeProp                 = element.FindPropertyRelative("eyeType");
            var rendererProp                = element.FindPropertyRelative("targetRenderer");
            var matIndexProp                = element.FindPropertyRelative("materialIndex");
            var uvRectsProp                 = element.FindPropertyRelative("eyePolygonRegions");
            var sourceRendererProp          = element.FindPropertyRelative("sourceRenderer");
            var sourceMaterialIndexProp     = element.FindPropertyRelative("sourceMaterialIndex");
            var sourceEyePolygonRegionsProp = element.FindPropertyRelative("sourceEyePolygonRegions");
            var extractResolutionProp       = element.FindPropertyRelative("extractTextureResolution");

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
                : $"[{index}] ({eyeTypeStr})";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ---- ヘッダー行 ----
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (GUILayout.Button(ManacoLocale.T("Button.Delete"), GUILayout.Width(50)))
            {
                eyeRegionsProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ==== コピー先（自分のアバター）====
            DrawSectionHeader(ManacoLocale.T("Section.CopyDestination"));

            EditorGUILayout.PropertyField(eyeTypeProp,  new GUIContent(ManacoLocale.T("Label.EyeType")));
            EditorGUILayout.PropertyField(rendererProp, new GUIContent(ManacoLocale.T("Label.Renderer")));
            EditorGUILayout.PropertyField(matIndexProp, new GUIContent(ManacoLocale.T("Label.MaterialSlot")));

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(
                ManacoLocale.T("Message.UVIslandCount", uvRectsProp.arraySize),
                EditorStyles.miniLabel);
            EditorGUI.indentLevel--;

            if (GUILayout.Button(ManacoLocale.T("Button.OpenUVEditor")))
                ManacoWindow.OpenWith(comp, index);

            EditorGUILayout.Space(6);

            // ==== コピー元（コピーするアバター）====
            DrawSectionHeader(ManacoLocale.T("Section.CopySource"));

            EditorGUILayout.PropertyField(sourceRendererProp,      new GUIContent(ManacoLocale.T("Label.Renderer")));
            EditorGUILayout.PropertyField(sourceMaterialIndexProp, new GUIContent(ManacoLocale.T("Label.MaterialSlot")));

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(
                ManacoLocale.T("Message.SourceUVIslandCount", sourceEyePolygonRegionsProp.arraySize),
                EditorStyles.miniLabel);
            EditorGUI.indentLevel--;

            if (GUILayout.Button(ManacoLocale.T("Button.OpenUVEditorSource")))
                ManacoWindow.OpenForSource(comp, index);

            extractResolutionProp.intValue = EditorGUILayout.IntPopup(
                ManacoLocale.T("Label.ExtractResolution"),
                extractResolutionProp.intValue,
                new[] { "64", "128", "256", "512", "1024", "2048" },
                new[] { 64, 128, 256, 512, 1024, 2048 });

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            return false;
        }

        private static void DrawSectionHeader(string title)
        {
            EditorGUI.DrawRect(
                GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)),
                SectionLineColor);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
