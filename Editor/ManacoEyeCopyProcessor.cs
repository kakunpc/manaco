using System.Collections.Generic;
using UnityEngine;

namespace com.kakunvr.manaco.Editor
{
    /// <summary>
    /// CopyEyeFromAvatar モードのビルド時テクスチャ抽出処理コア。
    /// PrepareEyeCopyMaterial でソースマテリアルの UV0 テクスチャを
    /// 目領域から切り出し、region.customMaterial にセットする。
    /// </summary>
    public static class ManacoEyeCopyProcessor
    {
        /// <summary>
        /// CopyEyeFromAvatar モード用マテリアルを生成して返す。
        /// region.customMaterial には書き込まない（呼び出し側が管理する）。
        /// </summary>
        public static Material PrepareEyeCopyMaterial(Manaco.EyeRegion region)
        {
            // 1. sourceRenderer / sourceMesh / sourceMaterial を取得・検証
            if (region.sourceRenderer == null)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: sourceRenderer が未設定です。スキップします。");
                return null;
            }

            var sourceMesh = region.sourceRenderer.sharedMesh;
            if (sourceMesh == null)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: sourceRenderer の sharedMesh が null です。スキップします。");
                return null;
            }

            var sourceMaterials = region.sourceRenderer.sharedMaterials;
            int matIdx = Mathf.Clamp(region.sourceMaterialIndex, 0, sourceMaterials.Length - 1);
            var sourceMaterial = sourceMaterials[matIdx];
            if (sourceMaterial == null)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: sourceMaterial が null です。スキップします。");
                return null;
            }

            if (region.sourceEyePolygonRegions == null || region.sourceEyePolygonRegions.Length == 0)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: sourceEyePolygonRegions が空です。スキップします。");
                return null;
            }

            // 2. sourceEyePolygonRegions → QuantizeUV でセット化
            var selectedUVPoints = new HashSet<long>();
            foreach (var pr in region.sourceEyePolygonRegions)
            {
                if (pr.uvPoints != null)
                    foreach (var pt in pr.uvPoints)
                        selectedUVPoints.Add(ManacoPass.QuantizeUV(pt));
            }

            // 3. sourceMesh のトライアングルから目頂点特定
            var uvs = sourceMesh.uv;
            if (uvs.Length == 0)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: sourceMesh にUVがありません。スキップします。");
                return null;
            }

            int sourceSubIdx = Mathf.Clamp(region.sourceMaterialIndex, 0, sourceMesh.subMeshCount - 1);
            var sourceTris = sourceMesh.GetTriangles(sourceSubIdx);
            var eyeVertSet = new HashSet<int>();
            for (int i = 0; i < sourceTris.Length; i += 3)
            {
                int i0 = sourceTris[i], i1 = sourceTris[i + 1], i2 = sourceTris[i + 2];
                if (selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i0])) &&
                    selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i1])) &&
                    selectedUVPoints.Contains(ManacoPass.QuantizeUV(uvs[i2])))
                {
                    eyeVertSet.Add(i0);
                    eyeVertSet.Add(i1);
                    eyeVertSet.Add(i2);
                }
            }

            if (eyeVertSet.Count == 0)
            {
                Debug.LogWarning("[Manaco] CopyEyeFromAvatar: ソースの目頂点が見つかりませんでした。UV設定を確認してください。");
                return null;
            }

            // 4. 目頂点の UV AABB を計算
            float minU = float.MaxValue, minV = float.MaxValue;
            float maxU = float.MinValue, maxV = float.MinValue;
            foreach (int vi in eyeVertSet)
            {
                if (uvs[vi].x < minU) minU = uvs[vi].x;
                if (uvs[vi].y < minV) minV = uvs[vi].y;
                if (uvs[vi].x > maxU) maxU = uvs[vi].x;
                if (uvs[vi].y > maxV) maxV = uvs[vi].y;
            }

            // 5. UV0 テクスチャ（_ST プロパティを持つもの）を抽出してクローンマテリアルにセット
            int res = Mathf.Clamp(region.extractTextureResolution, 64, 2048);
            var clonedMat = new Material(sourceMaterial);
            clonedMat.name = sourceMaterial.name + "_ManacoCopy";

            var texPropNames = sourceMaterial.GetTexturePropertyNames();
            foreach (var propName in texPropNames)
            {
                // UV0 テクスチャ判定: 対応する _ST プロパティを持つもの
                if (!sourceMaterial.HasProperty(propName + "_ST")) continue;

                var srcTex = sourceMaterial.GetTexture(propName);
                if (srcTex == null) continue;

                // _ST スケール・オフセットを適用した UV 範囲を計算
                var stVec = sourceMaterial.GetVector(propName + "_ST");
                float scaledMinU = minU * stVec.x + stVec.z;
                float scaledMinV = minV * stVec.y + stVec.w;
                float scaledMaxU = maxU * stVec.x + stVec.z;
                float scaledMaxV = maxV * stVec.y + stVec.w;

                var extracted = ExtractTextureRegion(srcTex, scaledMinU, scaledMinV, scaledMaxU, scaledMaxV, res);
                if (extracted != null)
                {
                    clonedMat.SetTexture(propName, extracted);
                    // 正規化済みなので ST をリセット
                    clonedMat.SetTextureScale(propName, Vector2.one);
                    clonedMat.SetTextureOffset(propName, Vector2.zero);
                }
            }

            return clonedMat;
        }

        /// <summary>
        /// ソーステクスチャの UV 範囲 [minU,maxU]×[minV,maxV] を
        /// res×res の Texture2D に切り出して返す。
        /// Graphics.Blit(src, rt, scale, offset) オーバーロードを使用することで
        /// UV クロップを正しく適用する。
        /// </summary>
        private static Texture2D ExtractTextureRegion(
            Texture src,
            float minU, float minV, float maxU, float maxV,
            int res)
        {
            float scaleU = maxU - minU;
            float scaleV = maxV - minV;
            if (Mathf.Approximately(scaleU, 0f) || Mathf.Approximately(scaleV, 0f)) return null;

            var rt = RenderTexture.GetTemporary(
                res, res, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.filterMode = FilterMode.Bilinear;

            // scale/offset オーバーロード:
            //   出力 UV (0→1) が ソース UV (minU→maxU, minV→maxV) に対応する
            Graphics.Blit(src, rt, new Vector2(scaleU, scaleV), new Vector2(minU, minV));

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            var tex2d = new Texture2D(res, res, TextureFormat.RGBA32, true);
            tex2d.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            tex2d.Apply(true);
            tex2d.name = src.name + "_EyeExtract";

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            return tex2d;
        }
    }
}
