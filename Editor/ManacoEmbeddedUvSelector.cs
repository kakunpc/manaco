using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    internal sealed class ManacoEmbeddedUvSelector
    {
        private static readonly Color SceneFillBase = new Color(0.20f, 1.00f, 0.45f, 0.18f);
        private static readonly Color SceneBorderBase = new Color(0.20f, 1.00f, 0.45f, 0.80f);
        private const float ScenePulseSpeed = 2.5f;
        private const float ScenePolylineWidth = 2.5f;
        private static bool s_SceneHooksInitialized;
        private static ManacoEmbeddedUvSelector s_ActiveSelector;
        private static double s_LastSceneRepaintTime;

        private readonly Manaco _target;
        private readonly int _regionIndex;
        private readonly bool _isSourceMode;
        private readonly SerializedObject _serializedObject;

        private SkinnedMeshRenderer[] _availableSmrs = System.Array.Empty<SkinnedMeshRenderer>();
        private string[] _smrNames = System.Array.Empty<string>();
        private int _smrIndex;
        private Texture2D _previewTexture;
        private Vector2[] _cachedUvs;
        private int[][] _cachedTriangles;
        private readonly HashSet<int> _selectedTriangles = new HashSet<int>();
        private Vector2 _leftScroll;

        public ManacoEmbeddedUvSelector(Manaco target, int regionIndex, bool isSourceMode)
        {
            _target = target;
            _regionIndex = regionIndex;
            _isSourceMode = isSourceMode;
            _serializedObject = new SerializedObject(target);
            EnsureSceneHooks();
            RefreshSmrList();
            RefreshPreviewCache();
        }

        public void DrawLayout(float minHeight = 430f, bool tutorialMode = false)
        {
            s_ActiveSelector = this;
            if (_target == null)
            {
                EditorGUILayout.HelpBox(ManacoLocale.T("Message.OpenFromInspector"), MessageType.Info);
                return;
            }

            _serializedObject.Update();
            var regionsProp = _serializedObject.FindProperty("eyeRegions");
            if (_regionIndex < 0 || _regionIndex >= regionsProp.arraySize)
            {
                EditorGUILayout.HelpBox(ManacoLocale.T("Message.RegionDeleted"), MessageType.Warning);
                _serializedObject.ApplyModifiedProperties();
                return;
            }

            var regionProp = regionsProp.GetArrayElementAtIndex(_regionIndex);
            if (tutorialMode)
                DrawTutorialLayout(regionProp, minHeight);
            else
            {
                EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.MinHeight(minHeight));
                DrawLeftPanel(regionProp);
                DrawRightPanel(regionProp, minHeight, false);
                EditorGUILayout.EndHorizontal();
            }

            _serializedObject.ApplyModifiedProperties();
        }

        public static void ClearActiveSelector()
        {
            s_ActiveSelector = null;
            SceneView.RepaintAll();
        }

        private void DrawTutorialLayout(SerializedProperty regionProp, float minHeight)
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.MinHeight(minHeight));
            DrawTutorialFields(regionProp);
            EditorGUILayout.Space(8f);
            DrawRightPanel(regionProp, minHeight, true);
            EditorGUILayout.Space(8f);
            DrawTutorialIslandList(regionProp);
            EditorGUILayout.EndVertical();
        }

        private void DrawTutorialFields(SerializedProperty regionProp)
        {
            var rendererProp = regionProp.FindPropertyRelative(GetRendererPropertyName());
            var materialIndexProp = regionProp.FindPropertyRelative(GetMaterialIndexPropertyName());
            var currentSmr = rendererProp.objectReferenceValue as SkinnedMeshRenderer;
            RefreshSmrIndex(currentSmr);

            EditorGUILayout.LabelField(ManacoLocale.T("Label.Renderer"), EditorStyles.boldLabel);
            if (_availableSmrs.Length > 0)
            {
                EditorGUI.BeginChangeCheck();
                int newSmrIndex = EditorGUILayout.Popup(_smrIndex, _smrNames);
                if (EditorGUI.EndChangeCheck())
                {
                    _smrIndex = newSmrIndex;
                    rendererProp.objectReferenceValue = _availableSmrs[_smrIndex];
                    materialIndexProp.intValue = 0;
                    _serializedObject.ApplyModifiedProperties();
                    RefreshPreviewCache();
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(currentSmr, typeof(SkinnedMeshRenderer), true);
                EditorGUI.EndDisabledGroup();
            }

            currentSmr = rendererProp.objectReferenceValue as SkinnedMeshRenderer;
            int maxSlot = currentSmr != null ? Mathf.Max(0, currentSmr.sharedMaterials.Length - 1) : 0;
            EditorGUILayout.LabelField(ManacoLocale.T("Label.MaterialSlot"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(materialIndexProp.intValue <= 0);
            if (GUILayout.Button("<", GUILayout.Width(28f)))
                materialIndexProp.intValue = Mathf.Max(0, materialIndexProp.intValue - 1);
            EditorGUI.EndDisabledGroup();
            materialIndexProp.intValue = Mathf.Clamp(EditorGUILayout.IntField(materialIndexProp.intValue, GUILayout.Width(40f)), 0, maxSlot);
            EditorGUI.BeginDisabledGroup(materialIndexProp.intValue >= maxSlot);
            if (GUILayout.Button(">", GUILayout.Width(28f)))
                materialIndexProp.intValue = Mathf.Min(maxSlot, materialIndexProp.intValue + 1);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                _serializedObject.ApplyModifiedProperties();
                RefreshPreviewCache();
            }
        }

        private void DrawTutorialIslandList(SerializedProperty regionProp)
        {
            var uvRegionsProp = regionProp.FindPropertyRelative(GetUvRegionsPropertyName());
            EditorGUILayout.LabelField(ManacoLocale.T("Tutorial.SelectedUvIslands", uvRegionsProp.arraySize), EditorStyles.boldLabel);

            for (int i = 0; i < uvRegionsProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                DrawIslandColorChip(i);
                EditorGUILayout.LabelField($"[{i}]", EditorStyles.miniLabel);
                if (GUILayout.Button(ManacoLocale.T("Button.Delete"), GUILayout.Width(56f)))
                {
                    uvRegionsProp.DeleteArrayElementAtIndex(i);
                    _serializedObject.ApplyModifiedProperties();
                    RefreshSelectionFromRects();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (uvRegionsProp.arraySize == 0)
                EditorGUILayout.LabelField($"  {ManacoLocale.T("Message.NotSet")}", EditorStyles.miniLabel);
        }

        private void DrawLeftPanel(SerializedProperty regionProp)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(260f), GUILayout.ExpandHeight(true));
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            var rendererProp = regionProp.FindPropertyRelative(GetRendererPropertyName());
            var materialIndexProp = regionProp.FindPropertyRelative(GetMaterialIndexPropertyName());
            var uvRegionsProp = regionProp.FindPropertyRelative(GetUvRegionsPropertyName());

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var currentSmr = rendererProp.objectReferenceValue as SkinnedMeshRenderer;
            RefreshSmrIndex(currentSmr);

            if (_availableSmrs.Length > 0)
            {
                EditorGUI.BeginChangeCheck();
                int newSmrIndex = EditorGUILayout.Popup(ManacoLocale.T("Label.Renderer"), _smrIndex, _smrNames);
                if (EditorGUI.EndChangeCheck())
                {
                    _smrIndex = newSmrIndex;
                    rendererProp.objectReferenceValue = _availableSmrs[_smrIndex];
                    materialIndexProp.intValue = 0;
                    _serializedObject.ApplyModifiedProperties();
                    RefreshPreviewCache();
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(ManacoLocale.T("Label.Renderer"), currentSmr, typeof(SkinnedMeshRenderer), true);
                EditorGUI.EndDisabledGroup();
            }

            int maxSlot = currentSmr != null ? Mathf.Max(0, currentSmr.sharedMaterials.Length - 1) : 0;
            EditorGUI.BeginChangeCheck();
            materialIndexProp.intValue = EditorGUILayout.IntSlider(
                ManacoLocale.T("Label.MaterialSlot"),
                Mathf.Clamp(materialIndexProp.intValue, 0, maxSlot),
                0,
                maxSlot);
            if (EditorGUI.EndChangeCheck())
            {
                _serializedObject.ApplyModifiedProperties();
                RefreshPreviewCache();
            }

            if (currentSmr != null && currentSmr.sharedMaterials.Length > 1)
                EditorGUILayout.HelpBox(ManacoLocale.T("Message.MatSlotHint"), MessageType.Info);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(ManacoLocale.T("Message.SelUVIslands", uvRegionsProp.arraySize), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(ManacoLocale.T("Message.ClickHint"), MessageType.None);

            for (int i = 0; i < uvRegionsProp.arraySize; i++)
            {
                var islandProp = uvRegionsProp.GetArrayElementAtIndex(i).FindPropertyRelative("uvPoints");
                EditorGUILayout.BeginHorizontal();
                DrawIslandColorChip(i);
                EditorGUILayout.LabelField($"[{i}]  {ManacoLocale.T("Message.UVPoints", islandProp.arraySize)}", EditorStyles.miniLabel);
                if (GUILayout.Button(ManacoLocale.T("Button.Delete"), GUILayout.Width(56f)))
                {
                    uvRegionsProp.DeleteArrayElementAtIndex(i);
                    _serializedObject.ApplyModifiedProperties();
                    RefreshSelectionFromRects();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (uvRegionsProp.arraySize == 0)
                EditorGUILayout.LabelField($"  {ManacoLocale.T("Message.NotSet")}", EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRightPanel(SerializedProperty regionProp, float minHeight, bool tutorialMode)
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.MinHeight(minHeight));

            var eyeType = (Manaco.EyeType)regionProp.FindPropertyRelative("eyeType").enumValueIndex;
            string eyeTypeName = ManacoLocale.GetEyeTypeName(eyeType);
            var uvRegionsProp = regionProp.FindPropertyRelative(GetUvRegionsPropertyName());

            if (tutorialMode)
                EditorGUILayout.LabelField(ManacoLocale.T("Tutorial.Texture"), EditorStyles.boldLabel);

            if (uvRegionsProp.arraySize == 0)
            {
                string message = _isSourceMode
                    ? ManacoLocale.T("Tutorial.SelectSourceEyeType", eyeTypeName)
                    : ManacoLocale.T("Tutorial.SelectEyeType", eyeTypeName);
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }

            Rect panelRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.MinHeight(tutorialMode ? Mathf.Max(280f, minHeight - 200f) : minHeight - 70f),
                GUILayout.ExpandHeight(true));

            if (panelRect.width <= 10f || panelRect.height <= 10f)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            float margin = 8f;
            float size = Mathf.Min(panelRect.width, panelRect.height) - margin * 2f;
            Rect previewRect = new Rect(
                panelRect.x + (panelRect.width - size) * 0.5f,
                panelRect.y + margin,
                size,
                size);

            EditorGUI.DrawRect(previewRect, new Color(0.12f, 0.12f, 0.12f));
            if (_previewTexture != null)
                GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.StretchToFill);
            else
                GUI.Label(previewRect, ManacoLocale.T("Message.NoTexture"), EditorStyles.centeredGreyMiniLabel);

            DrawWireframe(previewRect);
            DrawIslandOverlays(previewRect);
            HandlePreviewClick(previewRect);

            if (_cachedUvs != null)
            {
                var hintRect = new Rect(previewRect.x, previewRect.yMax + 4f, previewRect.width, 20f);
                GUI.Label(hintRect, ManacoLocale.T("Message.ClickHintBottom"), EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private string GetRendererPropertyName() => _isSourceMode ? "sourceRenderer" : "targetRenderer";
        private string GetMaterialIndexPropertyName() => _isSourceMode ? "sourceMaterialIndex" : "materialIndex";
        private string GetUvRegionsPropertyName() => _isSourceMode ? "sourceEyePolygonRegions" : "eyePolygonRegions";

        private void RefreshSmrList()
        {
            _availableSmrs = System.Array.Empty<SkinnedMeshRenderer>();
            _smrNames = System.Array.Empty<string>();
            if (_target == null) return;

            SkinnedMeshRenderer[] smrs;
            if (_isSourceMode)
            {
                if (_target.sourceAvatarPrefab == null) return;
                smrs = _target.sourceAvatarPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }
            else
            {
                Transform root = _target.transform.root;
                var descriptor = _target.GetComponentInParent<VRC.SDKBase.VRC_AvatarDescriptor>();
                if (descriptor != null) root = descriptor.transform;
                else
                {
                    var animator = _target.GetComponentInParent<Animator>();
                    if (animator != null) root = animator.transform;
                }
                smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }

            _availableSmrs = smrs;
            _smrNames = new string[smrs.Length];
            for (int i = 0; i < smrs.Length; i++) _smrNames[i] = smrs[i].name;
        }

        private void RefreshSmrIndex(SkinnedMeshRenderer currentSmr)
        {
            _smrIndex = 0;
            for (int i = 0; i < _availableSmrs.Length; i++)
            {
                if (_availableSmrs[i] == currentSmr)
                {
                    _smrIndex = i;
                    break;
                }
            }
        }

        private void RefreshPreviewCache()
        {
            RefreshSmrList();
            _previewTexture = null;
            _cachedUvs = null;
            _cachedTriangles = null;
            _selectedTriangles.Clear();

            if (_target == null || _regionIndex >= _target.eyeRegions.Count) return;

            var region = _target.eyeRegions[_regionIndex];
            var smr = _isSourceMode ? region.sourceRenderer : region.targetRenderer;
            if (smr == null || smr.sharedMesh == null) return;

            int materialIndex = _isSourceMode ? region.sourceMaterialIndex : region.materialIndex;
            materialIndex = Mathf.Clamp(materialIndex, 0, Mathf.Max(0, smr.sharedMaterials.Length - 1));

            if (smr.sharedMaterials.Length > 0 && smr.sharedMaterials[materialIndex] != null)
                _previewTexture = smr.sharedMaterials[materialIndex].mainTexture as Texture2D;

            _cachedUvs = smr.sharedMesh.uv;
            int subMeshIndex = materialIndex < smr.sharedMesh.subMeshCount ? materialIndex : 0;
            var rawTriangles = smr.sharedMesh.GetTriangles(subMeshIndex);
            _cachedTriangles = new int[rawTriangles.Length / 3][];
            for (int i = 0; i < rawTriangles.Length; i += 3)
                _cachedTriangles[i / 3] = new[] { rawTriangles[i], rawTriangles[i + 1], rawTriangles[i + 2] };

            RefreshSelectionFromRects();
        }

        private void RefreshSelectionFromRects()
        {
            _selectedTriangles.Clear();
            if (_cachedUvs == null || _cachedTriangles == null || _target == null || _regionIndex >= _target.eyeRegions.Count) return;

            var region = _target.eyeRegions[_regionIndex];
            var polygonRegions = _isSourceMode ? region.sourceEyePolygonRegions : region.eyePolygonRegions;
            if (polygonRegions == null) return;

            foreach (var polygonRegion in polygonRegions)
            {
                if (polygonRegion.uvPoints == null || polygonRegion.uvPoints.Length == 0) continue;

                var pointSet = new HashSet<long>();
                foreach (var point in polygonRegion.uvPoints) pointSet.Add(QuantizeUv(point));

                for (int i = 0; i < _cachedTriangles.Length; i++)
                {
                    var tri = _cachedTriangles[i];
                    if (pointSet.Contains(QuantizeUv(_cachedUvs[tri[0]])) &&
                        pointSet.Contains(QuantizeUv(_cachedUvs[tri[1]])) &&
                        pointSet.Contains(QuantizeUv(_cachedUvs[tri[2]])))
                        _selectedTriangles.Add(i);
                }
            }
        }

        private void DrawWireframe(Rect previewRect)
        {
            if (_cachedUvs == null || _cachedTriangles == null) return;

            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.15f);
            foreach (var tri in _cachedTriangles)
            {
                var p0 = UvToScreen(previewRect, _cachedUvs[tri[0]]);
                var p1 = UvToScreen(previewRect, _cachedUvs[tri[1]]);
                var p2 = UvToScreen(previewRect, _cachedUvs[tri[2]]);
                Handles.DrawLine(p0, p1);
                Handles.DrawLine(p1, p2);
                Handles.DrawLine(p2, p0);
            }

            Handles.color = new Color(0.2f, 1f, 0.4f, 0.85f);
            foreach (int index in _selectedTriangles)
            {
                var tri = _cachedTriangles[index];
                var p0 = UvToScreen(previewRect, _cachedUvs[tri[0]]);
                var p1 = UvToScreen(previewRect, _cachedUvs[tri[1]]);
                var p2 = UvToScreen(previewRect, _cachedUvs[tri[2]]);
                Handles.DrawLine(p0, p1);
                Handles.DrawLine(p1, p2);
                Handles.DrawLine(p2, p0);
            }
            Handles.EndGUI();
        }

        private void DrawIslandOverlays(Rect previewRect)
        {
            if (_target == null || _regionIndex >= _target.eyeRegions.Count) return;

            var region = _target.eyeRegions[_regionIndex];
            var polygonRegions = _isSourceMode ? region.sourceEyePolygonRegions : region.eyePolygonRegions;
            if (polygonRegions == null) return;

            for (int i = 0; i < polygonRegions.Length; i++)
            {
                if (polygonRegions[i].uvPoints == null || polygonRegions[i].uvPoints.Length == 0) continue;

                var color = Color.HSVToRGB((i * 0.618f) % 1f, 0.8f, 1f);
                var bounds = CalcUvBounds(polygonRegions[i].uvPoints);
                var rect = new Rect(
                    previewRect.x + bounds.x * previewRect.width,
                    previewRect.y + (1f - bounds.y - bounds.height) * previewRect.height,
                    bounds.width * previewRect.width,
                    bounds.height * previewRect.height);

                EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.2f));
                Handles.BeginGUI();
                Handles.color = color;
                Handles.DrawSolidRectangleWithOutline(rect, Color.clear, color);
                Handles.EndGUI();
            }
        }

        private void HandlePreviewClick(Rect previewRect)
        {
            var currentEvent = Event.current;
            if (currentEvent.type != EventType.MouseDown || !previewRect.Contains(currentEvent.mousePosition)) return;
            if (_cachedUvs == null || _cachedTriangles == null || _target == null || _regionIndex >= _target.eyeRegions.Count) return;

            Vector2 clickedUv = ScreenToUv(previewRect, currentEvent.mousePosition);
            var uvRegionsProp = _serializedObject.FindProperty("eyeRegions").GetArrayElementAtIndex(_regionIndex).FindPropertyRelative(GetUvRegionsPropertyName());

            if (currentEvent.button == 0)
            {
                var islandTriangles = FindIslandAt(clickedUv);
                if (islandTriangles.Count == 0) { currentEvent.Use(); return; }

                var pointSet = new HashSet<long>();
                var uvPoints = new List<Vector2>();
                foreach (int triangleIndex in islandTriangles)
                {
                    foreach (int vertexIndex in _cachedTriangles[triangleIndex])
                    {
                        var uv = _cachedUvs[vertexIndex];
                        if (pointSet.Add(QuantizeUv(uv))) uvPoints.Add(uv);
                    }
                }

                var existingRegions = _isSourceMode ? _target.eyeRegions[_regionIndex].sourceEyePolygonRegions : _target.eyeRegions[_regionIndex].eyePolygonRegions;
                foreach (var existing in existingRegions)
                {
                    if (existing.uvPoints == null || existing.uvPoints.Length != uvPoints.Count) continue;
                    bool same = true;
                    foreach (var point in existing.uvPoints)
                    {
                        if (!pointSet.Contains(QuantizeUv(point))) { same = false; break; }
                    }
                    if (same) { currentEvent.Use(); return; }
                }

                Undo.RecordObject(_target, "Add Eye UV Island");
                uvRegionsProp.arraySize++;
                var newRegionProp = uvRegionsProp.GetArrayElementAtIndex(uvRegionsProp.arraySize - 1).FindPropertyRelative("uvPoints");
                newRegionProp.arraySize = uvPoints.Count;
                for (int i = 0; i < uvPoints.Count; i++)
                    newRegionProp.GetArrayElementAtIndex(i).vector2Value = uvPoints[i];

                _serializedObject.ApplyModifiedProperties();
                foreach (int triangleIndex in islandTriangles) _selectedTriangles.Add(triangleIndex);
            }
            else if (currentEvent.button == 1)
            {
                int nearestTriangle = FindNearestTriangle(clickedUv);
                if (nearestTriangle >= 0)
                {
                    long key = QuantizeUv(_cachedUvs[_cachedTriangles[nearestTriangle][0]]);
                    var polygonRegions = _isSourceMode ? _target.eyeRegions[_regionIndex].sourceEyePolygonRegions : _target.eyeRegions[_regionIndex].eyePolygonRegions;
                    int removeIndex = -1;
                    for (int i = 0; i < polygonRegions.Length; i++)
                    {
                        if (polygonRegions[i].uvPoints == null) continue;
                        foreach (var point in polygonRegions[i].uvPoints)
                        {
                            if (QuantizeUv(point) == key) { removeIndex = i; break; }
                        }
                        if (removeIndex >= 0) break;
                    }

                    if (removeIndex >= 0)
                    {
                        Undo.RecordObject(_target, "Remove Eye UV Island");
                        uvRegionsProp.DeleteArrayElementAtIndex(removeIndex);
                        _serializedObject.ApplyModifiedProperties();
                        RefreshSelectionFromRects();
                    }
                }
            }

            currentEvent.Use();
        }

        private List<int> FindIslandAt(Vector2 clickedUv)
        {
            int nearestTriangle = FindNearestTriangle(clickedUv);
            return nearestTriangle < 0 ? new List<int>() : FloodFillIsland(nearestTriangle);
        }

        private int FindNearestTriangle(Vector2 clickedUv)
        {
            int nearestTriangle = -1;
            float minDistance = float.MaxValue;
            for (int i = 0; i < _cachedTriangles.Length; i++)
            {
                var tri = _cachedTriangles[i];
                Vector2 center = (_cachedUvs[tri[0]] + _cachedUvs[tri[1]] + _cachedUvs[tri[2]]) / 3f;
                float distance = (center - clickedUv).sqrMagnitude;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestTriangle = i;
                }
            }
            return nearestTriangle;
        }

        private List<int> FloodFillIsland(int startTriangle)
        {
            var uvToTriangles = new Dictionary<long, List<int>>();
            for (int i = 0; i < _cachedTriangles.Length; i++)
            {
                foreach (int vertexIndex in _cachedTriangles[i])
                {
                    long key = QuantizeUv(_cachedUvs[vertexIndex]);
                    if (!uvToTriangles.TryGetValue(key, out var triangles))
                    {
                        triangles = new List<int>();
                        uvToTriangles[key] = triangles;
                    }
                    if (!triangles.Contains(i)) triangles.Add(i);
                }
            }

            var visited = new HashSet<int> { startTriangle };
            var queue = new Queue<int>();
            queue.Enqueue(startTriangle);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int vertexIndex in _cachedTriangles[current])
                {
                    long key = QuantizeUv(_cachedUvs[vertexIndex]);
                    if (!uvToTriangles.TryGetValue(key, out var neighbors)) continue;
                    foreach (int neighbor in neighbors)
                        if (visited.Add(neighbor)) queue.Enqueue(neighbor);
                }
            }

            return new List<int>(visited);
        }

        private static void DrawIslandColorChip(int index)
        {
            var colorRect = GUILayoutUtility.GetRect(8f, 14f, GUILayout.Width(8f));
            EditorGUI.DrawRect(colorRect, Color.HSVToRGB((index * 0.618f) % 1f, 0.8f, 1f));
        }

        private static long QuantizeUv(Vector2 uv)
        {
            int x = Mathf.RoundToInt(uv.x * 10000f);
            int y = Mathf.RoundToInt(uv.y * 10000f);
            return ((long)x << 32) | (uint)y;
        }

        private static Rect CalcUvBounds(Vector2[] points)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var uv in points)
            {
                minX = Mathf.Min(minX, uv.x);
                minY = Mathf.Min(minY, uv.y);
                maxX = Mathf.Max(maxX, uv.x);
                maxY = Mathf.Max(maxY, uv.y);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Vector3 UvToScreen(Rect rect, Vector2 uv) =>
            new Vector3(rect.x + uv.x * rect.width, rect.y + (1f - uv.y) * rect.height, 0f);
        private static Vector2 ScreenToUv(Rect rect, Vector2 position) =>
            new Vector2((position.x - rect.x) / rect.width, 1f - (position.y - rect.y) / rect.height);

        private static void EnsureSceneHooks()
        {
            if (s_SceneHooksInitialized) return;
            SceneView.duringSceneGui += OnSceneGuiStatic;
            EditorApplication.update += OnEditorUpdateStatic;
            s_SceneHooksInitialized = true;
        }

        private static void OnEditorUpdateStatic()
        {
            if (s_ActiveSelector == null || s_ActiveSelector._selectedTriangles.Count == 0) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - s_LastSceneRepaintTime < 1.0 / 30.0) return;
            s_LastSceneRepaintTime = now;
            SceneView.RepaintAll();
        }

        private static void OnSceneGuiStatic(SceneView _) => s_ActiveSelector?.DrawSceneSelection();

        private void DrawSceneSelection()
        {
            if (_target == null || _regionIndex < 0 || _regionIndex >= _target.eyeRegions.Count) return;
            if (_cachedTriangles == null || _selectedTriangles.Count == 0) return;

            var region = _target.eyeRegions[_regionIndex];
            var smr = _isSourceMode ? region.sourceRenderer : region.targetRenderer;
            if (smr == null || smr.sharedMesh == null) return;

            var verts = smr.sharedMesh.vertices;
            var tf = smr.transform;
            float pulse = 0.5f + 0.5f * Mathf.Sin((float)(EditorApplication.timeSinceStartup * ScenePulseSpeed * Mathf.PI * 2.0));
            var fillColor = SceneFillBase; fillColor.a = Mathf.Lerp(0.05f, 0.35f, pulse);
            var borderColor = SceneBorderBase; borderColor.a = Mathf.Lerp(0.50f, 1.00f, pulse);

            foreach (int triIdx in _selectedTriangles)
            {
                if (triIdx >= _cachedTriangles.Length) continue;
                var tri = _cachedTriangles[triIdx];
                if (tri[0] >= verts.Length || tri[1] >= verts.Length || tri[2] >= verts.Length) continue;

                var p0 = tf.TransformPoint(verts[tri[0]]);
                var p1 = tf.TransformPoint(verts[tri[1]]);
                var p2 = tf.TransformPoint(verts[tri[2]]);
                Handles.color = fillColor;
                Handles.DrawAAConvexPolygon(p0, p1, p2);
                Handles.color = borderColor;
                Handles.DrawAAPolyLine(ScenePolylineWidth, p0, p1, p2, p0);
            }
        }
    }
}
