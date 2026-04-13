using System;
using System.Collections.Generic;
using System.Linq;
using com.kakunvr.manaco.Editor;
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
        internal sealed class PreviewMeshSnapshot
        {
            public Vector3[] Vertices { get; set; }
            public Vector3[] Normals { get; set; }
            public Vector4[] Tangents { get; set; }
        }

        public void Execute(BuildContext ctx)
        {
            var components = ctx.AvatarRootObject
                .GetComponentsInChildren<Manaco>(true);
            foreach (var component in components)
                ProcessComponent(component);
        }

        private void ProcessComponent(Manaco component)
        {
            var fallbackMaterialCache = new Dictionary<(Material material, int resolution, bool forceRender), Material>();
            var lightweightMaterialCache = new Dictionary<(SkinnedMeshRenderer renderer, int materialIndex), Material>();

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

                if (component.mode == Manaco.ManacoMode.CopyEyeFromAvatar)
                {
                    if (region.sourceRenderer == null)
                    {
                        Debug.LogWarning("[Manaco] CopyEyeFromAvatar: sourceRenderer が未設定のEyeRegionがあります。スキップします。", component);
                        continue;
                    }
                    // NDMF はクローン上で実行されるため region への書き込みは安全
                    region.customMaterial = ManacoEyeCopyProcessor.PrepareEyeCopyMaterial(region);
                    if (region.customMaterial == null) continue;
                }
                else if (region.customMaterial == null)
                {
                    Debug.LogWarning("[Manaco] customMaterial が未設定のEyeRegionがあります。スキップします。", component);
                    continue;
                }

                var eyeMaterial = ResolveEyeMaterial(region, component, fallbackMaterialCache);
                if (component.useLightweightMode)
                {
                    ManacoLightweightUtility.ApplyLightweightMaterial(
                        region,
                        region.targetRenderer,
                        eyeMaterial,
                        lightweightMaterialCache);
                }
                else
                {
                    ApplyEyeSubMesh(region, region.targetRenderer, eyeMaterial);
                }
            }
            UnityEngine.Object.DestroyImmediate(component);
        }

        internal static Material ResolveEyeMaterial(
            Manaco.EyeRegion region,
            Manaco component,
            Dictionary<(Material material, int resolution, bool forceRender), Material> fallbackMaterialCache)
        {
            var eyeMaterial = region.customMaterial;
            if (eyeMaterial == null)
                return eyeMaterial;

            bool forceRender = component != null && component.useLightweightMode;
            if (!forceRender && !region.bakeFallbackTexture)
                return eyeMaterial;

            int resolution = forceRender
                ? Mathf.Clamp(component.lightweightTextureResolution, 64, 2048)
                : Mathf.Clamp(region.fallbackTextureResolution, 64, 2048);
            var key = (eyeMaterial, resolution, forceRender);
            if (!fallbackMaterialCache.TryGetValue(key, out var cachedMaterial))
            {
                cachedMaterial = BakeFallbackTexture(eyeMaterial, resolution, forceRender);
                fallbackMaterialCache[key] = cachedMaterial;
            }

            return cachedMaterial;
        }

        internal Mesh ApplyEyeSubMesh(
            Manaco.EyeRegion region,
            SkinnedMeshRenderer smr,
            Material overrideMaterial = null,
            bool preserveBlendShapes = true,
            Mesh bakedShapeMesh = null,
            PreviewMeshSnapshot previewMeshSnapshot = null)
        {
            var originalMesh = smr.sharedMesh;
            if (originalMesh == null)
            {
                Debug.LogWarning($"[Manaco] {smr.name} のsharedMeshがnullです。スキップします。");
                return null;
            }

            var mesh = UnityEngine.Object.Instantiate(originalMesh);
            mesh.name = originalMesh.name + "_Manaco";

            if (bakedShapeMesh != null && bakedShapeMesh.vertexCount == mesh.vertexCount)
            {
                mesh.vertices = bakedShapeMesh.vertices;

                var bakedNormals = bakedShapeMesh.normals;
                if (bakedNormals != null && bakedNormals.Length == mesh.vertexCount)
                    mesh.normals = bakedNormals;

                var bakedTangents = bakedShapeMesh.tangents;
                if (bakedTangents != null && bakedTangents.Length == mesh.vertexCount)
                    mesh.tangents = bakedTangents;
            }
            else if (previewMeshSnapshot != null && previewMeshSnapshot.Vertices != null &&
                     previewMeshSnapshot.Vertices.Length == mesh.vertexCount)
            {
                mesh.vertices = previewMeshSnapshot.Vertices;

                var previewNormals = previewMeshSnapshot.Normals;
                if (previewNormals != null && previewNormals.Length == mesh.vertexCount)
                    mesh.normals = previewNormals;

                var previewTangents = previewMeshSnapshot.Tangents;
                if (previewTangents != null && previewTangents.Length == mesh.vertexCount)
                    mesh.tangents = previewTangents;
            }

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
                Debug.LogWarning($"[Manaco] {smr.name}: 指定されたUV Island内にポリゴンが見つかりませんでした。UV設定を確認してください。");
                UnityEngine.Object.DestroyImmediate(mesh);
                return null;
            }

            // ---- 目の頂点UV境界を計算（楕円→円形リマッピング用） ----
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
            float centerU = (minU + maxU) * 0.5f;
            float centerV = (minV + maxV) * 0.5f;
            float rangeU  = Mathf.Max(maxU - minU, 1e-5f);
            float rangeV  = Mathf.Max(maxV - minV, 1e-5f);

            // ---- ブレンドシェイプを頂点数変更前に保存 ----
            if (!preserveBlendShapes && mesh.blendShapeCount > 0)
            {
                // Preview only: avoid the full blendshape copy/rebuild cost.
                mesh.ClearBlendShapes();
            }

            int origVertCount = mesh.vertexCount;
            int blendShapeCount = preserveBlendShapes ? mesh.blendShapeCount : 0;
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

                // U・V を独立スケールして [0, 1] に正規化（楕円→円形マッピング）
                uvList.Add(new Vector2(
                    0.5f + (uvs[vi].x - centerU) / rangeU,
                    0.5f + (uvs[vi].y - centerV) / rangeV));
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
            mesh.subMeshCount = subMeshCount;
            for (int s = 0; s < subMeshCount; s++)
                mesh.SetTriangles(allTrianglesBySubMesh[s], s);

            mesh.RecalculateBounds();

            // 新SubMeshにカスタムマテリアルを割り当て（override が渡された場合はそちらを優先）
            var eyeMaterial = overrideMaterial ?? region.customMaterial;
            var materials = smr.sharedMaterials.ToList();
            int materialSlot = materials.FindIndex(mat => mat == eyeMaterial);
            if (materialSlot >= 0)
            {
                allTrianglesBySubMesh[materialSlot].AddRange(eyeTriangles);
                mesh.SetTriangles(allTrianglesBySubMesh[materialSlot], materialSlot);
            }
            else
            {
                materialSlot = subMeshCount;
                mesh.subMeshCount = subMeshCount + 1;
                mesh.SetTriangles(eyeTriangles, materialSlot);
                materials.Add(eyeMaterial);
            }
            smr.sharedMesh = mesh;
            smr.sharedMaterials = materials.ToArray();

            return mesh;
        }

        internal static PreviewMeshSnapshot CaptureBlendShapePreviewSnapshot(
            SkinnedMeshRenderer meshSourceSmr,
            SkinnedMeshRenderer weightSourceSmr)
        {
            var mesh = meshSourceSmr.sharedMesh;
            if (weightSourceSmr == null) weightSourceSmr = meshSourceSmr;
            if (mesh == null) return null;

            int vertexCount = mesh.vertexCount;
            if (vertexCount == 0 || mesh.blendShapeCount == 0) return null;

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;

            var deformedVertices = (Vector3[])vertices.Clone();
            Vector3[] deformedNormals =
                normals != null && normals.Length == vertexCount ? (Vector3[])normals.Clone() : null;
            Vector4[] deformedTangents =
                tangents != null && tangents.Length == vertexCount ? (Vector4[])tangents.Clone() : null;

            var frameVertices = new Vector3[vertexCount];
            var frameNormals = new Vector3[vertexCount];
            var frameTangents = new Vector3[vertexCount];

            for (int shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
            {
                float weight = weightSourceSmr.GetBlendShapeWeight(shapeIndex);
                if (Mathf.Approximately(weight, 0f)) continue;

                int frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);
                if (frameCount == 0) continue;

                ApplyBlendShapeWeight(mesh, shapeIndex, weight, frameVertices, frameNormals, frameTangents,
                    deformedVertices, deformedNormals, deformedTangents);
            }

            return new PreviewMeshSnapshot
            {
                Vertices = deformedVertices,
                Normals = deformedNormals,
                Tangents = deformedTangents,
            };
        }

        private static void ApplyBlendShapeWeight(
            Mesh mesh,
            int shapeIndex,
            float weight,
            Vector3[] frameVertices,
            Vector3[] frameNormals,
            Vector3[] frameTangents,
            Vector3[] deformedVertices,
            Vector3[] deformedNormals,
            Vector4[] deformedTangents)
        {
            int frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);
            if (frameCount == 1)
            {
                float frameWeight = mesh.GetBlendShapeFrameWeight(shapeIndex, 0);
                float factor = Mathf.Approximately(frameWeight, 0f) ? 0f : weight / frameWeight;
                AccumulateBlendShapeFrame(mesh, shapeIndex, 0, factor, frameVertices, frameNormals, frameTangents,
                    deformedVertices, deformedNormals, deformedTangents);
                return;
            }

            int lowerFrame = 0;
            int upperFrame = frameCount - 1;
            for (int i = 0; i < frameCount; i++)
            {
                float frameWeight = mesh.GetBlendShapeFrameWeight(shapeIndex, i);
                if (frameWeight <= weight) lowerFrame = i;
                if (frameWeight >= weight)
                {
                    upperFrame = i;
                    break;
                }
            }

            if (lowerFrame == upperFrame)
            {
                float frameWeight = mesh.GetBlendShapeFrameWeight(shapeIndex, lowerFrame);
                float factor = Mathf.Approximately(frameWeight, 0f) ? 0f : weight / frameWeight;
                AccumulateBlendShapeFrame(mesh, shapeIndex, lowerFrame, factor, frameVertices, frameNormals, frameTangents,
                    deformedVertices, deformedNormals, deformedTangents);
                return;
            }

            float lowerWeight = mesh.GetBlendShapeFrameWeight(shapeIndex, lowerFrame);
            float upperWeight = mesh.GetBlendShapeFrameWeight(shapeIndex, upperFrame);
            float t = Mathf.Approximately(upperWeight - lowerWeight, 0f)
                ? 0f
                : Mathf.InverseLerp(lowerWeight, upperWeight, weight);

            AccumulateInterpolatedBlendShapeFrames(mesh, shapeIndex, lowerFrame, upperFrame, t,
                frameVertices, frameNormals, frameTangents, deformedVertices, deformedNormals, deformedTangents);
        }

        private static void AccumulateBlendShapeFrame(
            Mesh mesh,
            int shapeIndex,
            int frameIndex,
            float factor,
            Vector3[] frameVertices,
            Vector3[] frameNormals,
            Vector3[] frameTangents,
            Vector3[] deformedVertices,
            Vector3[] deformedNormals,
            Vector4[] deformedTangents)
        {
            if (Mathf.Approximately(factor, 0f)) return;

            Array.Clear(frameVertices, 0, frameVertices.Length);
            Array.Clear(frameNormals, 0, frameNormals.Length);
            Array.Clear(frameTangents, 0, frameTangents.Length);
            mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, frameVertices, frameNormals, frameTangents);

            for (int i = 0; i < deformedVertices.Length; i++)
            {
                deformedVertices[i] += frameVertices[i] * factor;
                if (deformedNormals != null)
                    deformedNormals[i] += frameNormals[i] * factor;
                if (deformedTangents != null)
                {
                    var tangentDelta = frameTangents[i] * factor;
                    deformedTangents[i].x += tangentDelta.x;
                    deformedTangents[i].y += tangentDelta.y;
                    deformedTangents[i].z += tangentDelta.z;
                }
            }
        }

        private static void AccumulateInterpolatedBlendShapeFrames(
            Mesh mesh,
            int shapeIndex,
            int lowerFrameIndex,
            int upperFrameIndex,
            float t,
            Vector3[] frameVertices,
            Vector3[] frameNormals,
            Vector3[] frameTangents,
            Vector3[] deformedVertices,
            Vector3[] deformedNormals,
            Vector4[] deformedTangents)
        {
            var lowerVertices = new Vector3[frameVertices.Length];
            var lowerNormals = new Vector3[frameNormals.Length];
            var lowerTangents = new Vector3[frameTangents.Length];
            mesh.GetBlendShapeFrameVertices(shapeIndex, lowerFrameIndex, lowerVertices, lowerNormals, lowerTangents);

            Array.Clear(frameVertices, 0, frameVertices.Length);
            Array.Clear(frameNormals, 0, frameNormals.Length);
            Array.Clear(frameTangents, 0, frameTangents.Length);
            mesh.GetBlendShapeFrameVertices(shapeIndex, upperFrameIndex, frameVertices, frameNormals, frameTangents);

            for (int i = 0; i < deformedVertices.Length; i++)
            {
                deformedVertices[i] += Vector3.LerpUnclamped(lowerVertices[i], frameVertices[i], t);
                if (deformedNormals != null)
                    deformedNormals[i] += Vector3.LerpUnclamped(lowerNormals[i], frameNormals[i], t);
                if (deformedTangents != null)
                {
                    var tangentDelta = Vector3.LerpUnclamped(lowerTangents[i], frameTangents[i], t);
                    deformedTangents[i].x += tangentDelta.x;
                    deformedTangents[i].y += tangentDelta.y;
                    deformedTangents[i].z += tangentDelta.z;
                }
            }
        }

        /// <summary>
        /// マテリアルのシェーダーを Graphics.Blit でレンダリングしてフォールバックテクスチャを生成し、
        /// _MainTex に設定したクローンマテリアルを返す。
        /// _MainTex がすでに設定済みか _MainTex プロパティがない場合は元のマテリアルをそのまま返す。
        /// </summary>
        private static Material BakeFallbackTexture(Material sourceMaterial, int resolution, bool forceRender = false)
        {
            if (sourceMaterial == null) return null;

            if (!sourceMaterial.HasProperty("_MainTex"))
            {
                Debug.LogWarning($"[Manaco] {sourceMaterial.name} に _MainTex プロパティがないため、フォールバックテクスチャのベイクをスキップします。");
                return sourceMaterial;
            }

            if (!forceRender && sourceMaterial.GetTexture("_MainTex") != null)
            {
                Debug.Log($"[Manaco] {sourceMaterial.name} の _MainTex はすでに設定済みのため、ベイクをスキップします。");
                return sourceMaterial;
            }

            int res = Mathf.Clamp(resolution, 64, 2048);

            // Graphics.Blit が source を _MainTex にセットするため、副作用を避けるために一時コピーでベイク
            var fallbackTex = RenderMaterialToTexture(sourceMaterial, res, sourceMaterial.name + "_Fallback");
            if (fallbackTex == null) return sourceMaterial;

            // 元のマテリアルをクローンして _MainTex にベイク済みテクスチャをセット
            var clonedMaterial = new Material(sourceMaterial);
            clonedMaterial.name = sourceMaterial.name + "_WithFallback";
            clonedMaterial.SetTexture("_MainTex", fallbackTex);

            // Debug.Log($"[Manaco] {sourceMaterial.name} のフォールバックテクスチャをベイクしました ({res}x{res})");

            return clonedMaterial;
        }

        private static Texture2D RenderMaterialToTexture(Material sourceMaterial, int resolution, string textureName)
        {
            var tempMat = new Material(sourceMaterial);
            var rt = RenderTexture.GetTemporary(
                resolution,
                resolution,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            rt.filterMode = FilterMode.Bilinear;

            Graphics.Blit(null, rt, tempMat);
            UnityEngine.Object.DestroyImmediate(tempMat);

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            texture.Apply(true);
            texture.name = textureName;

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            return texture;
        }

        internal static long QuantizeUV(Vector2 uv)
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
