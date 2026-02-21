using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    /// <summary>
    /// 1つのEyeRegionに対してUV領域をインタラクティブに設定するEditorWindow。
    /// テクスチャ上で左クリック → UVIslandを追加、右クリック → 削除。
    /// </summary>
    public class ManacoWindow : EditorWindow
    {
        // ---- 対象 ----
        private Manaco _target;
        private SerializedObject _so;
        private int _regionIndex;

        // ---- プレビューキャッシュ ----
        private Texture2D _previewTexture;
        private Vector2[] _cachedUVs;   // materialIndex サブメッシュの頂点UV
        private int[][] _cachedTriangles; // materialIndex サブメッシュのトライアングル

        // eyeUVRects に対応する選択済みトライアングル
        private HashSet<int> _selectedTris = new HashSet<int>();

        private Vector2 _leftPanelScroll;
        private const float LeftPanelWidth = 260f;
        private static readonly Color SelectionColor = new Color(0.2f, 1.0f, 0.4f);

        // ============================================================
        //  開き方（Inspectorからのみ）
        // ============================================================

        public static void OpenWith(Manaco target, int regionIndex)
        {
            var w = GetWindow<ManacoWindow>("Eye UV Editor");
            w.SetRegion(target, regionIndex);
            w.Show();
        }

        // ============================================================
        //  ターゲット設定
        // ============================================================

        private void SetRegion(Manaco target, int regionIndex)
        {
            _target = target;
            _regionIndex = regionIndex;
            _so = target != null ? new SerializedObject(target) : null;
            RefreshPreviewCache();

            // ウィンドウタイトルをリージョン情報に合わせる
            if (target != null && regionIndex < target.eyeRegions.Count)
            {
                titleContent = new GUIContent($"Eye UV [{regionIndex}]");
            }
        }

        // ============================================================
        //  プレビューキャッシュの更新
        // ============================================================

        private void RefreshPreviewCache()
        {
            _previewTexture = null;
            _cachedUVs = null;
            _cachedTriangles = null;
            _selectedTris.Clear();

            if (_target == null) return;
            if (_regionIndex >= _target.eyeRegions.Count) return;

            var region = _target.eyeRegions[_regionIndex];
            if (region?.targetRenderer == null) return;

            var smr = region.targetRenderer;
            var mesh = smr.sharedMesh;
            if (mesh == null) return;

            // materialIndex のマテリアルからテクスチャを取得
            int matIdx = Mathf.Clamp(region.materialIndex, 0, smr.sharedMaterials.Length - 1);
            if (smr.sharedMaterials.Length > 0 && smr.sharedMaterials[matIdx] != null)
                _previewTexture = smr.sharedMaterials[matIdx].mainTexture as Texture2D;

            // materialIndex のサブメッシュのみUVキャッシュを作成
            _cachedUVs = mesh.uv;
            int subIdx = Mathf.Clamp(region.materialIndex, 0, mesh.subMeshCount - 1);
            var rawTris = mesh.GetTriangles(subIdx);
            var triList = new List<int[]>(rawTris.Length / 3);
            for (int i = 0; i < rawTris.Length; i += 3)
                triList.Add(new[] { rawTris[i], rawTris[i + 1], rawTris[i + 2] });
            _cachedTriangles = triList.ToArray();

            RefreshSelectionFromRects();
        }

        private void RefreshSelectionFromRects()
        {
            _selectedTris.Clear();
            if (_target == null || _cachedUVs == null || _cachedTriangles == null) return;
            if (_regionIndex >= _target.eyeRegions.Count) return;

            var region = _target.eyeRegions[_regionIndex];
            if (region.eyePolygonRegions == null) return;

            foreach (var polygonRegion in region.eyePolygonRegions)
            {
                if (polygonRegion.uvPoints == null || polygonRegion.uvPoints.Length == 0) continue;

                var pointSet = new HashSet<long>();
                foreach (var pt in polygonRegion.uvPoints)
                    pointSet.Add(QuantizeUV(pt));

                for (int i = 0; i < _cachedTriangles.Length; i++)
                {
                    var t = _cachedTriangles[i];
                    long q0 = QuantizeUV(_cachedUVs[t[0]]);
                    long q1 = QuantizeUV(_cachedUVs[t[1]]);
                    long q2 = QuantizeUV(_cachedUVs[t[2]]);

                    if (pointSet.Contains(q0) && pointSet.Contains(q1) && pointSet.Contains(q2))
                    {
                        _selectedTris.Add(i);
                    }
                }
            }
        }

        private static bool IsTriInRect(Vector2[] uvs, int i0, int i1, int i2, Rect rect)
            => rect.Contains(uvs[i0]) || rect.Contains(uvs[i1]) || rect.Contains(uvs[i2]);

        // ============================================================
        //  OnGUI
        // ============================================================

        private void OnGUI()
        {
            if (_target == null)
            {
                EditorGUILayout.HelpBox("Inspectorの「UV エディタを開く」ボタンから開いてください。", MessageType.Info);
                return;
            }

            if (_so == null || _so.targetObject == null)
                _so = new SerializedObject(_target);
            _so.Update();

            // ターゲットが変わっていないか確認
            if (_regionIndex >= _target.eyeRegions.Count)
            {
                EditorGUILayout.HelpBox("リージョンが削除されました。", MessageType.Warning);
                _so.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawLeftPanel();

            var sep = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(sep, new Color(0.1f, 0.1f, 0.1f));

            DrawRightPanel();
            EditorGUILayout.EndHorizontal();

            _so.ApplyModifiedProperties();
        }

        // ============================================================
        //  左パネル
        // ============================================================

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftPanelWidth), GUILayout.ExpandHeight(true));
            _leftPanelScroll = EditorGUILayout.BeginScrollView(_leftPanelScroll);
            EditorGUILayout.Space(4);

            var regionsProp = _so.FindProperty("eyeRegions");
            var elem = regionsProp.GetArrayElementAtIndex(_regionIndex);
            var rendererProp       = elem.FindPropertyRelative("targetRenderer");
            var matIndexProp       = elem.FindPropertyRelative("materialIndex");
            var uvRegionsProp      = elem.FindPropertyRelative("eyePolygonRegions");

            // ---- リージョン情報 ----
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // SMR（読み取り専用表示）
            var smr = rendererProp.objectReferenceValue as SkinnedMeshRenderer;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("SkinnedMeshRenderer", smr, typeof(SkinnedMeshRenderer), true);
            EditorGUI.EndDisabledGroup();

            // materialIndex（変更可：変えるとUV表示が切り替わる）
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(matIndexProp, new GUIContent("マテリアルスロット"));
            if (EditorGUI.EndChangeCheck())
            {
                _so.ApplyModifiedProperties();
                RefreshPreviewCache();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);

            // ---- UV Island一覧 ----
            EditorGUILayout.LabelField($"選択済み UV Island: {uvRegionsProp.arraySize} 個", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("右パネルをクリックして追加\n右クリックで削除", MessageType.None);

            for (int i = 0; i < uvRegionsProp.arraySize; i++)
            {
                var regionElem = uvRegionsProp.GetArrayElementAtIndex(i);
                var indicesProp = regionElem.FindPropertyRelative("uvPoints");
                int ptsCount = indicesProp.arraySize;

                EditorGUILayout.BeginHorizontal();

                // 色インジケーター
                float hue = (i * 0.618f) % 1f;
                var col = Color.HSVToRGB(hue, 0.8f, 1f);
                var indRect = GUILayoutUtility.GetRect(8f, 14f, GUILayout.Width(8f));
                EditorGUI.DrawRect(indRect, col);

                EditorGUILayout.LabelField(
                    $"[{i}]  UV頂点: {ptsCount} 個",
                    EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    uvRegionsProp.DeleteArrayElementAtIndex(i);
                    _so.ApplyModifiedProperties();
                    RefreshSelectionFromRects();
                    Repaint();
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (uvRegionsProp.arraySize == 0)
                EditorGUILayout.LabelField("  （未設定）", EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ============================================================
        //  右パネル（プレビュー）
        // ============================================================

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            var panelRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (panelRect.width < 10f || panelRect.height < 10f)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            float margin = 8f;
            float size = Mathf.Min(panelRect.width, panelRect.height) - margin * 2f;
            var previewRect = new Rect(
                panelRect.x + (panelRect.width - size) * 0.5f,
                panelRect.y + margin,
                size, size);

            EditorGUI.DrawRect(previewRect, new Color(0.12f, 0.12f, 0.12f));

            if (_previewTexture != null)
                GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.StretchToFill);
            else
                GUI.Label(previewRect, "テクスチャなし", EditorStyles.centeredGreyMiniLabel);

            DrawUVWireframe(previewRect);
            DrawUVRectOverlays(previewRect);

            if (_cachedUVs != null)
            {
                var hintRect = new Rect(previewRect.x, previewRect.yMax + 4f, previewRect.width, 20f);
                GUI.Label(hintRect, "左クリック: UVIslandを追加　右クリック: 削除",
                    EditorStyles.centeredGreyMiniLabel);
                EditorGUIUtility.AddCursorRect(previewRect, MouseCursor.Arrow);
            }

            HandlePreviewClick(previewRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawUVWireframe(Rect previewRect)
        {
            if (_cachedUVs == null || _cachedTriangles == null) return;

            Handles.BeginGUI();

            // 全トライアングル（薄い白）
            Handles.color = new Color(1f, 1f, 1f, 0.15f);
            foreach (var tri in _cachedTriangles)
            {
                var p0 = UVToScreen(previewRect, _cachedUVs[tri[0]]);
                var p1 = UVToScreen(previewRect, _cachedUVs[tri[1]]);
                var p2 = UVToScreen(previewRect, _cachedUVs[tri[2]]);
                Handles.DrawLine(p0, p1);
                Handles.DrawLine(p1, p2);
                Handles.DrawLine(p2, p0);
            }

            // 選択済みトライアングル（緑）
            Handles.color = new Color(SelectionColor.r, SelectionColor.g, SelectionColor.b, 0.7f);
            foreach (int idx in _selectedTris)
            {
                var tri = _cachedTriangles[idx];
                var p0 = UVToScreen(previewRect, _cachedUVs[tri[0]]);
                var p1 = UVToScreen(previewRect, _cachedUVs[tri[1]]);
                var p2 = UVToScreen(previewRect, _cachedUVs[tri[2]]);
                Handles.DrawLine(p0, p1);
                Handles.DrawLine(p1, p2);
                Handles.DrawLine(p2, p0);
            }

            Handles.EndGUI();
        }

        private void DrawUVRectOverlays(Rect previewRect)
        {
            if (_target == null || _regionIndex >= _target.eyeRegions.Count) return;
            var region = _target.eyeRegions[_regionIndex];
            if (region.eyePolygonRegions == null) return;

            for (int i = 0; i < region.eyePolygonRegions.Length; i++)
            {
                float hue = (i * 0.618f) % 1f;
                var col = Color.HSVToRGB(hue, 0.8f, 1f);

                if (region.eyePolygonRegions[i].uvPoints == null || region.eyePolygonRegions[i].uvPoints.Length == 0) continue;

                Rect uvRect = CalcUVBounds(region.eyePolygonRegions[i].uvPoints);

                DrawRectOverlay(previewRect, uvRect,
                    new Color(col.r, col.g, col.b, 0.25f),
                    new Color(col.r, col.g, col.b, 1f),
                    i.ToString());
            }
        }

        private void DrawRectOverlay(Rect previewRect, Rect uvRect,
            Color fill, Color border, string label)
        {
            float rx = previewRect.x + uvRect.x * previewRect.width;
            float ry = previewRect.y + (1f - uvRect.y - uvRect.height) * previewRect.height;
            float rw = uvRect.width  * previewRect.width;
            float rh = uvRect.height * previewRect.height;

            if (rw <= 0f || rh <= 0f) return;

            var r = new Rect(rx, ry, rw, rh);
            EditorGUI.DrawRect(r, fill);

            Handles.BeginGUI();
            Handles.color = border;
            Handles.DrawSolidRectangleWithOutline(r, Color.clear, border);
            Handles.EndGUI();

            if (rw > 20f && rh > 14f)
            {
                var s = new GUIStyle(EditorStyles.miniLabel);
                s.normal.textColor = border;
                GUI.Label(new Rect(rx + 2f, ry + 2f, rw - 4f, 16f), label, s);
            }
        }

        // ============================================================
        //  クリックイベント処理
        // ============================================================

        private void HandlePreviewClick(Rect previewRect)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown) return;
            if (!previewRect.Contains(e.mousePosition)) return;
            if (_cachedUVs == null || _cachedTriangles == null) return;
            if (_target == null || _regionIndex >= _target.eyeRegions.Count) return;

            var clickedUV = ScreenToUV(previewRect, e.mousePosition);

            if (e.button == 0)
            {
                // 左クリック: UVIslandを追加
                var islandTris = FindUVIslandAt(clickedUV);
                if (islandTris.Count == 0) { e.Use(); return; }

                var pointSet = new HashSet<long>();
                var uvPointsList = new List<Vector2>();
                foreach (int triIdx in islandTris)
                {
                    foreach (int vi in _cachedTriangles[triIdx])
                    {
                        var uv = _cachedUVs[vi];
                        long q = QuantizeUV(uv);
                        if (pointSet.Add(q))
                        {
                            uvPointsList.Add(uv);
                        }
                    }
                }

                // 既に含まれているかチェック
                var region = _target.eyeRegions[_regionIndex];
                foreach (var existing in region.eyePolygonRegions)
                {
                    if (existing.uvPoints != null && existing.uvPoints.Length == uvPointsList.Count)
                    {
                        int matchCount = 0;
                        foreach (var pt in existing.uvPoints)
                        {
                            if (pointSet.Contains(QuantizeUV(pt))) matchCount++;
                        }
                        if (matchCount == uvPointsList.Count) { e.Use(); return; }
                    }
                }

                Undo.RecordObject(_target, "Add Eye UV Island");
                var uvRegionsProp = _so.FindProperty("eyeRegions")
                    .GetArrayElementAtIndex(_regionIndex)
                    .FindPropertyRelative("eyePolygonRegions");
                uvRegionsProp.arraySize++;
                var newElemProp = uvRegionsProp.GetArrayElementAtIndex(uvRegionsProp.arraySize - 1);
                var indicesProp = newElemProp.FindPropertyRelative("uvPoints");
                indicesProp.arraySize = uvPointsList.Count;
                for (int i = 0; i < uvPointsList.Count; i++)
                {
                    indicesProp.GetArrayElementAtIndex(i).vector2Value = uvPointsList[i];
                }

                _so.ApplyModifiedProperties();

                foreach (int ti in islandTris) _selectedTris.Add(ti);
            }
            else if (e.button == 1)
            {
                // 右クリック: トライアングルを含むIslandを削除
                var region = _target.eyeRegions[_regionIndex];

                // クリック位置から最も近いトライアングルを見つける
                int nearestIdx = -1;
                float minDist = float.MaxValue;
                for (int i = 0; i < _cachedTriangles.Length; i++)
                {
                    var t = _cachedTriangles[i];
                    var center = (_cachedUVs[t[0]] + _cachedUVs[t[1]] + _cachedUVs[t[2]]) / 3f;
                    float d = (center - clickedUV).sqrMagnitude;
                    if (d < minDist) { minDist = d; nearestIdx = i; }
                }

                if (nearestIdx >= 0)
                {
                    long q0 = QuantizeUV(_cachedUVs[_cachedTriangles[nearestIdx][0]]);
                    int removeIdx = -1;
                    for (int i = 0; i < region.eyePolygonRegions.Length; i++)
                    {
                        if (region.eyePolygonRegions[i].uvPoints == null) continue;

                        bool contains = false;
                        foreach (var pt in region.eyePolygonRegions[i].uvPoints)
                        {
                            if (QuantizeUV(pt) == q0)
                            {
                                contains = true;
                                break;
                            }
                        }
                        if (contains)
                        {
                            removeIdx = i;
                            break;
                        }
                    }

                    if (removeIdx >= 0)
                    {
                        Undo.RecordObject(_target, "Remove Eye UV Island");
                        var uvRegionsProp = _so.FindProperty("eyeRegions")
                            .GetArrayElementAtIndex(_regionIndex)
                            .FindPropertyRelative("eyePolygonRegions");
                        uvRegionsProp.DeleteArrayElementAtIndex(removeIdx);
                        _so.ApplyModifiedProperties();
                        RefreshSelectionFromRects();
                    }
                }
            }

            e.Use();
            Repaint();
        }

        // ============================================================
        //  UVIsland探索
        // ============================================================

        private List<int> FindUVIslandAt(Vector2 clickedUV)
        {
            if (_cachedUVs == null || _cachedTriangles == null) return new List<int>();

            int nearestIdx = -1;
            float minDist = float.MaxValue;
            for (int i = 0; i < _cachedTriangles.Length; i++)
            {
                var t = _cachedTriangles[i];
                var center = (_cachedUVs[t[0]] + _cachedUVs[t[1]] + _cachedUVs[t[2]]) / 3f;
                float d = (center - clickedUV).sqrMagnitude;
                if (d < minDist) { minDist = d; nearestIdx = i; }
            }

            return nearestIdx < 0 ? new List<int>() : FloodFillUVIsland(nearestIdx);
        }

        private List<int> FloodFillUVIsland(int startTriIdx)
        {
            var uvToTris = new Dictionary<long, List<int>>();
            for (int i = 0; i < _cachedTriangles.Length; i++)
            {
                foreach (int vi in _cachedTriangles[i])
                {
                    long key = QuantizeUV(_cachedUVs[vi]);
                    if (!uvToTris.TryGetValue(key, out var list))
                        uvToTris[key] = list = new List<int>();
                    if (!list.Contains(i)) list.Add(i);
                }
            }

            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(startTriIdx);
            visited.Add(startTriIdx);

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (int vi in _cachedTriangles[cur])
                {
                    long key = QuantizeUV(_cachedUVs[vi]);
                    if (!uvToTris.TryGetValue(key, out var neighbors)) continue;
                    foreach (int ni in neighbors)
                    {
                        if (visited.Add(ni)) queue.Enqueue(ni);
                    }
                }
            }

            return new List<int>(visited);
        }

        private static long QuantizeUV(Vector2 uv)
        {
            int xi = Mathf.RoundToInt(uv.x * 10000);
            int yi = Mathf.RoundToInt(uv.y * 10000);
            return ((long)xi << 32) | (uint)yi;
        }

        private Rect CalcUVBounds(Vector2[] points)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var uv in points)
            {
                if (uv.x < minX) minX = uv.x;
                if (uv.y < minY) minY = uv.y;
                if (uv.x > maxX) maxX = uv.x;
                if (uv.y > maxY) maxY = uv.y;
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        // ============================================================
        //  座標変換
        // ============================================================

        private static Vector3 UVToScreen(Rect r, Vector2 uv) =>
            new Vector3(r.x + uv.x * r.width, r.y + (1f - uv.y) * r.height, 0f);

        private static Vector2 ScreenToUV(Rect r, Vector2 pos) =>
            new Vector2((pos.x - r.x) / r.width, 1f - (pos.y - r.y) / r.height);
    }
}
