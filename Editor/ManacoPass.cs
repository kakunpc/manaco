using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEngine;
using UnityEngine.Rendering;

namespace com.kakunvr.manaco
{
    /// <summary>
    /// ビルド時に目のポリゴンを別SubMeshに分割し、UV を [0, 1] に正規化するパス。
    /// 頂点は複製して元のメッシュデータに影響を与えない。
    /// </summary>
    public class ManacoPass
    {
        public void Execute(BuildContext ctx)
        {
            var components = ctx.AvatarRootObject
                .GetComponentsInChildren<Manaco>(true);
            foreach (var component in components)
                ProcessComponent(component);
        }

        private void ProcessComponent(Manaco component)
        {
            foreach (var region in component.eyeRegions)
            {
                if (region.targetRenderer == null)
                {
                    Debug.LogWarning("[Manaco] targetRenderer が未設定のEyeRegionがあります。スキップします。", component);
                    continue;
                }
                if (region.eyePolygonRegions == null || region.eyePolygonRegions.Length == 0)
                {
                    Debug.LogWarning("[Manaco] eyePolygonRegions が空のEyeRegionがあります。スキップします。", component);
                    continue;
                }
                ApplyEyeSubMesh(region, region.targetRenderer);
            }
            UnityEngine.Object.DestroyImmediate(component);
        }

        public Mesh ApplyEyeSubMesh(Manaco.EyeRegion region, SkinnedMeshRenderer smr)
        {
            var originalMesh = smr.sharedMesh;
            if (originalMesh == null)
            {
                Debug.LogWarning($"[Manaco] {smr.name} のsharedMeshがnullです。スキップします。");
                return null;
            }

            var mesh = UnityEngine.Object.Instantiate(originalMesh);
            mesh.name = originalMesh.name + "_Manaco";

            var uvs = mesh.uv;
            if (uvs.Length == 0)
            {
                Debug.LogWarning($"[Manaco] {smr.name} にUVが設定されていません。スキップします。");
                UnityEngine.Object.DestroyImmediate(mesh);
                return null;
            }

            // ---- 選択されたUV頂点をセット化 ----
            var selectedUVPoints = new HashSet<long>();
            if (region.eyePolygonRegions != null)
            {
                foreach (var pr in region.eyePolygonRegions)
                {
                    if (pr.uvPoints != null)
                    {
                        foreach (var pt in pr.uvPoints)
                        {
                            selectedUVPoints.Add(QuantizeUV(pt));
                        }
                    }
                }
            }

            // ---- 目のトライアングルを収集（materialIndex のサブメッシュのみ対象） ----
            int subMeshCount = mesh.subMeshCount;
            var allTrianglesBySubMesh = new List<List<int>>(subMeshCount);
            var eyeTriangles = new List<int>();

            int targetSubIdx = Mathf.Clamp(region.materialIndex, 0, subMeshCount - 1);

            for (int s = 0; s < subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);

                // materialIndex 以外のサブメッシュはそのまま保持
                if (s != targetSubIdx)
                {
                    allTrianglesBySubMesh.Add(tris.ToList());
                    continue;
                }

                var desc = mesh.GetSubMesh(s);
                if (desc.topology != MeshTopology.Triangles)
                {
                    allTrianglesBySubMesh.Add(tris.ToList());
                    continue;
                }

                var remaining = new List<int>();
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                    long q0 = QuantizeUV(uvs[i0]);
                    long q1 = QuantizeUV(uvs[i1]);
                    long q2 = QuantizeUV(uvs[i2]);

                    bool inEye = selectedUVPoints.Contains(q0) && selectedUVPoints.Contains(q1) && selectedUVPoints.Contains(q2);

                    if (inEye) { eyeTriangles.Add(i0); eyeTriangles.Add(i1); eyeTriangles.Add(i2); }
                    else       { remaining.Add(i0);    remaining.Add(i1);    remaining.Add(i2);    }
                }
                allTrianglesBySubMesh.Add(remaining);
            }

            if (eyeTriangles.Count == 0)
            {
                Debug.LogWarning($"[Manaco] {smr.name}: 指定されたUV島内にポリゴンが見つかりませんでした。UV設定を確認してください。");
                UnityEngine.Object.DestroyImmediate(mesh);
                return null;
            }

            // ---- 目の頂点UV境界を計算 ----
            var eyeVertSet = new HashSet<int>(eyeTriangles);
            float minU = float.MaxValue, minV = float.MaxValue;
            float maxU = float.MinValue, maxV = float.MinValue;
            foreach (int vi in eyeVertSet)
            {
                if (uvs[vi].x < minU) minU = uvs[vi].x;
                if (uvs[vi].y < minV) minV = uvs[vi].y;
                if (uvs[vi].x > maxU) maxU = uvs[vi].x;
                if (uvs[vi].y > maxV) maxV = uvs[vi].y;
            }
            float rangeU = Mathf.Max(maxU - minU, 1e-5f);
            float rangeV = Mathf.Max(maxV - minV, 1e-5f);

            // ---- ブレンドシェイプを頂点数変更前に保存 ----
            int origVertCount = mesh.vertexCount;
            int blendShapeCount = mesh.blendShapeCount;
            var blendShapeCache = new List<(string name, List<(float weight, Vector3[] dv, Vector3[] dn, Vector3[] dt)> frames)>(blendShapeCount);
            for (int si = 0; si < blendShapeCount; si++)
            {
                string shapeName = mesh.GetBlendShapeName(si);
                int frameCount = mesh.GetBlendShapeFrameCount(si);
                var frames = new List<(float, Vector3[], Vector3[], Vector3[])>(frameCount);
                for (int fi = 0; fi < frameCount; fi++)
                {
                    float w = mesh.GetBlendShapeFrameWeight(si, fi);
                    var dv = new Vector3[origVertCount];
                    var dn = new Vector3[origVertCount];
                    var dt = new Vector3[origVertCount];
                    mesh.GetBlendShapeFrameVertices(si, fi, dv, dn, dt);
                    frames.Add((w, dv, dn, dt));
                }
                blendShapeCache.Add((shapeName, frames));
            }

            // ---- 目の頂点を複製し UV を [0,1] に再割当て ----
            var verts    = new List<Vector3>(mesh.vertices);
            var normals  = new List<Vector3>(mesh.normals);
            var tangents = new List<Vector4>(mesh.tangents);
            var uvList   = new List<Vector2>(uvs);
            var bwList   = new List<BoneWeight>(mesh.boneWeights);
            var colList  = new List<Color32>(mesh.colors32);

            var uv2 = new List<Vector2>(); mesh.GetUVs(1, uv2);
            var uv3 = new List<Vector2>(); mesh.GetUVs(2, uv3);
            var uv4 = new List<Vector2>(); mesh.GetUVs(3, uv4);

            var oldToNew = new Dictionary<int, int>(eyeVertSet.Count);
            foreach (int vi in eyeVertSet)
            {
                int newIdx = verts.Count;
                oldToNew[vi] = newIdx;

                verts.Add(verts[vi]);
                if (normals.Count  > vi) normals.Add(normals[vi]);
                if (tangents.Count > vi) tangents.Add(tangents[vi]);
                if (bwList.Count   > vi) bwList.Add(bwList[vi]);
                if (colList.Count  > vi) colList.Add(colList[vi]);
                if (uv2.Count      > vi) uv2.Add(uv2[vi]);
                if (uv3.Count      > vi) uv3.Add(uv3[vi]);
                if (uv4.Count      > vi) uv4.Add(uv4[vi]);

                // UV を境界BOX基準で [0, 1] に正規化
                uvList.Add(new Vector2(
                    (uvs[vi].x - minU) / rangeU,
                    (uvs[vi].y - minV) / rangeV));
            }

            // 目トライアングルのインデックスを複製頂点に差し替え
            for (int i = 0; i < eyeTriangles.Count; i++)
                eyeTriangles[i] = oldToNew[eyeTriangles[i]];

            // ---- 頂点データをメッシュに書き戻す ----
            mesh.SetVertices(verts);
            if (normals.Count  > 0) mesh.SetNormals(normals);
            if (tangents.Count > 0) mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvList);
            if (bwList.Count  > 0) mesh.boneWeights = bwList.ToArray();
            if (colList.Count > 0) mesh.SetColors(colList);
            if (uv2.Count     > 0) mesh.SetUVs(1, uv2);
            if (uv3.Count     > 0) mesh.SetUVs(2, uv3);
            if (uv4.Count     > 0) mesh.SetUVs(3, uv4);

            // ---- ブレンドシェイプを再構築（複製頂点分のデルタをコピー） ----
            if (blendShapeCache.Count > 0)
            {
                mesh.ClearBlendShapes();
                int newVertCount = verts.Count;
                foreach (var (shapeName, frames) in blendShapeCache)
                {
                    foreach (var (w, dv, dn, dt) in frames)
                    {
                        var newDv = new Vector3[newVertCount];
                        var newDn = new Vector3[newVertCount];
                        var newDt = new Vector3[newVertCount];
                        Array.Copy(dv, newDv, origVertCount);
                        Array.Copy(dn, newDn, origVertCount);
                        Array.Copy(dt, newDt, origVertCount);
                        foreach (var (oldIdx, newIdx) in oldToNew)
                        {
                            newDv[newIdx] = dv[oldIdx];
                            newDn[newIdx] = dn[oldIdx];
                            newDt[newIdx] = dt[oldIdx];
                        }
                        mesh.AddBlendShapeFrame(shapeName, w, newDv, newDn, newDt);
                    }
                }
            }

            // ---- SubMeshを再構築 ----
            mesh.subMeshCount = subMeshCount + 1;
            for (int s = 0; s < subMeshCount; s++)
                mesh.SetTriangles(allTrianglesBySubMesh[s], s);
            mesh.SetTriangles(eyeTriangles, subMeshCount);

            mesh.RecalculateBounds();

            // 新SubMeshにカスタムマテリアルを割り当て
            var eyeMaterial = region.customMaterial;
            if (region.bakeFallbackTexture)
                eyeMaterial = BakeFallbackTexture(eyeMaterial, region.fallbackTextureResolution);

            var materials = smr.sharedMaterials.ToList();
            materials.Add(eyeMaterial);
            smr.sharedMesh = mesh;
            smr.sharedMaterials = materials.ToArray();

            return mesh;
        }

        /// <summary>
        /// マテリアルのシェーダーを Graphics.Blit でレンダリングしてフォールバックテクスチャを生成し、
        /// _MainTex に設定したクローンマテリアルを返す。
        /// _MainTex がすでに設定済みか _MainTex プロパティがない場合は元のマテリアルをそのまま返す。
        /// </summary>
        private static Material BakeFallbackTexture(Material sourceMaterial, int resolution)
        {
            if (sourceMaterial == null) return null;

            if (!sourceMaterial.HasProperty("_MainTex"))
            {
                Debug.LogWarning($"[Manaco] {sourceMaterial.name} に _MainTex プロパティがないため、フォールバックテクスチャのベイクをスキップします。");
                return sourceMaterial;
            }

            if (sourceMaterial.GetTexture("_MainTex") != null)
            {
                Debug.Log($"[Manaco] {sourceMaterial.name} の _MainTex はすでに設定済みのため、ベイクをスキップします。");
                return sourceMaterial;
            }

            int res = Mathf.Clamp(resolution, 64, 2048);

            // Graphics.Blit が source を _MainTex にセットするため、副作用を避けるために一時コピーでベイク
            var tempMat = new Material(sourceMaterial);
            var rt = RenderTexture.GetTemporary(res, res, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.filterMode = FilterMode.Bilinear;

            Graphics.Blit(null, rt, tempMat);
            UnityEngine.Object.DestroyImmediate(tempMat);

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            var fallbackTex = new Texture2D(res, res, TextureFormat.RGBA32, true);
            fallbackTex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            fallbackTex.Apply(true);
            fallbackTex.name = sourceMaterial.name + "_Fallback";

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            // 元のマテリアルをクローンして _MainTex にベイク済みテクスチャをセット
            var clonedMaterial = new Material(sourceMaterial);
            clonedMaterial.name = sourceMaterial.name + "_WithFallback";
            clonedMaterial.SetTexture("_MainTex", fallbackTex);

            // Debug.Log($"[Manaco] {sourceMaterial.name} のフォールバックテクスチャをベイクしました ({res}x{res})");

            return clonedMaterial;
        }

        private static long QuantizeUV(Vector2 uv)
        {
            int xi = Mathf.RoundToInt(uv.x * 10000);
            int yi = Mathf.RoundToInt(uv.y * 10000);
            return ((long)xi << 32) | (uint)yi;
        }

        private static bool IsTriangleInUVRect(Vector2[] uvs, int i0, int i1, int i2, Rect rect)
            => UVInRect(uvs[i0], rect) || UVInRect(uvs[i1], rect) || UVInRect(uvs[i2], rect);

        // Rect.Contains は右端・上端を exclusive にするため inclusive な比較を使う
        private static bool UVInRect(Vector2 uv, Rect r)
            => uv.x >= r.xMin && uv.x <= r.xMax && uv.y >= r.yMin && uv.y <= r.yMax;
    }
}
