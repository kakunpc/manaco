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
    /// ビルド時に目のポリゴンを別SubMeshに分割し、UV を円形テクスチャ向けに再配置するパス。
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
            bool useLightweightMode = IsLightweightModeEnabled(component);

            foreach (var region in component.eyeRegions.OrderBy(GetLightweightPriority))
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

                var eyeMaterial = ResolveBuildEyeMaterial(region, component, fallbackMaterialCache);
                if (eyeMaterial == null)
                    continue;

                if (useLightweightMode)
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

        private static bool IsLightweightModeEnabled(Manaco component)
        {
            return component != null &&
                   (component.mode == Manaco.ManacoMode.CopyEyeFromAvatar ||
                    (component.mode == Manaco.ManacoMode.EyeMaterialAssignment && component.useLightweightMode));
        }

        private static int GetLightweightPriority(Manaco.EyeRegion region)
        {
            return region.eyeType switch
            {
                Manaco.EyeType.LeftPupil => 1,
                Manaco.EyeType.RightPupil => 1,
                _ => 0,
            };
        }

        internal static Material ResolveEyeMaterial(
            Manaco.EyeRegion region,
            Manaco component,
            Dictionary<(Material material, int resolution, bool forceRender), Material> fallbackMaterialCache)
        {
            var eyeMaterial = region.customMaterial;
            if (eyeMaterial == null)
                return eyeMaterial;

            if (component != null && component.mode == Manaco.ManacoMode.CopyEyeFromAvatar)
                return eyeMaterial;

            bool forceRender = IsLightweightModeEnabled(component);
            if (!forceRender && !region.bakeFallbackTexture)
                return eyeMaterial;

            int resolution = forceRender
                ? Mathf.Clamp(
                    component != null && component.mode == Manaco.ManacoMode.CopyEyeFromAvatar
                        ? region.extractTextureResolution
                        : component.lightweightTextureResolution,
                    64,
                    2048)
                : Mathf.Clamp(region.fallbackTextureResolution, 64, 2048);
            var key = (eyeMaterial, resolution, forceRender);
            if (!fallbackMaterialCache.TryGetValue(key, out var cachedMaterial))
            {
                cachedMaterial = forceRender
                    ? CreateRenderedTextureMaterial(eyeMaterial, resolution)
                    : BakeFallbackTexture(eyeMaterial, resolution, false);
                fallbackMaterialCache[key] = cachedMaterial;
            }

            return cachedMaterial;
        }

        internal static Material ResolveBuildEyeMaterial(
            Manaco.EyeRegion region,
            Manaco component,
            Dictionary<(Material material, int resolution, bool forceRender), Material> fallbackMaterialCache)
        {
            if (region == null || component == null)
                return null;

            if (component.mode == Manaco.ManacoMode.CopyEyeFromAvatar)
            {
                if (region.sourceRenderer == null)
                {
                    Debug.LogWarning("[Manaco] CopyEyeFromAvatar: sourceRenderer が未設定のEyeRegionがあります。スキップします。", component);
                    return null;
                }

                return ManacoEyeCopyProcessor.PrepareEyeCopyMaterial(region);
            }

            if (region.customMaterial == null)
            {
                Debug.LogWarning("[Manaco] customMaterial が未設定のEyeRegionがあります。スキップします。", component);
                return null;
            }

            return ResolveEyeMaterial(region, component, fallbackMaterialCache);
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

            var eyeVertSet = new HashSet<int>(eyeTriangles);
            var circularUvMapping = BuildCircularUvMapping(uvs, eyeTriangles);

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

                uvList.Add(circularUvMapping.TryGetVertexUv(vi, out var circularUv)
                    ? circularUv
                    : new Vector2(0.5f, 0.5f));
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

        private static Material CreateRenderedTextureMaterial(Material sourceMaterial, int resolution)
        {
            if (sourceMaterial == null) return null;

            int res = Mathf.Clamp(resolution, 64, 2048);
            var renderedTexture = RenderMaterialToTexture(sourceMaterial, res, sourceMaterial.name + "_Rendered");
            if (renderedTexture == null)
                return null;

            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                UnityEngine.Object.DestroyImmediate(renderedTexture);
                return null;
            }

            var renderedMaterial = new Material(shader)
            {
                name = sourceMaterial.name + "_RenderedMaterial"
            };
            renderedMaterial.SetTexture("_MainTex", renderedTexture);
            return renderedMaterial;
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

        internal sealed class CircularUvMapping
        {
            internal readonly Dictionary<int, Vector2> VertexUvs = new Dictionary<int, Vector2>();
            internal readonly List<CircularUvTriangle> Triangles = new List<CircularUvTriangle>();

            internal bool TryGetVertexUv(int vertexIndex, out Vector2 uv) => VertexUvs.TryGetValue(vertexIndex, out uv);
        }

        internal sealed class CircularUvTriangle
        {
            internal readonly Vector2 Uv0;
            internal readonly Vector2 Uv1;
            internal readonly Vector2 Uv2;
            internal readonly Vector2 RemappedUv0;
            internal readonly Vector2 RemappedUv1;
            internal readonly Vector2 RemappedUv2;

            internal CircularUvTriangle(Vector2 uv0, Vector2 uv1, Vector2 uv2, CircularUvIsland island)
            {
                Uv0 = uv0;
                Uv1 = uv1;
                Uv2 = uv2;
                RemappedUv0 = island.Remap(uv0);
                RemappedUv1 = island.Remap(uv1);
                RemappedUv2 = island.Remap(uv2);
            }

            internal Vector2 InterpolateRemapped(float w0, float w1, float w2, float inverseArea)
            {
                float b0 = w0 * inverseArea;
                float b1 = w1 * inverseArea;
                float b2 = w2 * inverseArea;
                var uv = RemappedUv0 * b0 + RemappedUv1 * b1 + RemappedUv2 * b2;
                return new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
            }
        }

        internal static CircularUvMapping BuildCircularUvMapping(Vector2[] uvs, IReadOnlyList<int> triangleIndices)
        {
            var mapping = new CircularUvMapping();
            if (uvs == null || triangleIndices == null || triangleIndices.Count < 3)
                return mapping;

            foreach (var component in SplitCircularUvComponents(uvs, triangleIndices))
            {
                var island = CreateCircularUvIsland(uvs, triangleIndices, component);
                if (island == null)
                    continue;

                foreach (int triangleIndex in component)
                {
                    int start = triangleIndex * 3;
                    if (!TryGetTriangleVertices(triangleIndices, start, uvs.Length, out int i0, out int i1, out int i2))
                        continue;

                    var triangle = new CircularUvTriangle(uvs[i0], uvs[i1], uvs[i2], island);
                    mapping.Triangles.Add(triangle);
                    mapping.VertexUvs[i0] = triangle.RemappedUv0;
                    mapping.VertexUvs[i1] = triangle.RemappedUv1;
                    mapping.VertexUvs[i2] = triangle.RemappedUv2;
                }
            }

            return mapping;
        }

        private static List<List<int>> SplitCircularUvComponents(Vector2[] uvs, IReadOnlyList<int> triangleIndices)
        {
            int triangleCount = triangleIndices.Count / 3;
            var uvToTriangles = new Dictionary<long, List<int>>();
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                int start = triangleIndex * 3;
                if (!TryGetTriangleVertices(triangleIndices, start, uvs.Length, out int i0, out int i1, out int i2))
                    continue;

                AddTriangleReference(uvToTriangles, QuantizeUV(uvs[i0]), triangleIndex);
                AddTriangleReference(uvToTriangles, QuantizeUV(uvs[i1]), triangleIndex);
                AddTriangleReference(uvToTriangles, QuantizeUV(uvs[i2]), triangleIndex);
            }

            var components = new List<List<int>>();
            var visited = new HashSet<int>();
            for (int startTriangle = 0; startTriangle < triangleCount; startTriangle++)
            {
                if (!visited.Add(startTriangle))
                    continue;

                var component = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(startTriangle);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    component.Add(current);

                    int start = current * 3;
                    if (!TryGetTriangleVertices(triangleIndices, start, uvs.Length, out int i0, out int i1, out int i2))
                        continue;

                    EnqueueUvNeighbors(uvToTriangles, visited, queue, QuantizeUV(uvs[i0]));
                    EnqueueUvNeighbors(uvToTriangles, visited, queue, QuantizeUV(uvs[i1]));
                    EnqueueUvNeighbors(uvToTriangles, visited, queue, QuantizeUV(uvs[i2]));
                }

                if (component.Count > 0)
                    components.Add(component);
            }

            return components;
        }

        private static CircularUvIsland CreateCircularUvIsland(
            Vector2[] uvs,
            IReadOnlyList<int> triangleIndices,
            IReadOnlyList<int> component)
        {
            var points = new Dictionary<long, UvPointAccumulator>();
            var triangleIncidence = new Dictionary<long, int>();
            foreach (int triangleIndex in component)
            {
                int start = triangleIndex * 3;
                if (!TryGetTriangleVertices(triangleIndices, start, uvs.Length, out int i0, out int i1, out int i2))
                    continue;

                AddPoint(points, uvs[i0]);
                AddPoint(points, uvs[i1]);
                AddPoint(points, uvs[i2]);

                var uniqueTrianglePoints = new HashSet<long>
                {
                    QuantizeUV(uvs[i0]),
                    QuantizeUV(uvs[i1]),
                    QuantizeUV(uvs[i2])
                };
                foreach (long key in uniqueTrianglePoints)
                    triangleIncidence[key] = triangleIncidence.TryGetValue(key, out int count) ? count + 1 : 1;
            }

            if (points.Count == 0)
                return null;

            var center = EstimateCircularCenter(points, triangleIncidence);
            var bounds = CalculateBounds(points);
            var edgeCounts = CollectComponentEdges(uvs, triangleIndices, component, points);
            var boundaryEdges = new List<CircularUvEdge>();
            var boundaryPointKeys = new HashSet<long>();
            foreach (var entry in edgeCounts)
            {
                if (entry.Value.Count != 1)
                    continue;

                var edge = entry.Value;
                boundaryEdges.Add(new CircularUvEdge(edge.A, edge.B));
                boundaryPointKeys.Add(entry.Key.Item1);
                boundaryPointKeys.Add(entry.Key.Item2);
            }

            var boundaryPoints = new List<Vector2>();
            foreach (long key in boundaryPointKeys)
            {
                if (!points.TryGetValue(key, out var point))
                    continue;

                var uv = point.Average;
                if ((uv - center).sqrMagnitude > 1e-10f)
                    boundaryPoints.Add(uv);
            }

            if (boundaryPoints.Count == 0)
            {
                foreach (var point in points.Values)
                {
                    var uv = point.Average;
                    if ((uv - center).sqrMagnitude > 1e-10f)
                        boundaryPoints.Add(uv);
                }
            }

            return new CircularUvIsland(center, bounds, boundaryEdges, boundaryPoints);
        }

        private static Dictionary<(long, long), UvEdgeAccumulator> CollectComponentEdges(
            Vector2[] uvs,
            IReadOnlyList<int> triangleIndices,
            IReadOnlyList<int> component,
            Dictionary<long, UvPointAccumulator> points)
        {
            var edgeCounts = new Dictionary<(long, long), UvEdgeAccumulator>();
            foreach (int triangleIndex in component)
            {
                int start = triangleIndex * 3;
                if (!TryGetTriangleVertices(triangleIndices, start, uvs.Length, out int i0, out int i1, out int i2))
                    continue;

                AddEdge(edgeCounts, points, QuantizeUV(uvs[i0]), QuantizeUV(uvs[i1]));
                AddEdge(edgeCounts, points, QuantizeUV(uvs[i1]), QuantizeUV(uvs[i2]));
                AddEdge(edgeCounts, points, QuantizeUV(uvs[i2]), QuantizeUV(uvs[i0]));
            }

            return edgeCounts;
        }

        private static Vector2 EstimateCircularCenter(
            Dictionary<long, UvPointAccumulator> points,
            Dictionary<long, int> triangleIncidence)
        {
            var average = Vector2.zero;
            foreach (var point in points.Values)
                average += point.Average;
            average /= Mathf.Max(1, points.Count);

            long bestKey = 0;
            int bestCount = -1;
            int secondCount = -1;
            foreach (var entry in triangleIncidence)
            {
                if (entry.Value > bestCount)
                {
                    secondCount = bestCount;
                    bestCount = entry.Value;
                    bestKey = entry.Key;
                }
                else if (entry.Value > secondCount)
                {
                    secondCount = entry.Value;
                }
            }

            if (bestCount >= 3 && bestCount > secondCount && points.TryGetValue(bestKey, out var bestPoint))
            {
                var candidate = bestPoint.Average;
                float maxDistance = 0f;
                foreach (var point in points.Values)
                    maxDistance = Mathf.Max(maxDistance, (point.Average - average).magnitude);

                if (maxDistance <= 1e-5f || (candidate - average).magnitude <= maxDistance * 0.65f)
                    return candidate;
            }

            return average;
        }

        private static Rect CalculateBounds(Dictionary<long, UvPointAccumulator> points)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            foreach (var point in points.Values)
            {
                var uv = point.Average;
                minX = Mathf.Min(minX, uv.x);
                minY = Mathf.Min(minY, uv.y);
                maxX = Mathf.Max(maxX, uv.x);
                maxY = Mathf.Max(maxY, uv.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static bool TryGetTriangleVertices(
            IReadOnlyList<int> triangleIndices,
            int start,
            int vertexCount,
            out int i0,
            out int i1,
            out int i2)
        {
            i0 = i1 = i2 = -1;
            if (start < 0 || start + 2 >= triangleIndices.Count)
                return false;

            i0 = triangleIndices[start];
            i1 = triangleIndices[start + 1];
            i2 = triangleIndices[start + 2];
            return i0 >= 0 && i0 < vertexCount &&
                   i1 >= 0 && i1 < vertexCount &&
                   i2 >= 0 && i2 < vertexCount;
        }

        private static void AddTriangleReference(Dictionary<long, List<int>> uvToTriangles, long uvKey, int triangleIndex)
        {
            if (!uvToTriangles.TryGetValue(uvKey, out var triangles))
            {
                triangles = new List<int>();
                uvToTriangles[uvKey] = triangles;
            }

            if (!triangles.Contains(triangleIndex))
                triangles.Add(triangleIndex);
        }

        private static void EnqueueUvNeighbors(
            Dictionary<long, List<int>> uvToTriangles,
            HashSet<int> visited,
            Queue<int> queue,
            long uvKey)
        {
            if (!uvToTriangles.TryGetValue(uvKey, out var neighbors))
                return;

            foreach (int neighbor in neighbors)
                if (visited.Add(neighbor))
                    queue.Enqueue(neighbor);
        }

        private static void AddPoint(Dictionary<long, UvPointAccumulator> points, Vector2 uv)
        {
            long key = QuantizeUV(uv);
            if (!points.TryGetValue(key, out var point))
            {
                point = new UvPointAccumulator();
                points[key] = point;
            }

            point.Add(uv);
        }

        private static void AddEdge(
            Dictionary<(long, long), UvEdgeAccumulator> edgeCounts,
            Dictionary<long, UvPointAccumulator> points,
            long keyA,
            long keyB)
        {
            if (keyA == keyB)
                return;

            var key = keyA < keyB ? (keyA, keyB) : (keyB, keyA);
            if (!edgeCounts.TryGetValue(key, out var edge))
            {
                edge = new UvEdgeAccumulator
                {
                    A = points[key.Item1].Average,
                    B = points[key.Item2].Average
                };
                edgeCounts[key] = edge;
            }

            edge.Count++;
        }

        internal sealed class CircularUvIsland
        {
            private const float Epsilon = 1e-6f;
            private readonly Vector2 _center;
            private readonly Rect _bounds;
            private readonly List<CircularUvEdge> _boundaryEdges;
            private readonly List<Vector2> _boundaryPoints;

            internal CircularUvIsland(
                Vector2 center,
                Rect bounds,
                List<CircularUvEdge> boundaryEdges,
                List<Vector2> boundaryPoints)
            {
                _center = center;
                _bounds = bounds;
                _boundaryEdges = boundaryEdges;
                _boundaryPoints = boundaryPoints;
            }

            internal Vector2 Remap(Vector2 uv)
            {
                var delta = uv - _center;
                float distance = delta.magnitude;
                if (distance <= Epsilon)
                    return new Vector2(0.5f, 0.5f);

                var direction = delta / distance;
                float boundaryRadius = FindBoundaryRadius(direction);
                if (boundaryRadius <= Epsilon)
                    return RemapByBounds(uv);

                float normalizedRadius = Mathf.Clamp01(distance / boundaryRadius);
                return new Vector2(
                    Mathf.Clamp01(0.5f + direction.x * normalizedRadius * 0.5f),
                    Mathf.Clamp01(0.5f + direction.y * normalizedRadius * 0.5f));
            }

            private float FindBoundaryRadius(Vector2 direction)
            {
                float nearest = float.MaxValue;
                foreach (var edge in _boundaryEdges)
                {
                    if (TryRaySegmentIntersection(_center, direction, edge, out float distance))
                        nearest = Mathf.Min(nearest, distance);
                }

                if (nearest < float.MaxValue)
                    return nearest;

                return FindNearestBoundaryPointRadius(direction);
            }

            private float FindNearestBoundaryPointRadius(Vector2 direction)
            {
                float bestDot = -1f;
                float radius = 0f;
                foreach (var point in _boundaryPoints)
                {
                    var delta = point - _center;
                    float distance = delta.magnitude;
                    if (distance <= Epsilon)
                        continue;

                    float dot = Vector2.Dot(delta / distance, direction);
                    if (dot <= bestDot)
                        continue;

                    bestDot = dot;
                    radius = distance;
                }

                if (bestDot > 0.95f && radius > Epsilon)
                    return radius;

                return FindBoundsRadius(direction);
            }

            private float FindBoundsRadius(Vector2 direction)
            {
                float radius = float.MaxValue;
                if (Mathf.Abs(direction.x) > Epsilon)
                {
                    float x = direction.x > 0f ? _bounds.xMax : _bounds.xMin;
                    float tx = (x - _center.x) / direction.x;
                    if (tx > Epsilon)
                        radius = Mathf.Min(radius, tx);
                }

                if (Mathf.Abs(direction.y) > Epsilon)
                {
                    float y = direction.y > 0f ? _bounds.yMax : _bounds.yMin;
                    float ty = (y - _center.y) / direction.y;
                    if (ty > Epsilon)
                        radius = Mathf.Min(radius, ty);
                }

                if (radius < float.MaxValue)
                    return radius;

                return Mathf.Max(_bounds.width, _bounds.height) * 0.5f;
            }

            private Vector2 RemapByBounds(Vector2 uv)
            {
                return new Vector2(
                    Mathf.Clamp01(Mathf.InverseLerp(_bounds.xMin, _bounds.xMax, uv.x)),
                    Mathf.Clamp01(Mathf.InverseLerp(_bounds.yMin, _bounds.yMax, uv.y)));
            }

            private static bool TryRaySegmentIntersection(
                Vector2 origin,
                Vector2 direction,
                CircularUvEdge edge,
                out float distance)
            {
                distance = 0f;
                var segment = edge.B - edge.A;
                var relative = edge.A - origin;
                float denominator = Cross(direction, segment);
                if (Mathf.Abs(denominator) <= Epsilon)
                {
                    if (Mathf.Abs(Cross(relative, direction)) > 1e-5f)
                        return false;

                    float t0 = Vector2.Dot(edge.A - origin, direction);
                    float t1 = Vector2.Dot(edge.B - origin, direction);
                    distance = Mathf.Max(t0, t1);
                    return distance > Epsilon;
                }

                float t = Cross(relative, segment) / denominator;
                float u = Cross(relative, direction) / denominator;
                if (t <= Epsilon || u < -1e-4f || u > 1f + 1e-4f)
                    return false;

                distance = t;
                return true;
            }
        }

        private sealed class UvPointAccumulator
        {
            private Vector2 _sum;
            private int _count;

            internal Vector2 Average => _count > 0 ? _sum / _count : Vector2.zero;

            internal void Add(Vector2 uv)
            {
                _sum += uv;
                _count++;
            }
        }

        private sealed class UvEdgeAccumulator
        {
            internal Vector2 A;
            internal Vector2 B;
            internal int Count;
        }

        internal readonly struct CircularUvEdge
        {
            internal readonly Vector2 A;
            internal readonly Vector2 B;

            internal CircularUvEdge(Vector2 a, Vector2 b)
            {
                A = a;
                B = b;
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool IsTriangleInUVRect(Vector2[] uvs, int i0, int i1, int i2, Rect rect)
            => UVInRect(uvs[i0], rect) || UVInRect(uvs[i1], rect) || UVInRect(uvs[i2], rect);

        // Rect.Contains は右端・上端を exclusive にするため inclusive な比較を使う
        private static bool UVInRect(Vector2 uv, Rect r)
            => uv.x >= r.xMin && uv.x <= r.xMax && uv.y >= r.yMin && uv.y <= r.yMax;
    }
}
